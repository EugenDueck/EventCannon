 using System;
using System.Diagnostics;
using System.Threading;

namespace EventCannon
{
    public class FrequencyAdjustingCannon : IDisposable
    {
        private const int CheckActualPerformanceMillis = 50;
        private const double TargetChangeRatio = 0.7;
        // TODO ? I'm using floats here, because they can be made volatile, in contrast to doubles, which I normally use for floating point math
        private readonly Thread _thread;
        private volatile bool _disposed;
        private volatile float _perSecondRate;
        private volatile float _lastRatePerSecond;
        private readonly Func<float, long, float> _eventFunc;

        static FrequencyAdjustingCannon()
        {
            // we do the following in order to run "static SleepSpin()", which has to be run once, before usleep
            // can be used. It takes 1.7 secs on my box to finish, and we don't want to have that happen on the
            // first call to usleep, that's why we do it here
            SleepSpin.usleepSW(0);
        }
        public FrequencyAdjustingCannon(Func<float, long, float> eventFunc)
        {
            _eventFunc = eventFunc;
            _thread = new Thread(Run) { IsBackground = true };
            Init();
        }

        public FrequencyAdjustingCannon(Func<float, float> eventFunc)
            : this((rate, spun) => eventFunc(rate))
        {
        }

        private void Init()
        {
            _thread.Start();
        }

        public float PerSecondTarget
        {
            get { return _perSecondRate; }
            set { _perSecondRate = value; }
        }

        public float GetLastRatePerSecond()
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
        private static double ToMicros(long swTicks)
        {
            return swTicks * 1000000d / Stopwatch.Frequency;
        }

        private void Run()
        {
            long checkActualPerformanceTicks = (long) (CheckActualPerformanceMillis / 1000d * Stopwatch.Frequency);
            float lastTarget = 0;
            long lastSpun = 0;
            long intervalStartTimestamp = 0;
            float rateInThisInterval = 0;
            int eventsInThisInterval = 0;
            const long sleepRemainderMultiplier = 1000000000;
            const long sleepRemainderMultiplier2 = 1000; // factors in that sleep(1 tick) normally sleeps mor than 1 tick (7 ticks here on my machine)
            long currentIntervalSleepRemainderCounter = 0; // this is important for high frequencies, when currentSleepTicksBetweenEvents goes towards 0
            long currentSleepTicksBetweenEventsRemainder = 0;
            double currentSleepTicksBetweenEventsDbl = Stopwatch.Frequency / 1000d; // initialize at 1 msec
            long currentSleepTicksBetweenEventsLong = (long) currentSleepTicksBetweenEventsDbl;
            long t2;
            
            while (!_disposed)
            {
                float perSecondTargetRate = _perSecondRate;
                if (perSecondTargetRate > 0)
                {
// ReSharper disable CompareOfFloatsByEqualityOperator
                    bool targetChanged = perSecondTargetRate != lastTarget;
// ReSharper restore CompareOfFloatsByEqualityOperator
                    if (targetChanged)
                    {
                        rateInThisInterval = 0;
                        eventsInThisInterval = 0;
                        currentIntervalSleepRemainderCounter = 0;
                        currentSleepTicksBetweenEventsRemainder = 0;
                        _lastRatePerSecond = 0;
                        lastTarget = perSecondTargetRate;
                        intervalStartTimestamp = Stopwatch.GetTimestamp();
                    }
                    float lastEventReturnValue = _eventFunc(_lastRatePerSecond, lastSpun);
                    t2 = Stopwatch.GetTimestamp();
                    rateInThisInterval += lastEventReturnValue;
                    eventsInThisInterval++;
                    var ticksInThisInterval = t2 - intervalStartTimestamp;
                    if (targetChanged || ticksInThisInterval >= checkActualPerformanceTicks)
                    {
                        double actualRate = rateInThisInterval / Math.Max(0.000001, ToMicros(ticksInThisInterval) / 1000000d);
                        _lastRatePerSecond = (int)actualRate;
                        double targetSleepTicksBetweenEvents = targetChanged
                            ? Math.Max(0.0000001, ((Stopwatch.Frequency / (perSecondTargetRate / (rateInThisInterval / eventsInThisInterval)))))
                            : currentSleepTicksBetweenEventsDbl * (actualRate / perSecondTargetRate);
                        if (targetSleepTicksBetweenEvents < 0)
                            targetSleepTicksBetweenEvents = 0;
                        double newCur = targetChanged
                            ? targetSleepTicksBetweenEvents
                            : (currentSleepTicksBetweenEventsDbl + (TargetChangeRatio * (targetSleepTicksBetweenEvents - currentSleepTicksBetweenEventsDbl)));
                        currentSleepTicksBetweenEventsDbl = newCur;
                        currentSleepTicksBetweenEventsLong = (long) currentSleepTicksBetweenEventsDbl;
                        currentSleepTicksBetweenEventsRemainder = (long)((currentSleepTicksBetweenEventsDbl - currentSleepTicksBetweenEventsLong) * sleepRemainderMultiplier);
                        rateInThisInterval = 0;
                        eventsInThisInterval = 0;
                        currentIntervalSleepRemainderCounter = 0;
                        intervalStartTimestamp = t2;
                    }
                    long addRemainderTicks = 0;
                    if ((currentIntervalSleepRemainderCounter += currentSleepTicksBetweenEventsRemainder) > (sleepRemainderMultiplier * sleepRemainderMultiplier2))
                    {
                        addRemainderTicks = sleepRemainderMultiplier2;
                        currentIntervalSleepRemainderCounter -= sleepRemainderMultiplier * sleepRemainderMultiplier2;
                    }
                    lastSpun = currentSleepTicksBetweenEventsLong + addRemainderTicks > 0 ? SleepSpin.usleepSW(currentSleepTicksBetweenEventsLong + addRemainderTicks) : 0;
                }
                else
                {
                    lastTarget = perSecondTargetRate;
                    Thread.Sleep(1);
                }
            }
        }
    }
}
