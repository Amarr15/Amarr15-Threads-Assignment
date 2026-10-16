using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

public class FastShip : IShippingCarrier
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _failForOrderIds;

    public string Name => "FastShip";

    public FastShip(JsonFileStore? store = null, IEnumerable<int>? failForOrderIds = null)
    {
        _store = store ?? new JsonFileStore();
        _failForOrderIds = new HashSet<int>(failForOrderIds ?? Enumerable.Empty<int>());
    }

    public async Task<decimal> GetQuoteAsync(Order order, CancellationToken ct)
    {
        try
        {
            await Task.Delay(150, ct);

            if (_failForOrderIds.Contains(order.Id))
            {
                throw new ShippingProviderUnavailableException("FastShip unavailable");
            }

            int delay = Random.Shared.Next(500, 1501);
            await Task.Delay(delay, ct);

            decimal basePrice = CalculateBasePrice(order);
            return Math.Round(basePrice * 1.00m, 2, MidpointRounding.AwayFromZero);
        }
        catch (OperationCanceledException)
        {
            Log.Info($"[{Name}] cancelled");
            throw;
        }
    }

    private decimal CalculateBasePrice(Order order)
    {
        if (order.SubTotal >= 1000m) return 0m;

        var products = _store.Read<Product>(DataFiles.Products);
        decimal totalWeight = 0m;
        int totalQuantity = 0;

        foreach (var item in order.Items)
        {
            var p = products.Find(x => x.Id == item.ProductId);
            if (p is not null)
            {
                totalWeight += p.WeightKg * item.Quantity;
                totalQuantity += item.Quantity;
            }
        }

        return 5.00m + (1.50m * totalQuantity) + (2.00m * totalWeight);
    }
}
