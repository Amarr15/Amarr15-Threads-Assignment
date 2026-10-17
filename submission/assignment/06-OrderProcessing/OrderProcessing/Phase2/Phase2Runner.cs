using System.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public class Phase2Runner
{
    public async Task RunAsync(CancellationToken ct)
    {
        Log.Info("Phase2Runner -> Phase 2 started.");

        var seeder = new DataSeeder();
        await seeder.ResetAsync(ct);

        var raceConditionDemo = new RaceConditionDemo();
        await raceConditionDemo.RunAsync(ct);

        BlockingWaitDemo.RunDemo();

        await seeder.ResetAsync(ct);

        var store = new JsonFileStore();

        var customerRepository = new CustomerRepository(store);
        var productRepository = new ProductRepository(store);
        var couponRepository = new CouponRepository(store);

        var validator = new OrderValidator(
            customerRepository,
            productRepository,
            couponRepository);


        var incomingOrders = await store.ReadAsync<Order>(
            DataFiles.IncomingOrders, ct);

        if (incomingOrders.Count == 0)
        {
            Log.Info("Phase2Runner -> No incoming orders found.");
            return;
        }

        // Compare concurrent and sequential pricing for Order #1
        var pricingOrder = incomingOrders.Find(order => order.Id == 1);

        if (pricingOrder is not null)
        {
            var pricer = new OrderPricer();

            var concurrentStopwatch = Stopwatch.StartNew();

            await pricer.PriceOrderAsync(pricingOrder, ct);

            concurrentStopwatch.Stop();

            decimal concurrentTotal = pricingOrder.TotalAmount;

            var sequentialStopwatch = Stopwatch.StartNew();

            await pricer.PriceOrderSequentialAsync(pricingOrder, ct);

            sequentialStopwatch.Stop();

            decimal sequentialTotal = pricingOrder.TotalAmount;

            Console.WriteLine("\n===== Pricing Performance Comparison: Order #1 =====");
            Console.WriteLine($"Concurrent total: {concurrentTotal:F2}");
            Console.WriteLine($"Concurrent time: {concurrentStopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Sequential total: {sequentialTotal:F2}");
            Console.WriteLine($"Sequential time: {sequentialStopwatch.ElapsedMilliseconds} ms");
        }


        // Start validation for all orders first
        var validationTasks = new List<Task<bool>>();

        foreach (var order in incomingOrders)
        {
            validationTasks.Add(
                validator.ValidateOrderAsync(order, ct));
        }

        // Await each task individually, as required by the assignment
        var validationResults = new List<bool>();

        foreach (var task in validationTasks)
        {
            validationResults.Add(await task);
        }

        var validOrders = new List<Order>();
        var orderRepository = new OrderRepository(store);

        // Save every order immediately after validation (both validated and rejected)
        for (int i = 0; i < incomingOrders.Count; i++)
        {
            var order = incomingOrders[i];

            await orderRepository.SaveAsync(order, ct);

            if (validationResults[i])
            {
                validOrders.Add(order);
            }
            else
            {
                Log.Info(
                    $"Phase2Runner -> Order #{order.Id} rejected: {order.FailureReason}");
            }
        }

        var processor = new OrderProcessorV2();
        var stopwatch = Stopwatch.StartNew();


        var tasks = new List<Task<bool>>();

        foreach (var order in validOrders)
        {
            tasks.Add(processor.ProcessValidatedOrderAsync(order, ct));

            if (tasks.Count == 1)
            {
                Console.WriteLine(
                    $"First task status immediately after start: {tasks[0].Status}");
            }
        }

        await Task.Delay(1000, ct);

        Console.WriteLine("\n===== Task Status After 1 Second =====");

        foreach (var task in tasks)
            Console.WriteLine(task.Status);

        Console.WriteLine("\n===== Awaiting Orders =====");

        for (int i = 0; i < tasks.Count; i++)
        {
            try
            {
                bool completed = await tasks[i];

                Console.WriteLine($"Order #{validOrders[i].Id}: {tasks[i].Status}, result = {completed}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var order = validOrders[i];

                order.Status = OrderStatus.Failed;
                order.FailureReason = ex.Message;

                await orderRepository.SaveAsync(order, ct);

                Log.Error(
                    $"Phase2Runner -> Order #{order.Id} failed: {ex.Message}");

                Console.WriteLine(
                    $"Order #{order.Id}: {tasks[i].Status}");
            }
        }

        stopwatch.Stop();

        var reportGenerator = new ReportGenerator();

        Console.WriteLine("\nGenerating report...");

        Task<Report> reportTask =
            reportGenerator.GenerateReportAsync(ct);

        var report = await reportTask;

        Console.WriteLine("\n===== Final Task Statuses =====");

        foreach (var task in tasks)
            Console.WriteLine(task.Status);

        Console.WriteLine(
            $"\nTotal processing time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

        var finalOrders = await orderRepository.GetAllAsync(ct);

        Console.WriteLine($"Orders saved: {finalOrders.Count}");

        Console.WriteLine("\n===== Final Report =====");
        Console.WriteLine(
            System.Text.Json.JsonSerializer.Serialize(
                report,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy =
                        System.Text.Json.JsonNamingPolicy.CamelCase
                }));

        Log.Success("Phase2Runner -> Phase 2 finished.");
    }
}
