using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OrderProcessing.Phase4;

public class FraudScoringService
{
    public static int Score(int orderId)
    {
        long h = orderId;
        for (int i = 0; i < 100_000; i++)
            h = (h * 31 + i) % 1_000_000_007;
        return (int)(h % 100);
    }

    public void RunScoringComparison(CancellationToken ct)
    {
        Console.WriteLine("\n===== Task 10.4 A: CPU-Bound Fraud Scoring (10,000 Orders) =====");
        int totalOrders = 10_000;

        // R1: Sequential Loop
        var seqSw = Stopwatch.StartNew();
        var sequentialFlagged = new List<int>();
        for (int id = 1; id <= totalOrders; id++)
        {
            if (Score(id) >= 90)
            {
                sequentialFlagged.Add(id);
            }
        }
        seqSw.Stop();

        // R2: Parallel Loop across all cores
        var parSw = Stopwatch.StartNew();
        var parallelFlagged = new ConcurrentBag<int>();
        var threadIds = new ConcurrentDictionary<int, byte>();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = ct
        };

        Parallel.For(1, totalOrders + 1, options, id =>
        {
            threadIds.TryAdd(Environment.CurrentManagedThreadId, 0);

            if (Score(id) >= 90)
            {
                parallelFlagged.Add(id);
            }
        });

        parSw.Stop();

        var sortedParallel = parallelFlagged.OrderBy(x => x).ToList();

        // R3: Validate exact match
        bool countMatches = sequentialFlagged.Count == sortedParallel.Count;
        bool listsMatch = sequentialFlagged.SequenceEqual(sortedParallel);

        double speedup = (double)seqSw.ElapsedMilliseconds / Math.Max(parSw.ElapsedMilliseconds, 1);

        Console.WriteLine($"Flagged count: {sortedParallel.Count} (Sequential: {sequentialFlagged.Count}, Expected: 1005)");
        Console.WriteLine($"First 5 IDs:   {string.Join(", ", sortedParallel.Take(5))} (Expected: 4, 8, 12, 29, 33)");
        Console.WriteLine($"Sequential time: {seqSw.ElapsedMilliseconds} ms");
        Console.WriteLine($"Parallel time:   {parSw.ElapsedMilliseconds} ms");
        Console.WriteLine($"Speed-up factor: {speedup:F2}x");
        Console.WriteLine($"Distinct threads used: {threadIds.Count}");
        Console.WriteLine($"Environment.ProcessorCount: {Environment.ProcessorCount}");
        Console.WriteLine($"Results match exactly: {countMatches && listsMatch}");
        Console.WriteLine("=================================================================\n");
    }
}
