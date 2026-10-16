using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public class CustomerRepository
{
    private readonly JsonFileStore _store;

    public CustomerRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<List<Customer>> GetAllAsync(
        CancellationToken ct)
    {
        Log.Info("CustomerRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("CustomerRepository -> Executing query to get all customers...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        return await _store.ReadAsync<Customer>(
            DataFiles.Customers,
            ct);
    }

    public async Task<Customer?> GetByIdAsync(
        int id,
        CancellationToken ct)
    {
        Log.Info($"CustomerRepository -> Opening connection to get customer #{id}...");
        await Task.Delay(200, ct);

        Log.Info($"CustomerRepository -> Executing query for customer #{id}...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Customer> customers =
            await _store.ReadAsync<Customer>(
                DataFiles.Customers,
                ct);

        return customers.Find(customer => customer.Id == id);
    }
}