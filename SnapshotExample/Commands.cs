using System;

namespace SnapshotExample
{
    public class CreateResource
    {
        public Guid ResourceId { get; }
        public string Name { get; }

        public CreateResource(Guid resourceId, string name)
        {
            ResourceId = resourceId;
            Name = name;
        }
    }

    public class BookResource
    {
        public Guid ResourceId { get; }
        public Guid ActivityId { get; }
        public DateTime Date { get; }
        public int TimeSlot { get; }

        public BookResource(Guid resourceId, Guid activityId, DateTime date, int timeSlot)
        {
            ResourceId = resourceId;
            ActivityId = activityId;
            Date = date;
            TimeSlot = timeSlot;
        }
    }
}