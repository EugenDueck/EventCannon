EventCannon
===========

A .net library that creates events at a predetermined rate in Hz (`EventCannon`) or any other unit like e.g. Mbps (`FrequencyAdjustingCannon`).

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

## i5-2400 3.1GHz, 4core, Windows 8.1 Enterprise, 64bit VMWare guest, .Net 4.6.1

| Target |      Mean |           Diff/Target | Median |
|-------:|----------:|:----------------------|-------:|
|   1000 |      1000 |  1.39179747870912E-05 |    999 |
|   5000 |      5000 |  2.86149763209323E-05 |   5000 |
|  10000 |      9991 | -0.000858937882804094 |  10005 |
|  50000 |     50025 |  0.000496465401208843 |  50018 |
| 100000 |     99931 | -0.000691051643280225 | 100085 |
| 500000 |    499597 | -0.000805650404079235 | 500220 |

## i5-2400 3.1GHz, 4core, Linux 4.14.0-2-amd64 #1 SMP Debian 4.14.7-1 (2017-12-22) x86_64 GNU/Linux, Mono 4.6.2.0

| Target |      Mean |           Diff/Target | Median |
|-------:|----------:|:----------------------|-------:|
|   1000 |      1000 |  0.000157407489267484 |   1000 |
|   5000 |      4998 | -0.000308743535296708 |   4999 |
|  10000 |      9996 | -0.000388310694691609 |   9999 |
|  50000 |     49966 | -0.000675579762706184 |  49993 |
| 100000 |     99948 | -0.000523830278989626 | 100068 |
| 500000 |    499235 |   -0.0015292623534248 | 500367 |
