
using System.Diagnostics;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase2;

public class OrderPricer
{
    private readonly PricingService _pricingService;

    public OrderPricer(
        IEnumerable<int>? failShippingForOrderIds = null)
    {
        _pricingService = new PricingService(
            failShippingForOrderIds ?? Array.Empty<int>());
    }

    public async Task PriceOrderAsync(
        Order order,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await _pricingService.CalculateSubTotalAsync(order, ct);

        // Start both operations before awaiting either one.
        Task<decimal> discountTask =
            _pricingService.GetDiscountAsync(order, ct);

        Task<decimal> shippingTask =
            _pricingService.CalculateShippingCostAsync(order, ct);

        // Await individually; do not use Task.WhenAll.
        order.Discount = await discountTask;
        order.ShippingCost = await shippingTask;

        order.TotalAmount = Math.Round(
            order.SubTotal - order.Discount + order.ShippingCost,
            2,
            MidpointRounding.AwayFromZero);

        Log.Success(
            $"OrderPricer -> Order #{order.Id}: Total = {order.TotalAmount:F2}");
    }

    public async Task PriceOrderSequentialAsync(
        Order order,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await _pricingService.CalculateSubTotalAsync(order, ct);

        order.Discount =
            await _pricingService.GetDiscountAsync(order, ct);

        order.ShippingCost =
            await _pricingService.CalculateShippingCostAsync(order, ct);

        order.TotalAmount = Math.Round(
            order.SubTotal - order.Discount + order.ShippingCost,
            2,
            MidpointRounding.AwayFromZero);

        Log.Success(
            $"OrderPricer -> Sequential total for Order #{order.Id}: {order.TotalAmount:F2}");
    }
}
