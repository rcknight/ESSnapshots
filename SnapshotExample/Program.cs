using System;
using System.Diagnostics;
using System.Net;
using EventStore.ClientAPI;

namespace SnapshotExample
{
    internal class Program
    {
        private const int NumberOfCommands = 50000;
        private const bool DoSnapshots = true;
        
        public static void Main(string[] args)
        {
            /*var conn = EventStoreConnection.Create(ConnectionSettings.Default, ClusterSettings.Create()
                .DiscoverClusterViaGossipSeeds()
                .SetGossipSeedEndPoints(
                    new IPEndPoint(IPAddress.Loopback, 1113),
                    new IPEndPoint(IPAddress.Loopback, 2113),
                    new IPEndPoint(IPAddress.Loopback, 3113)
                ));*/
            var conn = EventStoreConnection.Create(ConnectionSettings.Default,
                new IPEndPoint(IPAddress.Loopback, 4111));

            conn.ConnectAsync().Wait();
            
            var commandHandler = new ResourceCommandHandler(conn, DoSnapshots);

            var resourceId = Guid.NewGuid();
            
            commandHandler.Handle(new CreateResource(resourceId, "Meeting Room"));

            var day = DateTime.Today;
            for (int i = 0; i < NumberOfCommands; i++)
            {
                if (i % 24 == 0)
                    day = day.AddDays(1);
                
                
                var command = new BookResource(resourceId, Guid.NewGuid(), day, i % 24);
                commandHandler.Handle(command);
                Console.Write($"{i} - ");
            }
            
            // lets do a final command with snapshots disabled, to compare
            
            var noSnaphotHandler = new ResourceCommandHandler(conn, false);
            
            Console.WriteLine($"After {NumberOfCommands} events in stream, running without snapshots looks like:");
            noSnaphotHandler.Handle(new BookResource(resourceId, Guid.NewGuid(), day.AddDays(1), 0));

            Console.ReadLine();
        }
    }
}