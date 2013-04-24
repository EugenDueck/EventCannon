using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using EventCannon;

namespace Tests
{

    public class CannonTest
    {

#if !__MonoCS__
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        public static extern uint TimeEndPeriod(uint uMilliseconds);
#endif

        public static void Main(string[] args)
        {
#if !__MonoCS__
            if (TimeBeginPeriod(1) != 0)
                throw new Exception("Call to TimeBeginPeriod(1) not successful!");

            try
            {
#endif
				int workerSpin = 10;
				Console.WriteLine ("WorkerSpin: " + workerSpin);
                int lastCurrentRate = 0;
                const int checkRateCount = 50;
				long totalEvents = 0;
				long totalSpun = 0;
                var actualRates = new List<int>(checkRateCount);
                Cannon cannon = null;
				Stopwatch sw = new Stopwatch();
				Action<int, long> action = (currentRate, spun) =>
                    {
						Interlocked.Increment (ref totalEvents);
						Interlocked.Add(ref totalSpun, spun);
                        if (lastCurrentRate != currentRate)
                        {
                            //Console.WriteLine(currentRate);
                            lastCurrentRate = currentRate;
							lock (sw)
							{
	                            actualRates.Add(currentRate);
	                            if (actualRates.Count == checkRateCount)
								{
									sw.Stop();
	                                // ReSharper disable PossibleNullReferenceException
	                                // ReSharper disable AccessToModifiedClosure
	                                cannon.SetEventsPerSecond(0);
	                            	// ReSharper restore AccessToModifiedClosure
	                            	// ReSharper restore PossibleNullReferenceException
								}
							}
                        }
						if (workerSpin > 0)
							ThreadPool.QueueUserWorkItem(state => Thread.SpinWait (workerSpin));
					};

                using (cannon = new Cannon(action))
                {
                    Console.WriteLine("Target TotalMean Diff/Target Min Median Max Spun");
                    for (var i = 3; i < 6; i++)
                    {
                        for (var j = 1; j < 6; j += 4)
                        {
                            int rate = (int) Math.Pow(10, i)*j;
							Interlocked.Exchange(ref totalEvents, 0);
							lock (sw)
								sw.Restart();
                            cannon.SetEventsPerSecond(rate);
                            while (cannon.GetEventsPerSecond() != 0)
                                Thread.Sleep(50);
                            
							long localTotalEvents = Interlocked.Read(ref totalEvents);
							long localTotalSpun = Interlocked.Read(ref totalSpun);
							lock (sw)
							{
								var m = GetMinMeanMedianMax(actualRates);
								var mean = localTotalEvents / sw.Elapsed.TotalSeconds; // we'll ignore the mean in m.Item2 as it's not weighted proportional to actually elapsed time
//								Console.WriteLine ("total events / elapsed seconds " + localTotalEvents + " / " + sw.Elapsed.TotalSeconds);
                            	Console.WriteLine(rate + " " + 
								                  (int) Math.Round(mean) + " " + 
								                  ((mean - rate) / rate) + " " + m.Item1 + " " + m.Item3 + " " + m.Item4 + " " +
								                  localTotalSpun.ToString("E"));
	                            actualRates.Clear();
							}
                        }
                    }
                }
#if !__MonoCS__
            }
            finally
            {
                TimeEndPeriod(1);
            }
#endif
			Console.ReadKey();
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
