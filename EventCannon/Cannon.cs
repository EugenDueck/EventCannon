using System;

namespace EventCannon
{
    public class Cannon : IDisposable
    {
        private readonly FrequencyAdjustingCannon _cannonDelegate;
        private readonly Action<int, long> _eventAction;

        public Cannon(Action<int, long> eventAction)
        {
            _eventAction = eventAction;
            _cannonDelegate = new FrequencyAdjustingCannon(EventFunc);
        }

        public Cannon(Action<int> eventAction)
            : this((rate, spun) => eventAction(rate))
        {
        }

        private float EventFunc(float f, long l)
        {
            _eventAction((int)f, l);
            return 1f;
        }

        public int EventsPerSecond
        {
            get { return (int) _cannonDelegate.PerSecondTarget; }
            set { _cannonDelegate.PerSecondTarget = value; }
        }

        public int GetLastRatePerSecond()
        {
            return (int) _cannonDelegate.GetLastRatePerSecond();
        }

        public void Dispose()
        {
            _cannonDelegate.Dispose();
        }
    }
}
