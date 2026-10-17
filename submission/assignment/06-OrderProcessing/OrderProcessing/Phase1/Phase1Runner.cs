using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public class Phase1Runner
{
    public async Task RunAsync(CancellationToken ct)
    {
        var seeder = new DataSeeder();
        var store = new JsonFileStore();
        var incomingRepository = new IncomingOrderRepository(store);
        var orderRepository = new OrderRepository(store);

        await seeder.ResetAsync(ct);
        var incomingOrders = await incomingRepository.GetAllAsync(ct);

        var syncValidator = new SyncOrderValidator(
            new SyncCustomerRepository(store),
            new SyncProductRepository(store),
            new SyncCouponRepository(store));

        var stopwatch = Stopwatch.StartNew();

        foreach (var order in incomingOrders)
        {
            ct.ThrowIfCancellationRequested();
            syncValidator.Validate(order);
        }

        stopwatch.Stop();
        var syncTime = stopwatch.Elapsed.TotalSeconds;

        await seeder.ResetAsync(ct);
        incomingOrders = await incomingRepository.GetAllAsync(ct);

        var asyncValidator = new OrderValidator(
            new CustomerRepository(store),
            new ProductRepository(store),
            new CouponRepository(store));

        stopwatch.Restart();

        foreach (var order in incomingOrders)
        {
            ct.ThrowIfCancellationRequested();

            await asyncValidator.ValidateOrderAsync(order, ct);
            await orderRepository.SaveAsync(order, ct);
        }

        stopwatch.Stop();
        var asyncTime = stopwatch.Elapsed.TotalSeconds;

        var validatedOrders = await orderRepository.GetByStatusAsync(
            OrderStatus.Validated, ct);

        var rejectedOrders = await orderRepository.GetByStatusAsync(
            OrderStatus.Rejected, ct);

        Console.WriteLine();
        Console.WriteLine("===== Validation Summary =====");
        Console.WriteLine($"Total Orders: {validatedOrders.Count + rejectedOrders.Count}");
        Console.WriteLine($"Validated:    {validatedOrders.Count}");
        Console.WriteLine($"Rejected:     {rejectedOrders.Count}");

        foreach (var order in rejectedOrders.OrderBy(order => order.Id))
        {
            Console.WriteLine(
                $"  - Order #{order.Id}: {order.FailureReason}");
        }

        Console.WriteLine($"Sync Time:  {syncTime:F1} seconds");
        Console.WriteLine($"Async Time: {asyncTime:F1} seconds");
        Console.WriteLine(
            $"orders.json now contains {validatedOrders.Count + rejectedOrders.Count} records.");
    }
}