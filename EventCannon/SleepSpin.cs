using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace EventCannon
{
    public class SleepSpin
    {
        private static readonly double SpinsPerMicroRoughly;
        private static readonly int SleepMillisResolution;
        private static readonly double SwTicksPerMicrosecond = Stopwatch.Frequency / 1000d / 1000d;
        private static readonly double SwTicksPerMillisecond = SwTicksPerMicrosecond * 1000d;

        static SleepSpin()
        {
            const int count = 250000;
            const int spins = 100;

			SpinsPerMicroRoughly = spins / GetMedianMicrosPerSpinWait(count, spins);

            var sleepOneTimes = new List<int>();
            for (int i = 0; i < 100; i++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                Thread.Sleep(1);
                sleepOneTimes.Add((int)Math.Ceiling(sw.ElapsedTicks / SwTicksPerMillisecond));
            }
            SleepMillisResolution = Median(sleepOneTimes);
        }

		// The value in spinTimeDivisor is a tradeoff between accuracy (in the face of
		// frequency scaling and who knows what other effects) and giving Thread.SpinWait
		// as many iterations at a time (rather than calling it many times with only few
		// iterations each), because my uneducated guess is that, at least on hyper-threaded
		// systems, SpinWait would be more efficient that way. If anyone know better, let me
		// know.

		// TODO: 20 is just an kind of arbitrary choice
		// On my system here, a value of 2 or less would decrease accuracy,
		// but taking cpu frequency scaling into account, I figured to go 20
		private const double spinTimeDivisor = 20;

        public static long usleep(int micros)
        {
            Stopwatch sw = Stopwatch.StartNew();
            int millis = micros / 1000;
            if (millis >= SleepMillisResolution * 2)
                Thread.Sleep(millis - (SleepMillisResolution * 2));

            long microsToGo;
			long spun = 0;
            while ((microsToGo = micros - (long)(sw.ElapsedTicks / SwTicksPerMicrosecond)) > 0)
			{
				int spin = (int) (SpinsPerMicroRoughly * microsToGo / spinTimeDivisor);
				spun += spin;
                Thread.SpinWait(spin);
			}
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
                Stopwatch sw = Stopwatch.StartNew();
                Thread.SpinWait(spins);
                long elapsedSwTicks = sw.ElapsedTicks;
                elapsedTicks.Add(elapsedSwTicks);
            }
            return Median(elapsedTicks) / SwTicksPerMicrosecond;
        }
    }
}
