using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public class IncomingOrderRepository
{
    private readonly JsonFileStore _store;

    public IncomingOrderRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<List<Order>> GetAllAsync(
        CancellationToken ct)
    {
        Log.Info("IncomingOrderRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("IncomingOrderRepository -> Executing query to get incoming orders...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        return await _store.ReadAsync<Order>(
            DataFiles.IncomingOrders,
            ct);
    }
}