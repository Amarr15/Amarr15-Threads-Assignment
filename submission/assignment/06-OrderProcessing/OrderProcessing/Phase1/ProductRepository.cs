using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public class ProductRepository
{
    private readonly JsonFileStore _store;

    public ProductRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<List<Product>> GetAllAsync(
        CancellationToken ct)
    {
        Log.Info("ProductRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("ProductRepository -> Executing query to get all products...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        return await _store.ReadAsync<Product>(
            DataFiles.Products,
            ct);
    }

    public async Task<Product?> GetByIdAsync(
        int id,
        CancellationToken ct)
    {
        Log.Info($"ProductRepository -> Opening connection for product #{id}...");
        await Task.Delay(200, ct);

        Log.Info($"ProductRepository -> Executing query for product #{id}...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Product> products =
            await _store.ReadAsync<Product>(
                DataFiles.Products,
                ct);

        return products.Find(product => product.Id == id);
    }

    public async Task<bool> ReserveStockAsync(
        int productId,
        int qty,
        CancellationToken ct)
    {
        Log.Info("ProductRepository -> Opening connection to reserve stock...");
        await Task.Delay(200, ct);

        Log.Info("ProductRepository -> Beginning stock transaction...");
        await Task.Delay(100, ct);

        Log.Info($"ProductRepository -> Updating stock for product #{productId}...");
        await Task.Delay(400, ct);

        bool reserved = false;

        await _store.UpdateAsync<Product>(
            DataFiles.Products,
            products =>
            {
                var product = products.Find(
                    item => item.Id == productId);

                if (product is null ||
                    product.StockQuantity < qty)
                {
                    return products;
                }

                product.StockQuantity -= qty;
                reserved = true;

                return products;
            },
            ct);

        Log.Info("ProductRepository -> Committing stock transaction...");
        await Task.Delay(200, ct);

        return reserved;
    }
    public async Task ReleaseStockAsync(
    int productId,
    int qty,
    CancellationToken ct)
    {
        await _store.UpdateAsync<Product>(
            DataFiles.Products,
            products =>
            {
                var product = products.Find(
                    item => item.Id == productId);

                if (product is not null)
                {
                    product.StockQuantity += qty;
                }

                return products;
            },
            ct);

        Log.Info(
            $"ProductRepository -> Released {qty} unit(s) of product #{productId}.");
    }
    public async Task<bool> ReserveStockUnsafeAsync(
    int productId,
    int qty,
    CancellationToken ct)
    {
        Log.Info(
            $"ProductRepository -> [UNSAFE] Reading stock for product #{productId}...");

        await Task.Delay(200, ct);

        var products = await _store.ReadAsync<Product>(
            DataFiles.Products,
            ct);

        var product = products.Find(p => p.Id == productId);

        if (product is null || product.StockQuantity < qty)
            return false;

        int newStock = product.StockQuantity - qty;

        Log.Info("ProductRepository -> [UNSAFE] Beginning transaction...");
        await Task.Delay(100, ct);

        Log.Info("ProductRepository -> [UNSAFE] Updating stock...");
        await Task.Delay(400, ct);

        product.StockQuantity = newStock;

        await _store.WriteAsync<Product>(
            DataFiles.Products,
            products,
            ct);

        Log.Info("ProductRepository -> [UNSAFE] Committing transaction...");
        await Task.Delay(200, ct);

        return true;
    }
}