using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

public class CustomerDashboardService
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _failPaymentsForCustomerIds;

    public CustomerDashboardService(
        JsonFileStore? store = null,
        IEnumerable<int>? failPaymentsForCustomerIds = null)
    {
        _store = store ?? new JsonFileStore();
        _failPaymentsForCustomerIds = new HashSet<int>(failPaymentsForCustomerIds ?? Enumerable.Empty<int>());
    }

    public async Task<CustomerDashboard> LoadAsync(int customerId, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        Log.Info($"CustomerDashboardService -> Loading dashboard concurrently for Customer #{customerId}...");

        // Task 1: Customer Profile
        var customerTask = Task.Run(async () =>
        {
            Log.Info($"Dashboard [Profile] -> Opening connection for Customer #{customerId}...");
            await Task.Delay(200, ct);
            Log.Info($"Dashboard [Profile] -> Executing query for Customer #{customerId}...");
            await Task.Delay(Random.Shared.Next(300, 601), ct);

            var customers = await _store.ReadAsync<Customer>(DataFiles.Customers, ct);
            var customer = customers.Find(c => c.Id == customerId);
            if (customer is null)
            {
                throw new CustomerNotFoundException($"Customer #{customerId} not found");
            }
            return customer;
        }, ct);

        // Task 2: Orders
        var ordersTask = Task.Run(async () =>
        {
            Log.Info($"Dashboard [Orders] -> Opening connection for Customer #{customerId}...");
            await Task.Delay(200, ct);
            Log.Info($"Dashboard [Orders] -> Executing query for Customer #{customerId}...");
            await Task.Delay(Random.Shared.Next(300, 601), ct);

            var orders = await _store.ReadAsync<Order>(DataFiles.Orders, ct);
            return orders.Where(o => o.CustomerId == customerId).ToList();
        }, ct);

        // Task 3: Payments
        var paymentsTask = Task.Run(async () =>
        {
            Log.Info($"Dashboard [Payments] -> Opening connection for Customer #{customerId}...");
            await Task.Delay(200, ct);
            Log.Info($"Dashboard [Payments] -> Executing query for Customer #{customerId}...");
            await Task.Delay(Random.Shared.Next(300, 601), ct);

            if (_failPaymentsForCustomerIds.Contains(customerId))
            {
                throw new TimeoutException("Payments store timeout");
            }

            var orders = await _store.ReadAsync<Order>(DataFiles.Orders, ct);
            var customerOrderIds = orders.Where(o => o.CustomerId == customerId).Select(o => o.Id).ToHashSet();

            var payments = await _store.ReadAsync<PaymentTransaction>(DataFiles.Payments, ct);
            return payments.Where(p => customerOrderIds.Contains(p.OrderId)).ToList();
        }, ct);

        // Task 4: Emails
        var emailsTask = Task.Run(async () =>
        {
            Log.Info($"Dashboard [Emails] -> Opening connection for Customer #{customerId}...");
            await Task.Delay(200, ct);
            Log.Info($"Dashboard [Emails] -> Executing query for Customer #{customerId}...");
            await Task.Delay(Random.Shared.Next(300, 601), ct);

            var orders = await _store.ReadAsync<Order>(DataFiles.Orders, ct);
            var customerOrderIds = orders.Where(o => o.CustomerId == customerId).Select(o => o.Id).ToHashSet();

            var emails = await _store.ReadAsync<EmailMessage>(DataFiles.Emails, ct);
            return emails.Where(e => customerOrderIds.Contains(e.OrderId)).ToList();
        }, ct);

        // Wait for all 4 loads with a single await
        var allLoadsTask = Task.WhenAll(customerTask, ordersTask, paymentsTask, emailsTask);

        try
        {
            await allLoadsTask;
        }
        catch
        {
            // Requirement R4: Report every failure, not only the first one
            var allExceptions = allLoadsTask.Exception?.InnerExceptions ?? new List<Exception>().AsReadOnly();
            Console.WriteLine($"[CustomerDashboard] Errors encountered loading Customer #{customerId} ({allExceptions.Count} failures):");
            foreach (var ex in allExceptions)
            {
                Console.WriteLine($"  - {ex.GetType().Name}: {ex.Message}");
            }
            throw new AggregateException($"Multiple dashboard load failures for Customer #{customerId}", allExceptions);
        }

        sw.Stop();
        Log.Success($"CustomerDashboardService -> Dashboard loaded in {sw.ElapsedMilliseconds} ms ({sw.Elapsed.TotalSeconds:F2} s)");

        return new CustomerDashboard
        {
            Customer = await customerTask,
            Orders = await ordersTask,
            Payments = await paymentsTask,
            Emails = await emailsTask
        };
    }
}
