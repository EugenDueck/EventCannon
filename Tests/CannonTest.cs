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

        private static void SleepTest()
        {
            Console.WriteLine("Target Mean Diff/Target Min Median Max");
            const int repetitions = 25;
            foreach (int micros in new[] { 0, 1, 5, 10, 50, 100, 500, 1000, 5000, 10000, 50000, 100000 })
            {
                var measurements = new List<double>(repetitions);
                for (int repetition = 0; repetition < repetitions; repetition++)
                {
                    var sw = Stopwatch.StartNew();
                    SleepSpin.usleep(micros);
                    sw.Stop();
                    measurements.Add(sw.Elapsed.TotalMilliseconds * 1000);
                }
                var m = GetMinMeanMedianMax(measurements);
                Console.WriteLine(micros + " " + m.Item2 + " " + ((m.Item2 - micros) / micros) + " " + m.Item1 + " " + m.Item3 + " " + m.Item4 + " ");
            }
        }
        public static void Main(string[] args)
        {

#if !__MonoCS__
            if (false && TimeBeginPeriod(1) != 0)
                throw new Exception("Call to TimeBeginPeriod(1) not successful!");

            Console.WriteLine("------------");
            SleepTest();
            Console.WriteLine("------------");
            SleepTest();
            return;
            try
            {
#endif
				int workerSpin = 0;
		        double SwTicksPerMicrosecond = Stopwatch.Frequency / 1000d / 1000d;
		        double SwTicksPerMillisecond = SwTicksPerMicrosecond * 1000d;
				var workerMicrosElapsed = new LinkedList<int>();
				Console.WriteLine ("WorkerSpin: " + workerSpin);
                int lastCurrentRate = 0;
				long totalEvents = 0;
				long totalSpun = 0;
				long millisTest = 2000;
                var actualRates = new List<int>(1000);
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
	                            if (sw.ElapsedMilliseconds >= millisTest)
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
							ThreadPool.QueueUserWorkItem(state => 
                            {
								Stopwatch sww = Stopwatch.StartNew();
								SleepSpin.usleep(workerSpin);
								var elapsedTicks = sww.ElapsedTicks;
//								Thread.SpinWait (workerSpin);
								lock(workerMicrosElapsed)
									workerMicrosElapsed.AddLast ((int) (elapsedTicks / SwTicksPerMicrosecond));
							});
					};

                using (cannon = new Cannon(action))
                {
                    Console.WriteLine("Target TotalMean Diff/Target Min Median Max Spun");
//                    Console.WriteLine("Min Mean Median Max");
					bool firstRound = true; // ignore first round
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
							double mean;
							Tuple<int, double, int, int> m;
							lock (sw)
							{
								m = GetMinMeanMedianMax(actualRates);
	                            actualRates.Clear();
								mean = localTotalEvents / sw.Elapsed.TotalSeconds; // we'll ignore the mean in m.Item2 as it's not weighted proportional to actually elapsed time
//								Console.WriteLine ("total events / elapsed seconds " + localTotalEvents + " / " + sw.Elapsed.TotalSeconds);

//								List<int> workerMicrosElapsedCopy;
//								lock(workerMicrosElapsed)
//								{
//									workerMicrosElapsedCopy = new List<int>(workerMicrosElapsed);
//									workerMicrosElapsed.Clear();
//								}
							}
//							var w = GetMinMeanMedianMax(workerMicrosElapsedCopy);
//                        	Console.WriteLine(w.Item1 + " " + (int) Math.Round(w.Item2) + " " + w.Item3 + " " + w.Item4);

							if (firstRound)
							{
								firstRound = false;
								j-= 4;
								continue;
							}
							else
							{
                            	Console.WriteLine(rate + " " + 
								                  (int) Math.Round(mean) + " " + 
								                  ((mean - rate) / rate) + " " + m.Item1 + " " + m.Item3 + " " + m.Item4 + " " +
								                  localTotalSpun.ToString("E"));
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

        private static Tuple<double, double, double, double> GetMinMeanMedianMax(IEnumerable<double> values)
        {
            double sum = 0;
            double min = Double.MaxValue;
            double max = Double.MinValue;
            // ReSharper disable SuspiciousTypeConversion.Global
            var list = new List<double>(values is ICollection ? ((ICollection)values).Count : 100);
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
