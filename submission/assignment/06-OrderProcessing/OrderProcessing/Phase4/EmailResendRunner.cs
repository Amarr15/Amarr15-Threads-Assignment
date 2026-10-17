using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Phase3;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

public class EmailResendRunner
{
    private readonly JsonFileStore _store;

    public EmailResendRunner(JsonFileStore? store = null)
    {
        _store = store ?? new JsonFileStore();
    }

    public async Task RunEmailResendAsync(CancellationToken ct)
    {
        Console.WriteLine("\n===== Task 10.4 B: I/O-Bound Email Re-Send (30 Emails, Concurrency = 4) =====");

        var emailService = new EmailService(_store); // no failures

        var testOrders = Enumerable.Range(1001, 30).Select(id => new Order
        {
            Id = id,
            CustomerId = 1,
            TotalAmount = 50.00m,
            Status = OrderStatus.Completed,
            CreatedAt = DateTime.Now
        }).ToList();

        var customer = new Customer
        {
            Id = 1,
            Name = "Test Customer",
            Email = "customer@example.com",
            IsActive = true
        };

        int currentRunning = 0;
        int maxRunning = 0;

        var sw = Stopwatch.StartNew();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = ct
        };

        // Requirement: Concurrency limited by Parallel.ForEachAsync from the framework
        await Parallel.ForEachAsync(testOrders, options, async (order, cancellationToken) =>
        {
            int current = Interlocked.Increment(ref currentRunning);

            // Update max concurrent observed
            int initialMax;
            do
            {
                initialMax = maxRunning;
                if (current <= initialMax) break;
            }
            while (Interlocked.CompareExchange(ref maxRunning, current, initialMax) != initialMax);

            try
            {
                await emailService.SendConfirmationAsync(order, customer, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref currentRunning);
            }
        });

        sw.Stop();

        var allEmails = await _store.ReadAsync<EmailMessage>(DataFiles.Emails, ct);
        var resendEmails = allEmails.Where(e => e.OrderId >= 1001 && e.OrderId <= 1030).ToList();

        Console.WriteLine($"Emails sent: {resendEmails.Count} (Expected: 30)");
        Console.WriteLine($"Max concurrent sends observed: {maxRunning} (Expected: exactly 4)");
        Console.WriteLine($"Total Elapsed Time: {sw.Elapsed.TotalSeconds:F2} seconds (Expected ~8 s)");
        Console.WriteLine("============================================================================\n");
    }
}
