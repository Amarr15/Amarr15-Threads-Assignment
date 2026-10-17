using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase4;

public class ShippingQuoteService
{
    private readonly List<IShippingCarrier> _carriers;

    public ShippingQuoteService(IEnumerable<IShippingCarrier>? carriers = null)
    {
        _carriers = carriers?.ToList() ?? new List<IShippingCarrier>
        {
            new FastShip(),
            new CairoExpress(),
            new NileCourier()
        };
    }

    public async Task<ShippingQuote> GetFastestQuoteAsync(Order order, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(1200); // 1.2s time budget

        // Create dictionary mapping task to carrier
        var carrierTasks = new Dictionary<Task<decimal>, IShippingCarrier>();

        foreach (var carrier in _carriers)
        {
            var task = carrier.GetQuoteAsync(order, linkedCts.Token);
            carrierTasks[task] = carrier;
        }

        var activeTasks = new List<Task<decimal>>(carrierTasks.Keys);
        var exceptions = new List<Exception>();

        while (activeTasks.Count > 0)
        {
            Task<decimal> finishedTask;
            try
            {
                finishedTask = await Task.WhenAny(activeTasks);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                break;
            }

            activeTasks.Remove(finishedTask);

            if (finishedTask.IsCompletedSuccessfully)
            {
                sw.Stop();
                var winner = carrierTasks[finishedTask];
                decimal price = finishedTask.Result;

                // Requirement R3: Cancel all remaining carriers
                linkedCts.Cancel();

                // Wait briefly for cancellations to log
                await Task.WhenAll(activeTasks.Select(t => t.ContinueWith(_ => { })));

                return new ShippingQuote
                {
                    Carrier = winner.Name,
                    Price = price,
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
            else
            {
                // Task failed or threw exception
                if (finishedTask.Exception is not null)
                {
                    exceptions.AddRange(finishedTask.Exception.InnerExceptions);
                }
            }
        }

        sw.Stop();

        // Check if caller explicitly cancelled
        ct.ThrowIfCancellationRequested();

        // Requirement R6: If all three carriers failed within the budget, throw All carriers failed
        if (exceptions.Count >= _carriers.Count && sw.ElapsedMilliseconds < 1200)
        {
            throw new ShippingProviderUnavailableException("All carriers failed");
        }

        // Requirement R4: Time budget 1.2s exceeded -> Flat rate
        linkedCts.Cancel();
        return new ShippingQuote
        {
            Carrier = "Flat rate",
            Price = 25.00m,
            ElapsedMs = sw.ElapsedMilliseconds
        };
    }
}
