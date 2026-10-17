using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public class OrderRepository
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _failSaveForOrderIds;

    public OrderRepository(JsonFileStore store, IEnumerable<int>? failSaveForOrderIds = null)
    {
        _store = store;
        _failSaveForOrderIds = new HashSet<int>(failSaveForOrderIds ?? Enumerable.Empty<int>());
    }

    public async Task<List<Order>> GetAllAsync(
        CancellationToken ct)
    {
        Log.Info("OrderRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("OrderRepository -> Executing query to get all orders...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        return await _store.ReadAsync<Order>(
            DataFiles.Orders,
            ct);
    }

    public async Task<List<Order>> GetByStatusAsync(
        OrderStatus status,
        CancellationToken ct)
    {
        Log.Info($"OrderRepository -> Opening connection to get orders with status {status}...");
        await Task.Delay(200, ct);

        Log.Info($"OrderRepository -> Executing query for status {status}...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Order> orders =
            await _store.ReadAsync<Order>(
                DataFiles.Orders,
                ct);

        return orders
            .Where(order => order.Status == status)
            .ToList();
    }

    public async Task SaveAsync(
        Order order,
        CancellationToken ct)
    {
        Log.Info($"OrderRepository -> Opening connection to save order #{order.Id}...");
        await Task.Delay(200, ct);

        Log.Info($"OrderRepository -> Beginning transaction for order #{order.Id}...");
        await Task.Delay(100, ct);

        Log.Info($"OrderRepository -> Inserting or updating order #{order.Id}...");
        await Task.Delay(400, ct);

        if (_failSaveForOrderIds.Contains(order.Id) && order.Status == OrderStatus.Completed)
        {
            Log.Info($"OrderRepository -> Committing transaction for order #{order.Id}...");
            await Task.Delay(200, ct);
            throw new DatabaseWriteException("Connection lost during commit");
        }

        await _store.UpdateAsync<Order>(
            DataFiles.Orders,
            orders =>
            {
                int index = orders.FindIndex(
                    existing => existing.Id == order.Id);

                if (index == -1)
                {
                    orders.Add(order);
                }
                else
                {
                    orders[index] = order;
                }

                return orders;
            },
            ct);

        Log.Info($"OrderRepository -> Committing transaction for order #{order.Id}...");
        await Task.Delay(200, ct);
    }
}