using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace EventCannon
{
    public static class SleepSpin
    {
        private static readonly double SpinsPerSwTickRoughly;
        private static readonly int SleepMillisResolution;
        private static readonly double SwTicksPerMicrosecond = Stopwatch.Frequency / 1000d / 1000d;
        private static readonly double SwTicksPerMillisecond = SwTicksPerMicrosecond * 1000d;

        static SleepSpin()
        {
            const int count = 250000;
            const int spins = 100;

			SpinsPerSwTickRoughly = spins / GetMedianSwTicksPerSpinWait(count, spins);

            var sleepOneTimes = new List<int>();
            for (int i = 0; i < 100; i++)
            {
                var ticksBefore = Stopwatch.GetTimestamp();
                Thread.Sleep(1);
                sleepOneTimes.Add((int)Math.Ceiling((Stopwatch.GetTimestamp() - ticksBefore) / SwTicksPerMillisecond));
            }
            SleepMillisResolution = Median(sleepOneTimes);
        }

        public static bool WaitOneMicros(this WaitHandle waitHandle, long micros)
        {
            bool waitHandleSignaled;
            usleep(micros, out waitHandleSignaled, waitHandle);
            return waitHandleSignaled;
        }
        
        // The value in spinTimeDivisor is a tradeoff between accuracy (in the face of
		// frequency scaling and who knows what other effects) and giving Thread.SpinWait
		// as many iterations at a time (rather than calling it many times with only few
		// iterations each), because my uneducated guess is that, at least on hyper-threaded
		// systems, SpinWait would be more efficient that way. If anyone know better, let me
		// know.

		// TODO: 20 was chosen somewhat arbitrarily
		// On my system here, a value of 2 or less would decrease accuracy,
		// but taking cpu frequency scaling into account, I figured to go 20
		private const double SpinTimeDivisor = 20;

        private static long StopwatchTicksToMicroseconds(long stopwatchTicks)
        {
            return stopwatchTicks * 1000000 / Stopwatch.Frequency;
        }

        private static long MicrosecondsToStopwatchTicks(long microseconds)
        {
            return microseconds * Stopwatch.Frequency / 1000000;
        }

        public static long usleep(long micros)
        {
            bool waitHandleSignaled;
            return usleep(micros, out waitHandleSignaled, null);
        }

        public static long usleep(long micros, out bool waitHandleSignaled, WaitHandle waitHandle)
        {
            return usleepSW(MicrosecondsToStopwatchTicks(micros), out waitHandleSignaled, waitHandle);
        }

        public static long usleepSW(long stopwatchTicks)
        {
            bool waitHandleSignaled;
            return usleepSW(stopwatchTicks, out waitHandleSignaled, null);
        }

        public static long usleepSW(long stopwatchTicks, out bool waitHandleSignaled, WaitHandle waitHandle)
        {
            long spun = 0;
            if (stopwatchTicks > 0)
            {
                var endTimestamp = Stopwatch.GetTimestamp() + stopwatchTicks;
                int millis = (int) Math.Min(int.MaxValue, StopwatchTicksToMicroseconds(stopwatchTicks)/1000);
                if (millis >= SleepMillisResolution*2)
                {
                    if (waitHandle != null && waitHandle.WaitOne(millis - (SleepMillisResolution * 2)))
                    {
                        waitHandleSignaled = true;
                        return 0;
                    }
                }

                long ticksToGo;
                while ((ticksToGo = endTimestamp - Stopwatch.GetTimestamp()) > 0)
                {
                    int spin = (int) (SpinsPerSwTickRoughly*ticksToGo/SpinTimeDivisor);
                    spun += spin;
                    Thread.SpinWait(spin);
                }
            }
            waitHandleSignaled = waitHandle != null && waitHandle.WaitOne(0);
			return spun;
        }

        private static T Median<T>(IEnumerable<T> collection)
        {
            var copy = new List<T>(collection);
            copy.Sort();
            return copy[copy.Count / 2];
        }

        private static double GetMedianSwTicksPerSpinWait(int count, int spins)
        {
            var elapsedTicks = new List<long>(count);
            for (int i = 1; i <= count; i++)
            {
                var ticksBefore = Stopwatch.GetTimestamp();
                Thread.SpinWait(spins);
                elapsedTicks.Add(Stopwatch.GetTimestamp() - ticksBefore);
            }
            return Median(elapsedTicks);
        }
    }
}
