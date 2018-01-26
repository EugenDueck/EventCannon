using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace EventCannon
{
    public static class SleepSpin
    {
        private static readonly double SpinsPerMicroRoughly;
        private static readonly int SleepMillisResolution;
        private static readonly double SwTicksPerMicrosecond = Stopwatch.Frequency / 1000d / 1000d;
        private static readonly double SwTicksPerMillisecond = SwTicksPerMicrosecond * 1000d;

        private static long ElapsedTicks(long startTicks)
        {
            return Stopwatch.GetTimestamp() - startTicks;
        }

        static SleepSpin()
        {
            const int count = 250000;
            const int spins = 100;

			SpinsPerMicroRoughly = spins / GetMedianMicrosPerSpinWait(count, spins);

            var sleepOneTimes = new List<int>();
            for (int i = 0; i < 100; i++)
            {
                var ticksBefore = Stopwatch.GetTimestamp();
                Thread.Sleep(1);
                sleepOneTimes.Add((int)Math.Ceiling(ElapsedTicks(ticksBefore) / SwTicksPerMillisecond));
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
		private const double spinTimeDivisor = 20;

        private static readonly WaitHandle DummyWaitHandle = new AutoResetEvent(false);

        public static long usleep(long micros)
        {
            bool waitHandleSignaled;
            return usleep(micros, out waitHandleSignaled, DummyWaitHandle);
        }


        public static long usleep(long micros, out bool waitHandleSignaled, WaitHandle waitHandle)
        {
            long spun = 0;
            if (micros > 0)
            {
                var ticksAtStart = Stopwatch.GetTimestamp();
                int millis = (int) Math.Min(int.MaxValue, micros/1000);
                if (millis >= SleepMillisResolution*2)
                {
                    if (waitHandle.WaitOne(millis - (SleepMillisResolution*2)))
                    {
                        waitHandleSignaled = true;
                        return 0;
                    }
                }

                long microsToGo;
                while ((microsToGo = micros - (long) (ElapsedTicks(ticksAtStart)/SwTicksPerMicrosecond)) > 0)
                {
                    int spin = (int) (SpinsPerMicroRoughly*microsToGo/spinTimeDivisor);
                    spun += spin;
                    Thread.SpinWait(spin);
                }
            }
            waitHandleSignaled = waitHandle.WaitOne(0);
			return spun;
        }

        private static T Median<T>(IEnumerable<T> collection)
        {
            var copy = new List<T>(collection);
            copy.Sort();
            return copy[copy.Count / 2];
        }

        private static double GetMedianMicrosPerSpinWait(int count, int spins)
        {
            var elapsedTicks = new List<long>(count);
            for (int i = 1; i <= count; i++)
            {
                var ticksBefore = Stopwatch.GetTimestamp();
                Thread.SpinWait(spins);
                elapsedTicks.Add(ElapsedTicks(ticksBefore));
            }
            return Median(elapsedTicks) / SwTicksPerMicrosecond;
        }
    }
}
