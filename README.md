EventCannon
===========

A .net library that creates events at a predetermined rate in Hz.

It will try to `Thread.Sleep()` in-between events, but as Sleep's accuracy is 15.6ms (or e.g. 1ms, when changed via `timeBeginPeriod(1)`, which the cannon does not do itself, so you hvae to, if you want that), the fine-tuning is always done using `Thread.SpinWait()`. This means that for rates higher than roughly 1 / 2 * sleepAccuracy, Sleep() won't be used at all and **one core of your machine will be pegged at 100% continuously. You have been warned!**

The cannon will adapt itself to the actual conditions on the system, so expect the initial rate to be way off the target, but it will quickly adapt.

One furter note of warning: **If you want to do any substantial work in the callback, you better ship it off to some thread pool!** The cannon has only one thread (which is enough to make one core spin), and invokes the callback on that thread. This implies that **the callback is always invoked on the same thread**, which makes some multi-threading issues easier. However, if you block on that thread, the next event will only come after your callback returned. As the cannon adapts itself, it will try to make up for the lost events, but only for the last 250ms or so (this depends on the rate and is subject to changes as EventCannon is improved)

The cannon **also runs on Mono**.

If you want to use it in a production system, make sure you test it beforehand. I didn't! This library is brandnew, and not very well tested.

If you have any issues with it, let me know via GitHub's issues.

# Example Usage
    
## Hello World
```C#
    Action<int> action = currentRate => Console.WriteLine(currentRate); // print current actual rate
    using (var cannon = new Cannon(action))
	{
        cannon.SetEventsPerSecond(3); // Only 3Hz, we are writing to the console every single time!
        Thread.Sleep(5000);
        cannon.SetEventsPerSecond(0);
    }
```

## Simple accuracy check
```C#
    long n = 200000;
    long totalEvents = 0;
    int targetRate = 17000;
    using (var cannon = new Cannon(currentRate => Interlocked.Increment(ref totalEvents)))
    {
        Stopwatch sw = Stopwatch.StartNew();
        cannon.SetEventsPerSecond(targetRate);
        while (Interlocked.Read(ref totalEvents) < n)
            Thread.Sleep(0);
        cannon.SetEventsPerSecond(0);
        var elapsed = sw.Elapsed;
        var actualRate = totalEvents / elapsed.TotalSeconds;
        Console.WriteLine("{0} events took {1}ms", totalEvents, elapsed.TotalMilliseconds);
        Console.WriteLine("Average actual rate: {0}Hz. (Actual-Target)/Target: {1}",
              actualRate, (actualRate - targetRate) / targetRate);
    }
    Console.ReadKey();
```

# Accuracy

A simple and not at all rigorous test, so take these as ballpark figures. I let the cannon fire for some 10 seconds each.

To sum up the Windows figures: The best result was achieved at **5kHz**, the cannon was only **0.2% too fast on average**, the worst was the highest tested rate of **500kHz**, where the cannon just couldn't keep up and was **able to maintain roughly half the target rate**.

## i5-2400 3.1GHz, 4core, Windows 7, 64bit VMWare guest, .net 4

| Target |      Mean |         Diff/Target | Min | Median |    Max |
|-------:|----------:|--------------------:|----:|-------:|-------:|
|   1000 |       950 |  -0.049607377051097 | 916 |   1001 |   3225 |
|   5000 |      5010 | 0.00193113936439131 |   0 |   5011 |   5494 |
|  10000 |     10054 | 0.00544182148805485 |   0 |  10047 |  11680 |
|  50000 |     51831 |  0.0366185840517688 |   0 |  51774 |  56207 |
| 100000 |    114956 |   0.149564571846537 |   0 | 117383 | 131094 |
| 500000 |    266223 |  -0.467553502536521 |   0 | 269173 | 284942 |

## i7-3667U, 2core, Linux 3.2.0-4-rt-amd64Windows 7, libmono 2.0

| Target |      Mean |          Diff/Target | Min | Median |    Max |
|-------:|----------:|---------------------:|----:|-------:|-------:|
|   1000 |       997 | -0.00306562307876834 |   1 |   1001 |   2522 |
|   5000 |      5025 |  0.00508689735438056 |   0 |   5015 |   6274 |
|  10000 |     10097 |  0.00974095840787559 |   0 |  10077 |  13027 |
|  50000 |     51318 |   0.0263592605804703 |   0 |  51846 |  51953 |
| 100000 |    105694 |   0.0569395918055285 |   0 | 106385 | 133704 |
| 500000 |    574093 |    0.148185628158968 |   0 | 477315 | 790861 |
