using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public class InventoryService
{
    private readonly JsonFileStore _store;

    public InventoryService(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<bool> ReserveStockAsync(Order order, CancellationToken ct)
    {
        Log.Info($"InventoryService -> Opening connection to reserve stock for Order #{order.Id}...");
        await Task.Delay(200, ct);

        Log.Info($"InventoryService -> Beginning transaction for Order #{order.Id}...");
        await Task.Delay(100, ct);

        Log.Info($"InventoryService -> Updating stock for Order #{order.Id}...");
        await Task.Delay(400, ct);

        bool success = false;
        string? failureProductName = null;

        await _store.UpdateAsync<Product>(
            DataFiles.Products,
            products =>
            {
                // Check all items first (all or nothing)
                foreach (var item in order.Items)
                {
                    var product = products.Find(p => p.Id == item.ProductId);
                    if (product is null || product.StockQuantity < item.Quantity)
                    {
                        failureProductName = product?.Name ?? $"Product #{item.ProductId}";
                        return products; // Rollback: do not deduct anything
                    }
                }

                // If all available, reserve
                foreach (var item in order.Items)
                {
                    var product = products.Find(p => p.Id == item.ProductId)!;
                    product.StockQuantity -= item.Quantity;
                }

                success = true;
                return products;
            },
            ct);

        Log.Info($"InventoryService -> Committing stock transaction for Order #{order.Id}...");
        await Task.Delay(200, ct);

        if (!success)
        {
            order.FailureReason = $"Insufficient stock for '{failureProductName}'";
        }

        return success;
    }

    public async Task ReleaseStockAsync(Order order, CancellationToken ct)
    {
        Log.Info($"InventoryService -> Opening connection to release stock for Order #{order.Id}...");
        await Task.Delay(200, ct);

        Log.Info($"InventoryService -> Beginning transaction to release stock for Order #{order.Id}...");
        await Task.Delay(100, ct);

        Log.Info($"InventoryService -> Releasing stock for Order #{order.Id}...");
        await Task.Delay(400, ct);

        await _store.UpdateAsync<Product>(
            DataFiles.Products,
            products =>
            {
                foreach (var item in order.Items)
                {
                    var product = products.Find(p => p.Id == item.ProductId);
                    if (product is not null)
                    {
                        product.StockQuantity += item.Quantity;
                    }
                }
                return products;
            },
            ct);

        Log.Info($"InventoryService -> Committing release transaction for Order #{order.Id}...");
        await Task.Delay(200, ct);

        Log.Info($"InventoryService -> Released reserved stock for Order #{order.Id}.");
    }
}
