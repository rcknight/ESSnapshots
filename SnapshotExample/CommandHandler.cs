using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Newtonsoft.Json;

namespace SnapshotExample
{
    public class ResourceCommandHandler
    {
        private readonly IEventStoreConnection _connection;
        private readonly bool _enableSnapshots;
        private const int ReadPageSize = 250;
        private const int SnapshotThreshold = 250;

        public ResourceCommandHandler(IEventStoreConnection connection, bool enableSnapshots)
        {
            _connection = connection;
            _enableSnapshots = enableSnapshots;
        }

       
        public void Handle(CreateResource cmd)
        {
            var aggregate = new Resource();
            aggregate.Create(cmd.ResourceId, cmd.Name);
            WriteEvents(cmd.ResourceId, ExpectedVersion.NoStream, aggregate.GetUncommittedEvents());
        }

        
        public void Handle(BookResource cmd)
        {
            var sw = Stopwatch.StartNew();
            var snapshot = LoadSnapshot(cmd.ResourceId);
          
            var aggregate = HydrateResource(cmd.ResourceId, snapshot);
            var expectedVersion = aggregate.Version; // This is the version we expect the stream to be
                                                     // provided nobody snuck in and wrote more events 
                                                     // while we were processing the command
            sw.Stop();
            
            Console.WriteLine($"{sw.ElapsedMilliseconds}ms");
            
            aggregate.Book(cmd.Date, cmd.TimeSlot, cmd.ActivityId);
            WriteEvents(cmd.ResourceId, expectedVersion, aggregate.GetUncommittedEvents());
            
            //if we have more than X events since our last snapshot, write a new one
            //you could also have some external subscriber process which is responsible for creating these
            if (_enableSnapshots && (aggregate.Version + 1) % SnapshotThreshold == 0)
                WriteSnapshot(cmd.ResourceId, aggregate.ProduceSnapshot());
        }
        
        
        // Event store reading/writing
        // Probably you would make something a little more generic to share this code between all command handlers
        // but we only have one so they can live here
        
        private void WriteSnapshot(Guid resourceId, Resource.Snapshot snapshot)
        {
            string streamName = $"resourceSnapshots-{resourceId}";
            var json = JsonConvert.SerializeObject(snapshot);
            var esEvent = new EventData(Guid.NewGuid(), "ResourceSnapshot", true, Encoding.UTF8.GetBytes(json), null);
            var result = _connection.AppendToStreamAsync(streamName, ExpectedVersion.Any, esEvent).Result;

            // This is the first ever snapshot, do a one off metadata update for the stream
            // We never need to keep anything but the latest snapshot, so set maxCount to 1
            // Event Store will eventually delete the data during a scavenge
            if (result.NextExpectedVersion == 0)
                _connection.SetStreamMetadataAsync(streamName, ExpectedVersion.Any,
                    StreamMetadata.Create(maxCount: 1));
        }
        
        private void WriteEvents(Guid resourceId, long expectedVersion, IEnumerable<Event> events)
        {
            var esEvents = events.Select(e =>
            {
                var data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(e));
                return new EventData(Guid.NewGuid(), e.GetType().Name, true, data, null);
            });
            
            _connection.AppendToStreamAsync($"resource-{resourceId}", expectedVersion, esEvents).Wait();
        }

        
        public Resource HydrateResource(Guid resourceId, Resource.Snapshot snapshot)
        {
            var aggregate = new Resource();
            var streamName = $"resource-{resourceId}";
            var eventsAssembly = typeof(Event).Assembly.GetName().Name;
            
            if(snapshot != null)
                aggregate.ApplySnapshot(snapshot);
            
            // Now read any events written since the last snapshot.
            // If we failed to load a snapshot, just start from the beginning of the stream
            var nextEventNumber = snapshot?.Version + 1 ?? StreamPosition.Start;             
            StreamEventsSlice slice = null;
            var sliceTask = _connection.ReadStreamEventsForwardAsync(streamName, nextEventNumber, ReadPageSize, false);
            
            do
            {
                slice = sliceTask.Result;
                nextEventNumber = slice.NextEventNumber;
                
                //start reading the next slice while we process this one
                sliceTask = _connection.ReadStreamEventsForwardAsync(streamName, nextEventNumber, ReadPageSize, false);
                
                var deserialized = slice.Events.Select(e =>
                {
                    var eventData = Encoding.UTF8.GetString(e.Event.Data);
                    return JsonConvert.DeserializeObject(eventData,Type.GetType($"{eventsAssembly}.{e.Event.EventType}"));
                });
                
                // Apply each event to the aggregate, this will cause it to update its internal model
                // in preparation to process a command
                foreach(var e in deserialized)
                    aggregate.ApplyEvent((Event)e);
                
            } while (!slice.IsEndOfStream);

            return aggregate;
        }
       
        
        // Returns the latest snapshot for the supplied resource ID if it can find one in Event Store.
        // Otherwise returns null.
        public Resource.Snapshot LoadSnapshot(Guid resourceId)
        {
            if (!_enableSnapshots)
                return null;
            // look for a snapshot in the snapshots-resourceId stream
            // (read one event backwards, we only care about the latest snapshot)
            var slice =
                _connection.ReadStreamEventsBackwardAsync($"resourceSnapshots-{resourceId}", StreamPosition.End, 1,
                    false).Result;

            if (slice.Status != SliceReadStatus.Success)
                return null;

            var checkpointData = Encoding.UTF8.GetString(slice.Events[0].Event.Data);
            return JsonConvert.DeserializeObject<Resource.Snapshot>(checkpointData);
        }
    }
}