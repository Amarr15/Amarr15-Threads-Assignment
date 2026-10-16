using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public class Phase3Runner
{
    public async Task RunAsync(CancellationToken ct)
    {
        Log.Info("Phase3Runner -> Phase 3 started.");

        var seeder = new DataSeeder();
        var store = new JsonFileStore();

        // ----------------------------------------------------
        // 1. Task 9.7: Fire-and-forget investigation
        // ----------------------------------------------------
        var orderRepoClean = new OrderRepository(store);
        var emailServiceClean = new EmailService(store);
        var fafDemo = new FireAndForgetDemo(orderRepoClean, emailServiceClean);
        await fafDemo.RunDemoAsync(ct);

        // ----------------------------------------------------
        // 2. Task 9.6: Sync vs Async comparison (Orders #1–#10)
        // ----------------------------------------------------
        Console.WriteLine("\n===== Task 9.6: Sync vs Async Performance Comparison (Orders #1–#10) =====");

        // Reset before Sync
        await seeder.ResetAsync(ct);
        var incomingRepo = new IncomingOrderRepository(store);
        var allOrders = await incomingRepo.GetAllAsync(ct);
        var comparisonOrders = allOrders.Where(o => o.Id >= 1 && o.Id <= 10).ToList();

        // Sync Run
        var syncProcessor = new SyncOrderProcessor(store);
        var syncThreads = new List<Thread>();
        var syncStopwatch = Stopwatch.StartNew();
        int initialThreads = ThreadPool.ThreadCount;

        foreach (var order in comparisonOrders)
        {
            var t = new Thread(() => syncProcessor.ProcessOrder(order));
            syncThreads.Add(t);
            t.Start();
        }

        foreach (var t in syncThreads)
        {
            t.Join();
        }
        syncStopwatch.Stop();
        int syncThreadCount = ThreadPool.ThreadCount;

        Console.WriteLine($"Sync Version (10 dedicated threads):");
        Console.WriteLine($"  - Elapsed Time: {syncStopwatch.ElapsedMilliseconds} ms ({syncStopwatch.Elapsed.TotalSeconds:F2} s)");
        Console.WriteLine($"  - OS Threads Created: {syncThreads.Count}, ThreadPool Threads: {syncThreadCount}");

        // Reset before Async
        await seeder.ResetAsync(ct);
        allOrders = await incomingRepo.GetAllAsync(ct);
        comparisonOrders = allOrders.Where(o => o.Id >= 1 && o.Id <= 10).ToList();

        var customerRepoClean = new CustomerRepository(store);
        var productRepoClean = new ProductRepository(store);
        var couponRepoClean = new CouponRepository(store);
        var customerServiceClean = new CustomerService(customerRepoClean);
        var inventoryServiceClean = new InventoryService(store);
        var pricingServiceClean = new PricingService(); // no shipping failures
        var paymentGatewayClean = new PaymentGateway(store); // no failures

        var cleanOrderProcessor = new OrderProcessor(
            customerServiceClean,
            inventoryServiceClean,
            pricingServiceClean,
            paymentGatewayClean,
            orderRepoClean,
            emailServiceClean);

        var asyncStopwatch = Stopwatch.StartNew();
        var asyncTasks = new List<Task<bool>>();

        foreach (var order in comparisonOrders)
        {
            asyncTasks.Add(cleanOrderProcessor.ProcessOrderAsync(order, ct));
        }

        foreach (var task in asyncTasks)
        {
            await task;
        }
        asyncStopwatch.Stop();
        int asyncThreadCount = ThreadPool.ThreadCount;

        Console.WriteLine($"Async Version (10 tasks on thread pool):");
        Console.WriteLine($"  - Elapsed Time: {asyncStopwatch.ElapsedMilliseconds} ms ({asyncStopwatch.Elapsed.TotalSeconds:F2} s)");
        Console.WriteLine($"  - ThreadPool Threads: {asyncThreadCount}");
        Console.WriteLine("===========================================================================\n");

        // ----------------------------------------------------
        // 3. Task 9.3: Full Checkout for all 20 Orders with Controlled Failures
        // ----------------------------------------------------
        Console.WriteLine("\n===== Running Full Checkout for All 20 Orders =====");
        await seeder.ResetAsync(ct);
        var ordersToProcess = await incomingRepo.GetAllAsync(ct);

        // Configure controlled failure services
        var customerRepo = new CustomerRepository(store);
        var customerService = new CustomerService(customerRepo);
        var inventoryService = new InventoryService(store);
        var pricingService = new PricingService(); // Empty failShipping in Phase 3

        var paymentGateway = new PaymentGateway(
            store,
            timeoutOrderIds: new[] { 5 },
            declineOrderIds: new[] { 8 });

        var orderRepository = new OrderRepository(
            store,
            failSaveForOrderIds: new[] { 13 });

        var emailService = new EmailService(
            store,
            failAttempts: new Dictionary<int, int>
            {
                [3] = 2,
                [17] = int.MaxValue
            });

        var processor = new OrderProcessor(
            customerService,
            inventoryService,
            pricingService,
            paymentGateway,
            orderRepository,
            emailService);

        var totalStopwatch = Stopwatch.StartNew();
        var processTasks = new List<Task<bool>>();

        // Start all 20 orders
        foreach (var order in ordersToProcess)
        {
            processTasks.Add(processor.ProcessOrderAsync(order, ct));
        }

        // Await each task
        try
        {
            for (int i = 0; i < processTasks.Count; i++)
            {
                await processTasks[i];
            }
        }
        catch (OperationCanceledException)
        {
            // Wait briefly for all cancellations to finalize
            await Task.WhenAll(processTasks.Select(t => t.ContinueWith(_ => { })));

            var cancelledOrders = await orderRepository.GetAllAsync(CancellationToken.None);
            int finished = cancelledOrders.Count(o => o.Status != OrderStatus.Pending);
            int cancelled = cancelledOrders.Count(o => o.Status == OrderStatus.Pending && o.FailureReason == "Cancelled");

            Console.WriteLine($"\nRun cancelled — {finished} orders finished, {cancelled} cancelled.");
            return;
        }

        totalStopwatch.Stop();

        // ----------------------------------------------------
        // 4. Print Final Results Table
        // ----------------------------------------------------
        var finalOrders = await orderRepository.GetAllAsync(ct);

        Console.WriteLine("\n===== Final Orders Table (Phase 3) =====");
        Console.WriteLine($"{"Order",-7} | {"Status",-15} | {"Total",-10} | {"Reason"}");
        Console.WriteLine(new string('-', 75));

        foreach (var o in finalOrders.OrderBy(o => o.Id))
        {
            Console.WriteLine($"{o.Id,-7} | {o.Status,-15} | {o.TotalAmount,10:F2} | {o.FailureReason ?? "-"}");
        }

        Console.WriteLine(new string('-', 75));
        Console.WriteLine($"Total Checkout Time: {totalStopwatch.Elapsed.TotalSeconds:F2} seconds\n");

        // ----------------------------------------------------
        // 5. Verify File Counts
        // ----------------------------------------------------
        var payments = await store.ReadAsync<PaymentTransaction>(DataFiles.Payments, ct);
        var emails = await store.ReadAsync<EmailMessage>(DataFiles.Emails, ct);
        var emailRetries = await store.ReadAsync<EmailMessage>(DataFiles.EmailRetry, ct);
        var products = await store.ReadAsync<Product>(DataFiles.Products, ct);

        int completedCount = finalOrders.Count(o => o.Status == OrderStatus.Completed);
        int rejectedCount = finalOrders.Count(o => o.Status == OrderStatus.Rejected);
        int paymentFailedCount = finalOrders.Count(o => o.Status == OrderStatus.PaymentFailed);
        int failedCount = finalOrders.Count(o => o.Status == OrderStatus.Failed);

        Console.WriteLine("===== End-State Verification =====");
        Console.WriteLine($"Orders total: {finalOrders.Count} (Expected: 20)");
        Console.WriteLine($"  - Completed: {completedCount} (Expected: 11)");
        Console.WriteLine($"  - Rejected: {rejectedCount} (Expected: 6)");
        Console.WriteLine($"  - PaymentFailed: {paymentFailedCount} (Expected: 2)");
        Console.WriteLine($"  - Failed: {failedCount} (Expected: 1)");
        Console.WriteLine($"Payments: {payments.Count} (Expected: 13: 11 Captured, 1 Declined, 1 Refunded)");
        Console.WriteLine($"Emails: {emails.Count} (Expected: 10)");
        Console.WriteLine($"Email Retries: {emailRetries.Count} (Expected: 1 for Order #17)");
        var keyboard = products.Find(p => p.Id == 4);
        Console.WriteLine($"Mechanical Keyboard Stock: {keyboard?.StockQuantity} (Expected: 0)");

        Log.Success("Phase3Runner -> Phase 3 finished.");
    }
}
