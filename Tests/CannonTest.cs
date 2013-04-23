using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using EventCannon;

namespace Tests
{

    public class CannonTest
    {

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        public static extern uint TimeEndPeriod(uint uMilliseconds);

        public static void MyClassCleanup()
        {
            TimeEndPeriod(1);
        }

        public static void Main(string[] args)
        {
            if (TimeBeginPeriod(1) != 0)
                throw new Exception("Call to TimeBeginPeriod(1) not successful!");

            try
            {
                int lastCurrentRate = 0;
                const int checkRateCount = 10;
                var actualRates = new List<int>(checkRateCount);
                Cannon cannon = null;
                using (cannon = new Cannon(currentRate =>
                    {
                        if (lastCurrentRate != currentRate)
                        {
                            //Console.WriteLine(currentRate);
                            lastCurrentRate = currentRate;
                            actualRates.Add(currentRate);
                            if (actualRates.Count == checkRateCount)
                                // ReSharper disable PossibleNullReferenceException
                                // ReSharper disable AccessToModifiedClosure
                                cannon.SetEventsPerSecond(0);
                            // ReSharper restore AccessToModifiedClosure
                            // ReSharper restore PossibleNullReferenceException
                        }
                    }))
                {
                    Console.WriteLine("Target Min Mean Median Max");
                    for (var i = 0; i < 7; i++)
                    {
                        for (var j = 1; j < 2; j += 4)
                        {
                            int rate = (int) Math.Pow(10, i)*j;
                            cannon.SetEventsPerSecond(rate);
                            while (cannon.GetEventsPerSecond() != 0)
                                Thread.Sleep(50);
                            var m = GetMinMeanMedianMax(actualRates);
                            Console.WriteLine(rate + " " + m.Item1 + " " + m.Item2 + " " + m.Item3 + " " + m.Item4);
                            actualRates.Clear();
                        }
                    }
                }
            }
            finally
            {
                TimeEndPeriod(1);
            }
        }

        private static Tuple<int, double, int, int> GetMinMeanMedianMax(IEnumerable<int> values)
        {
            double sum = 0;
            int min = Int32.MaxValue;
            int max = Int32.MinValue;
            // ReSharper disable SuspiciousTypeConversion.Global
            var list = new List<int>(values is ICollection ? ((ICollection)values).Count : 100);
            // ReSharper restore SuspiciousTypeConversion.Global
            foreach (var value in values)
            {
                if (value < min)
                    min = value;
                if (value > max)
                    max = value;
                sum += value;
                list.Add(value);
            }
            list.Sort();
            return Tuple.Create(min, sum / list.Count, list[list.Count / 2], max);
        }
    }

}
