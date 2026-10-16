using System;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Phase3;
using OrderProcessing.Phase4;

namespace OrderProcessing;

internal class Program
{
    static async Task Main(string[] args)
    {
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // Parse --cancel-after <ms>
        int cancelAfterMs = 0;
        var argList = new System.Collections.Generic.List<string>(args);
        int cancelIdx = argList.IndexOf("--cancel-after");
        if (cancelIdx >= 0 && cancelIdx + 1 < argList.Count &&
            int.TryParse(argList[cancelIdx + 1], out int ms))
        {
            cancelAfterMs = ms;
            argList.RemoveRange(cancelIdx, 2);
        }

        if (cancelAfterMs > 0)
        {
            cts.CancelAfter(cancelAfterMs);
            Console.WriteLine($"[Program] Run will auto-cancel after {cancelAfterMs} ms.");
        }

        string phase = argList.Count > 0 ? argList[0].ToLowerInvariant() : string.Empty;

        if (string.IsNullOrEmpty(phase))
        {
            phase = ShowMenu();
        }

        try
        {
            switch (phase)
            {
                case "phase1":
                    await new Phase1Runner().RunAsync(cts.Token);
                    break;
                case "phase2":
                    await new Phase2Runner().RunAsync(cts.Token);
                    break;
                case "phase3":
                    await new Phase3Runner().RunAsync(cts.Token);
                    break;
                case "phase4":
                    await new Phase4Runner().RunAsync(cts.Token);
                    break;
                case "all":
                    Console.WriteLine("=== Phase 1 ===");
                    await new Phase1Runner().RunAsync(cts.Token);
                    Console.WriteLine("\n=== Phase 2 ===");
                    await new Phase2Runner().RunAsync(cts.Token);
                    Console.WriteLine("\n=== Phase 3 ===");
                    await new Phase3Runner().RunAsync(cts.Token);
                    Console.WriteLine("\n=== Phase 4 ===");
                    await new Phase4Runner().RunAsync(cts.Token);
                    break;
                default:
                    Console.WriteLine($"Unknown phase: '{phase}'. Use phase1, phase2, phase3, phase4, or all.");
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Run cancelled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
        }
    }

    private static string ShowMenu()
    {
        Console.WriteLine("Order Processing System");
        Console.WriteLine("=======================");
        Console.WriteLine("1. Phase 1 — Async Data Access Layer");
        Console.WriteLine("2. Phase 2 — Concurrent Processing");
        Console.WriteLine("3. Phase 3 — Full Checkout Workflow  [coming soon]");
        Console.WriteLine("4. Phase 4 — Scale-Up               [coming soon]");
        Console.WriteLine("5. All Phases");
        Console.Write("\nSelect option (1-5): ");

        string? input = Console.ReadLine()?.Trim();

        return input switch
        {
            "1" => "phase1",
            "2" => "phase2",
            "3" => "phase3",
            "4" => "phase4",
            "5" => "all",
            _   => "phase2"
        };
    }
}