using System.Collections.Generic;

namespace SnapshotExample
{   
    public abstract class SnapshottableAggregate<TSnapshot>
    {
        public long Version { get; protected set; } = -1;
        private readonly List<Event> _changes = new List<Event>();
       
        public abstract void ApplySnapshot(TSnapshot snap);
        public abstract TSnapshot ProduceSnapshot();
        
        protected void RaiseEvent(Event @event)
        {
            ApplyEvent(@event);
            _changes.Add(@event);
        }
        
        public void ApplyEvent(Event @event)
        {
            ((dynamic)this).Apply((dynamic)@event);
            Version++;
        }

        public ICollection<Event> GetUncommittedEvents()
        {
            return _changes;
        }
    }
}