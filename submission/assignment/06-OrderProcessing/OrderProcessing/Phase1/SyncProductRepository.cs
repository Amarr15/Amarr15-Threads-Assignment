using System;
using System.Collections.Generic;
using System.Threading;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public class SyncProductRepository
{
    private readonly JsonFileStore _store;

    public SyncProductRepository(JsonFileStore store)
    {
        _store = store;
    }

    public List<Product> GetAll()
    {
        Log.Info("SyncProductRepository -> Opening connection...");
        Thread.Sleep(200);

        Log.Info("SyncProductRepository -> Executing query to get all products...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        return _store.Read<Product>(DataFiles.Products);
    }

    public Product? GetById(int id)
    {
        Log.Info($"SyncProductRepository -> Opening connection for product #{id}...");
        Thread.Sleep(200);

        Log.Info($"SyncProductRepository -> Executing query for product #{id}...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        List<Product> products =
            _store.Read<Product>(DataFiles.Products);

        return products.Find(product => product.Id == id);
    }
}