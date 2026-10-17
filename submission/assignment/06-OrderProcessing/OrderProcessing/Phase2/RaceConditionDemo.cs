
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Storage;
using OrderProcessing.Phase1;

namespace OrderProcessing.Phase2;

public class RaceConditionDemo
{
    public async Task RunAsync(CancellationToken ct)
    {
        Log.Info("RaceConditionDemo -> Starting stock race condition demo.");

        var store = new JsonFileStore();
        var repository = new ProductRepository(store);

        // Reset the keyboard stock to exactly one unit.
        await store.UpdateAsync<OrderProcessing.Shared.Models.Product>(
            DataFiles.Products,
            products =>
            {
                var keyboard = products.Find(p => p.Id == 4);

                if (keyboard is null)
                    throw new InvalidOperationException("Keyboard product #4 was not found.");

                keyboard.StockQuantity = 1;
                return products;
            },
            ct);

        Log.Info("Unsafe test: starting orders #10 and #11 concurrently.");

        var unsafeTask1 = repository.ReserveStockUnsafeAsync(4, 1, ct);
        var unsafeTask2 = repository.ReserveStockUnsafeAsync(4, 1, ct);

        bool unsafeResult1 = await unsafeTask1;
        bool unsafeResult2 = await unsafeTask2;

        var afterUnsafe = await repository.GetByIdAsync(4, ct);

        Log.Info($"Unsafe results: Order #10 = {unsafeResult1}, Order #11 = {unsafeResult2}.");
        Log.Info($"Stock after unsafe reservations: {afterUnsafe?.StockQuantity}");

        // Reset stock before testing the safe implementation.
        await store.UpdateAsync<OrderProcessing.Shared.Models.Product>(
            DataFiles.Products,
            products =>
            {
                var keyboard = products.Find(p => p.Id == 4);

                if (keyboard is null)
                    throw new InvalidOperationException("Keyboard product #4 was not found.");

                keyboard.StockQuantity = 1;
                return products;
            },
            ct);

        Log.Info("Safe test: starting orders #10 and #11 concurrently.");

        var safeTask1 = repository.ReserveStockAsync(4, 1, ct);
        var safeTask2 = repository.ReserveStockAsync(4, 1, ct);

        bool safeResult1 = await safeTask1;
        bool safeResult2 = await safeTask2;

        var afterSafe = await repository.GetByIdAsync(4, ct);

        Log.Info($"Safe results: Order #10 = {safeResult1}, Order #11 = {safeResult2}.");
        Log.Info($"Stock after safe reservations: {afterSafe?.StockQuantity}");

        Log.Success("RaceConditionDemo -> Demo finished.");
    }
}