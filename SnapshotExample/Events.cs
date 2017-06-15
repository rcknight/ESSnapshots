using System;

namespace SnapshotExample
{
    public interface Event {}
    
    public class ResourceCreated : Event
    {
        public Guid ResourceId { get; set; }
        public string Name { get; set; }
        
        public ResourceCreated(Guid resourceId, string name)
        {
            ResourceId = resourceId;
            Name = name;
        }
    }

    public class ResourceBooked : Event
    {
        public Guid ResourceId { get; }
        public DateTime BookingDate { get; }
        public int TimeSlot { get; }
        public Guid ActivityId { get; }
       
        public ResourceBooked(Guid resourceId, DateTime bookingDate, int timeSlot, Guid activityId)
        {
            ResourceId = resourceId;
            BookingDate = bookingDate;
            TimeSlot = timeSlot;
            ActivityId = activityId;
        }
    }
}