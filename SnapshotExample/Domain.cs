using System;
using System.Collections.Generic;
using System.Linq;

namespace SnapshotExample
{
    public class Resource : SnapshottableAggregate<Resource.Snapshot>
    {
        // Only the bare minimum state to handle our business constraints
        private Guid _id;
        private Dictionary<string, List<int>> _bookings = new Dictionary<string, List<int>>();

        public void Create(Guid id, string name)
        {
            if(string.IsNullOrEmpty(name))
                throw new InvalidOperationException("Cannot create a resource without a name");
            
            RaiseEvent(new ResourceCreated(id, name));
        }

        public void Book(DateTime bookingDate, int timeSlot, Guid activityId)
        {
            // Rubbish made up business logic
            if(bookingDate < DateTime.Today)
                throw new InvalidOperationException("Cannot book a resource for a day in the past");
            
            List<int> dayTimeslots = null;
            if(_bookings.TryGetValue(bookingDate.ToShortDateString(), out dayTimeslots)
               && dayTimeslots.Contains(timeSlot))
                throw new InvalidOperationException($"Resource is already booked for slot {timeSlot} on {bookingDate}");
            
            
            // Otherwise ok to book
            RaiseEvent(new ResourceBooked(_id, bookingDate, timeSlot, activityId));
        }

        public void Apply(ResourceCreated e)
        {
            _id = e.ResourceId;
        }

        public void Apply(ResourceBooked e)
        {   
            var key = e.BookingDate.ToShortDateString();
            if (!_bookings.ContainsKey(key))
                _bookings.Add(key, new List<int>());
            
            _bookings[key].Add(e.TimeSlot);
        }
        
        public override void ApplySnapshot(Snapshot snap)
        {
            _id = snap.ResourceId;
            _bookings = snap.Bookings;
            Version = snap.Version;
        }

        public override Snapshot ProduceSnapshot()
        {
            // In this demo, these snapshots are going to get pretty large, which will slow things down slightly
            // In reality you probably never want to book things in the past, so we would be able to purge some of
            // the _bookings state. To simulate this, we will save only a random 365 days worth.
            var purgedBookings = _bookings.Take(365).ToDictionary(b => b.Key, b => b.Value);
            
            return new Snapshot(_id, Version, purgedBookings);
        }

        public class Snapshot
        {
            public readonly Guid ResourceId;
            public readonly long Version;
            public readonly Dictionary<string, List<int>> Bookings;

            public Snapshot(Guid resourceId, long version, Dictionary<string, List<int>> bookings)
            {
                ResourceId = resourceId;
                Version = version;
                Bookings = bookings;
            }
        }
    }
}