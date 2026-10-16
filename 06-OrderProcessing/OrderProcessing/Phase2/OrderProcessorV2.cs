
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public class OrderProcessorV2
{
    private readonly OrderPricer _orderPricer;
    private readonly InvoiceGenerator _invoiceGenerator;
    private readonly ProductRepository _productRepository;
    private readonly OrderRepository _orderRepository;

    public OrderProcessorV2()
    {
        var store = new JsonFileStore();

        _orderPricer = new OrderPricer(new[] { 7 });
        _invoiceGenerator = new InvoiceGenerator();
        _productRepository = new ProductRepository(store);
        _orderRepository = new OrderRepository(store);
    }

    public async Task<bool> ProcessValidatedOrderAsync(
        Order order,
        CancellationToken ct)
    {
        var reservedItems = new List<OrderItem>();

        try
        {
            ct.ThrowIfCancellationRequested();

            Log.Info(
                $"OrderProcessorV2 -> Processing Order #{order.Id}.");

            var products = await _productRepository.GetAllAsync(ct);

            foreach (var item in order.Items)
            {
                var product = products.Find(p => p.Id == item.ProductId);

                if (product is null ||
                    !await _productRepository.ReserveStockAsync(
                        item.ProductId, item.Quantity, ct))
                {
                    order.Status = OrderStatus.Rejected;
                    order.FailureReason =
                        $"Insufficient stock for '{product?.Name ?? $"Product #{item.ProductId}"}'";

                    await ReleaseReservedStockAsync(reservedItems, ct);
                    await _orderRepository.SaveAsync(order, ct);

                    Log.Info(
                        $"OrderProcessorV2 -> Order #{order.Id} rejected: {order.FailureReason}");

                    return false;
                }

                reservedItems.Add(item);
            }

            await _orderPricer.PriceOrderAsync(order, ct);

            order.Status = OrderStatus.Completed;
            order.FailureReason = null;

            await _invoiceGenerator.GenerateAsync(order, ct);

            await _orderRepository.SaveAsync(order, ct);

            Log.Success(
                $"OrderProcessorV2 -> Order #{order.Id} completed.");

            return true;
        }
        catch
        {
            await ReleaseReservedStockAsync(reservedItems, ct);
            throw;
        }
    }

    private async Task ReleaseReservedStockAsync(
        List<OrderItem> reservedItems,
        CancellationToken ct)
    {
        foreach (var item in reservedItems)
        {
            await _productRepository.ReleaseStockAsync(
                item.ProductId, item.Quantity, ct);
        }

        reservedItems.Clear();
    }
}
