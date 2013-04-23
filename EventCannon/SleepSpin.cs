using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace EventCannon
{
    public class SleepSpin
    {
        private static readonly double SpinsPerMicroRoughly;
        private static readonly int SleepMillisPrecision;
        private static readonly double SwTicksPerMicrosecond = Stopwatch.Frequency / 1000d / 1000d;
        private static readonly double SwTicksPerMillisecond = SwTicksPerMicrosecond * 1000d;


        static SleepSpin()
        {
            const int count = 250000;
            const int spins = 100; // a SpinWait(100) takes propably longer per spin than SpinWait(1000)
            // warm up
            GetAvgMicrosPerSpinWait(count, spins);
            // time
            SpinsPerMicroRoughly = spins / GetAvgMicrosPerSpinWait(count, spins);

            var sleepOneTimes = new List<int>();
            for (int i = 0; i < 100; i++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                Thread.Sleep(1);
                sleepOneTimes.Add((int)Math.Ceiling(sw.ElapsedTicks / SwTicksPerMillisecond));
            }
            SleepMillisPrecision = Median(sleepOneTimes);
        }

        public static void Sleep(int micros)
        {
            Stopwatch sw = Stopwatch.StartNew();
            int millis = micros / 1000;
            if (millis >= SleepMillisPrecision * 2)
                Thread.Sleep(millis - (SleepMillisPrecision * 2));

            long microsToGo;
            while ((microsToGo = micros - (long)(sw.ElapsedTicks / SwTicksPerMicrosecond)) > 0)
                Thread.SpinWait((int)(SpinsPerMicroRoughly * microsToGo));
        }

        private static T Median<T>(IEnumerable<T> collection)
        {
            var copy = new List<T>(collection);
            copy.Sort();
            return copy[copy.Count / 2];
        }

        private static double Avg(IEnumerable<long> collection)
        {
            int count = 0;
            long sum = 0;
            foreach (long l in collection)
            {
                sum += l;
                count++;
            }
            return (double)sum / count;
        }

        private static double SwToUs(double ticks)
        {
            return ticks / (Stopwatch.Frequency / 1000d / 1000d);
        }

        private static double GetAvgMicrosPerSpinWait(int count, int spins)
        {
            var elapsedTicks = new List<long>(count);
            for (int i = 1; i <= count; i++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                Thread.SpinWait(spins);
                long elapsedSwTicks = sw.ElapsedTicks;
                elapsedTicks.Add(elapsedSwTicks);
            }
            return SwToUs(Avg(elapsedTicks));
        }
    }
}
