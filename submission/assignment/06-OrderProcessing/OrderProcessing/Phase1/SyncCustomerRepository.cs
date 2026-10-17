using System;
using System.Collections.Generic;
using System.Threading;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public class SyncCustomerRepository
{
    private readonly JsonFileStore _store;

    public SyncCustomerRepository(JsonFileStore store)
    {
        _store = store;
    }

    public List<Customer> GetAll()
    {
        Log.Info("SyncCustomerRepository -> Opening connection...");
        Thread.Sleep(200);

        Log.Info("SyncCustomerRepository -> Executing query to get all customers...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        return _store.Read<Customer>(DataFiles.Customers);
    }

    public Customer? GetById(int id)
    {
        Log.Info($"SyncCustomerRepository -> Opening connection for customer #{id}...");
        Thread.Sleep(200);

        Log.Info($"SyncCustomerRepository -> Executing query for customer #{id}...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        List<Customer> customers =
            _store.Read<Customer>(DataFiles.Customers);

        return customers.Find(customer => customer.Id == id);
    }
}