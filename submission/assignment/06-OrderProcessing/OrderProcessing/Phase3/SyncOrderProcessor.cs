using System;
using System.Collections.Generic;
using System.Threading;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public class SyncOrderProcessor
{
    private readonly JsonFileStore _store;

    public SyncOrderProcessor(JsonFileStore store)
    {
        _store = store;
    }

    public bool ProcessOrder(Order order)
    {
        // 1. Customer
        Thread.Sleep(200); // open
        Thread.Sleep(400); // query
        var customers = _store.Read<Customer>(DataFiles.Customers);
        var customer = customers.Find(c => c.Id == order.CustomerId);

        if (customer is null)
        {
            order.Status = OrderStatus.Rejected;
            order.FailureReason = $"Customer #{order.CustomerId} not found";
            SaveOrder(order);
            return false;
        }

        if (!customer.IsActive)
        {
            order.Status = OrderStatus.Rejected;
            order.FailureReason = "Customer is inactive";
            SaveOrder(order);
            return false;
        }

        // 2. Stock
        Thread.Sleep(200); // open
        Thread.Sleep(100); // begin
        Thread.Sleep(400); // update
        var products = _store.Read<Product>(DataFiles.Products);
        foreach (var item in order.Items)
        {
            var p = products.Find(x => x.Id == item.ProductId);
            if (p is null || p.StockQuantity < item.Quantity)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = $"Insufficient stock for '{p?.Name ?? $"Product #{item.ProductId}"}'";
                SaveOrder(order);
                Thread.Sleep(200); // commit
                return false;
            }
            p.StockQuantity -= item.Quantity;
            item.UnitPrice = p.Price;
        }
        _store.Write(DataFiles.Products, products);
        Thread.Sleep(200); // commit

        // 3. Pricing
        order.SubTotal = 0;
        foreach (var item in order.Items) order.SubTotal += item.UnitPrice * item.Quantity;
        order.Discount = Math.Round(order.SubTotal * 0.1m, 2);
        order.ShippingCost = 10m;
        order.TotalAmount = order.SubTotal - order.Discount + order.ShippingCost;

        // 4. Payment
        Thread.Sleep(800); // auth
        Thread.Sleep(500); // fraud
        Thread.Sleep(700); // capture
        string txnId = "TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpper();
        order.TransactionId = txnId;

        // 5. Save Completed
        Thread.Sleep(200); // open
        Thread.Sleep(100); // begin
        Thread.Sleep(400); // update
        order.Status = OrderStatus.Completed;
        SaveOrder(order);
        Thread.Sleep(200); // commit

        // 6. Email
        Thread.Sleep(200); // render
        Thread.Sleep(300); // smtp
        Thread.Sleep(500); // send

        return true;
    }

    private void SaveOrder(Order order)
    {
        var orders = _store.Read<Order>(DataFiles.Orders);
        int idx = orders.FindIndex(o => o.Id == order.Id);
        if (idx >= 0) orders[idx] = order;
        else orders.Add(order);
        _store.Write(DataFiles.Orders, orders);
    }
}
