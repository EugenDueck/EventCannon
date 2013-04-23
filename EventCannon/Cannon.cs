using System;
using System.Diagnostics;
using System.Threading;

namespace EventCannon
{
    public class Cannon : IDisposable
    {
        private readonly Thread _thread;
        private volatile bool _disposed;
        private volatile int _eventsPerSecond;
        private volatile int _lastRatePerSecond;
        private readonly Action<int> _eventAction;

        public Cannon(Action<int> eventAction)
        {
            _eventAction = eventAction;
            _thread = new Thread(Run, 256 * 1024) {IsBackground = true};
            _thread.Start();
        }

        public void SetEventsPerSecond(int eventsPerSecond)
        {
            _eventsPerSecond = eventsPerSecond;
        }

        public int GetEventsPerSecond()
        {
            return _eventsPerSecond;
        }

        public int GetLastRatePerSecond()
        {
            return _lastRatePerSecond;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        private void Run()
        {
            int lastEventsPerSecTarget = 0;
            long eventsInThisInterval = 0;
            int currentSleepMicrosBetweenEvents = 0;
            int checkActualPerformanceMillis = 500;
            var sw = new Stopwatch();

            while (!_disposed)
            {
                int curEventsPerSecTarget = _eventsPerSecond;
                if (curEventsPerSecTarget > 0)
                {
                    if (curEventsPerSecTarget != lastEventsPerSecTarget)
                    {
                        currentSleepMicrosBetweenEvents = (int)(1000000d / curEventsPerSecTarget);
                        eventsInThisInterval = 0;
                        _lastRatePerSecond = 0;
                        lastEventsPerSecTarget = curEventsPerSecTarget;
                        checkActualPerformanceMillis = Math.Min(500, Math.Max(250, 100000 / curEventsPerSecTarget));
                        sw.Restart();
                    }
                    else
                    {
                        _eventAction(_lastRatePerSecond);
                        eventsInThisInterval++;
                        SleepSpin.Sleep(currentSleepMicrosBetweenEvents);
                        var elapsed = sw.Elapsed;
                        if (elapsed.TotalMilliseconds >= checkActualPerformanceMillis)
                        {
                            double actualRate = eventsInThisInterval / elapsed.TotalSeconds;
                            _lastRatePerSecond = (int)actualRate;
                            double newSleepMicrosBetweenEvents = Math.Max(1, ((double)currentSleepMicrosBetweenEvents / curEventsPerSecTarget) * actualRate);
                            currentSleepMicrosBetweenEvents = (int)
                                (0.33 * currentSleepMicrosBetweenEvents + 0.67 * newSleepMicrosBetweenEvents);
                            eventsInThisInterval = 0;
                            sw.Restart();
                        }
                    }
                }
                else
                {
                    lastEventsPerSecTarget = curEventsPerSecTarget;
                    Thread.Sleep(0);
                }
            }
        }

    }
}
