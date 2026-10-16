using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Phase3;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

public class Phase4Runner
{
    public async Task RunAsync(CancellationToken ct)
    {
        Log.Info("Phase4Runner -> Phase 4 Scale-Up started.");

        var store = new JsonFileStore();
        var seeder = new DataSeeder();

        // ================================================================
        // Task 10.1: Live Checkout Progress (Task.WhenEach)
        // ================================================================
        Console.WriteLine("\n===== Task 10.1: Live Checkout Progress (Real-Time as Completed) =====");
        await seeder.ResetAsync(ct);

        var incomingRepo = new IncomingOrderRepository(store);
        var ordersToProcess = await incomingRepo.GetAllAsync(ct);

        // Same failure configuration as Phase 3 (9.2)
        var customerRepo = new CustomerRepository(store);
        var customerService = new CustomerService(customerRepo);
        var inventoryService = new InventoryService(store);
        var pricingService = new PricingService();

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

        var orderTasks = ordersToProcess.Select(async order =>
        {
            var sw = Stopwatch.StartNew();
            await processor.ProcessOrderAsync(order, ct);
            sw.Stop();
            return (Order: order, ElapsedSeconds: sw.Elapsed.TotalSeconds);
        }).ToList();

        int counter = 0;
        // Task.WhenEach (.NET 9/10) yields tasks in order of completion without rebuilding lists
        await foreach (var completedTask in Task.WhenEach(orderTasks))
        {
            var (order, elapsed) = await completedTask;
            counter++;

            string statusText = order.Status switch
            {
                OrderStatus.Completed => $"Completed after {elapsed:F1} s",
                OrderStatus.Rejected => $"Rejected ({order.FailureReason}) after {elapsed:F1} s",
                OrderStatus.PaymentFailed => $"PaymentFailed ({order.FailureReason}) after {elapsed:F1} s",
                OrderStatus.Failed => $"Failed ({order.FailureReason}) after {elapsed:F1} s",
                _ => $"{order.Status} after {elapsed:F1} s"
            };

            Console.WriteLine($"[{counter,2}/20] Order #{order.Id,-2} -> {statusText}");
        }

        var finalOrders = await orderRepository.GetAllAsync(ct);
        int completedCount = finalOrders.Count(o => o.Status == OrderStatus.Completed);
        int rejectedCount = finalOrders.Count(o => o.Status == OrderStatus.Rejected);
        int paymentFailedCount = finalOrders.Count(o => o.Status == OrderStatus.PaymentFailed);
        int failedCount = finalOrders.Count(o => o.Status == OrderStatus.Failed);

        Console.WriteLine("\n===== Task 10.1 Status Breakdown =====");
        Console.WriteLine($"Total:         {finalOrders.Count}");
        Console.WriteLine($"Completed:     {completedCount} (Expected: 11)");
        Console.WriteLine($"Rejected:      {rejectedCount} (Expected: 6)");
        Console.WriteLine($"PaymentFailed: {paymentFailedCount} (Expected: 2)");
        Console.WriteLine($"Failed:        {failedCount} (Expected: 1)");
        Console.WriteLine("======================================================================\n");

        // ================================================================
        // Task 10.2: Customer Dashboard
        // ================================================================
        Console.WriteLine("\n===== Task 10.2: Customer Dashboard Service =====");
        var dashboardService = new CustomerDashboardService(store, failPaymentsForCustomerIds: new[] { 99 });

        // Run for Customer #3 (Omar Hassan)
        Console.WriteLine("\n--- Loading Dashboard for Customer #3 (Omar Hassan) ---");
        var dashboard3 = await dashboardService.LoadAsync(3, ct);
        Console.WriteLine($"Customer: {dashboard3.Customer.Name} ({dashboard3.Customer.Email})");
        Console.WriteLine($"Orders count:   {dashboard3.Orders.Count} (Expected: 2 -> #3 Completed, #13 Failed)");
        foreach (var o in dashboard3.Orders)
        {
            Console.WriteLine($"  - Order #{o.Id}: Status={o.Status}, Total={o.TotalAmount:F2}");
        }
        Console.WriteLine($"Payments count: {dashboard3.Payments.Count} (Expected: 2 -> #3 Captured, #13 Refunded)");
        foreach (var p in dashboard3.Payments)
        {
            Console.WriteLine($"  - Payment: Order #{p.OrderId}, Status={p.Status}, Txn={p.TransactionId}");
        }
        Console.WriteLine($"Emails count:   {dashboard3.Emails.Count} (Expected: 1 -> #3)");
        foreach (var e in dashboard3.Emails)
        {
            Console.WriteLine($"  - Email: Order #{e.OrderId}, To={e.To}, SentAt={e.SentAt:HH:mm:ss}");
        }

        // Run for Customer #99 (Expected to report both failures)
        Console.WriteLine("\n--- Loading Dashboard for Customer #99 (Multiple Failure Test) ---");
        try
        {
            await dashboardService.LoadAsync(99, ct);
        }
        catch (AggregateException)
        {
            Console.WriteLine("Caught expected AggregateException reporting both failures.");
        }
        Console.WriteLine("=================================================================\n");

        // ================================================================
        // Task 10.3: Fastest Shipping Quote
        // ================================================================
        Console.WriteLine("\n===== Task 10.3: Fastest Shipping Quote (3 Carriers) =====");
        var quoteService = new ShippingQuoteService();

        for (int orderId = 1; orderId <= 5; orderId++)
        {
            var order = finalOrders.Find(o => o.Id == orderId) ?? new Order { Id = orderId, SubTotal = 500m };
            var quote = await quoteService.GetFastestQuoteAsync(order, ct);
            Console.WriteLine($"Order #{order.Id}: Winning Carrier = '{quote.Carrier}', Price = {quote.Price:F2}, Response Time = {quote.ElapsedMs} ms");
        }

        // Show failure on Order #6 when all carriers are configured to fail
        Console.WriteLine("\n--- Testing All Carriers Failing on Order #6 ---");
        var failingQuoteService = new ShippingQuoteService(new IShippingCarrier[]
        {
            new FastShip(store, failForOrderIds: new[] { 6 }),
            new CairoExpress(store, failForOrderIds: new[] { 6 }),
            new NileCourier(store, failForOrderIds: new[] { 6 })
        });

        try
        {
            var order6 = finalOrders.Find(o => o.Id == 6) ?? new Order { Id = 6, SubTotal = 100m };
            await failingQuoteService.GetFastestQuoteAsync(order6, ct);
        }
        catch (ShippingProviderUnavailableException ex)
        {
            Console.WriteLine($"Correctly caught expected exception: {ex.Message}");
        }
        Console.WriteLine("=========================================================\n");

        // ================================================================
        // Task 10.4: Fraud Scoring (CPU) & Email Re-Send (I/O)
        // ================================================================
        var fraudService = new FraudScoringService();
        fraudService.RunScoringComparison(ct);

        var emailResendRunner = new EmailResendRunner(store);
        await emailResendRunner.RunEmailResendAsync(ct);

        Log.Success("Phase4Runner -> Phase 4 Scale-Up finished.");
    }
}
