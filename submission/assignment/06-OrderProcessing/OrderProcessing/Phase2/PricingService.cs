
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public class PricingService
{
    private readonly ProductRepository _products;
    private readonly CustomerRepository _customers;
    private readonly CouponRepository _coupons;
    private readonly HashSet<int> _failShippingForOrderIds;

    public PricingService(IEnumerable<int>? failShippingForOrderIds = null)
    {
        var store = new JsonFileStore();

        _products = new ProductRepository(store);
        _customers = new CustomerRepository(store);
        _coupons = new CouponRepository(store);

        _failShippingForOrderIds = new HashSet<int>(
            failShippingForOrderIds ?? Enumerable.Empty<int>());
    }

    public async Task<decimal> CalculateSubTotalAsync(
        Order order,
        CancellationToken ct)
    {
        decimal subtotal = 0m;

        foreach (var item in order.Items)
        {
            ct.ThrowIfCancellationRequested();

            var product = await _products.GetByIdAsync(
                item.ProductId, ct);

            if (product is null)
            {
                throw new InvalidOperationException(
                    $"Product #{item.ProductId} not found.");
            }

            item.UnitPrice = product.Price;

            subtotal += product.Price * item.Quantity;
        }

        order.SubTotal = Math.Round(
            subtotal, 2, MidpointRounding.AwayFromZero);

        Log.Info(
            $"PricingService -> Order #{order.Id}: Subtotal = {order.SubTotal:F2}");

        return order.SubTotal;
    }

    public async Task<decimal> GetDiscountAsync(
        Order order,
        CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(
            order.CustomerId, ct);

        if (customer is null)
        {
            throw new InvalidOperationException(
                $"Customer #{order.CustomerId} not found.");
        }

        decimal loyaltyPercent = customer.LoyaltyTier switch
        {
            LoyaltyTier.Bronze => 0m,
            LoyaltyTier.Silver => 5m,
            LoyaltyTier.Gold => 10m,
            _ => 0m
        };

        decimal couponPercent = 0m;

        if (!string.IsNullOrWhiteSpace(order.CouponCode))
        {
            var coupon = await _coupons.GetByCodeAsync(
                order.CouponCode, ct);

            if (coupon is null)
            {
                throw new InvalidCouponException(
                    $"Coupon '{order.CouponCode}' does not exist");
            }

            if (coupon.ExpiryDate.Date < DateTime.Today)
            {
                throw new InvalidCouponException(
                    $"Coupon '{order.CouponCode}' has expired");
            }

            couponPercent = coupon.DiscountPercent;
        }

        var totalPercent = Math.Min(
            loyaltyPercent + couponPercent, 30m);

        order.Discount = Math.Round(
            order.SubTotal * totalPercent / 100m,
            2,
            MidpointRounding.AwayFromZero);

        Log.Info(
            $"PricingService -> Order #{order.Id}: Discount = {order.Discount:F2}");

        return order.Discount;
    }

    public async Task<decimal> CalculateShippingCostAsync(
        Order order,
        CancellationToken ct)
    {
        Log.Info(
            $"PricingService -> Order #{order.Id}: Shipping DNS/handshake started.");

        await Task.Delay(150, ct);

        Log.Info(
            $"PricingService -> Order #{order.Id}: Shipping handshake completed.");

        if (_failShippingForOrderIds.Contains(order.Id))
        {
            throw new ShippingProviderUnavailableException(
                "Shipping provider unavailable");
        }

        Log.Info(
            $"PricingService -> Order #{order.Id}: Sending shipping request.");

        await Task.Delay(300, ct);

        Log.Info(
            $"PricingService -> Order #{order.Id}: Waiting for shipping response.");

        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        if (order.SubTotal >= 1000m)
        {
            order.ShippingCost = 0m;
        }
        else
        {
            decimal totalWeight = 0m;
            int totalQuantity = 0;

            foreach (var item in order.Items)
            {
                ct.ThrowIfCancellationRequested();

                var product = await _products.GetByIdAsync(
                    item.ProductId, ct);

                if (product is null)
                {
                    throw new InvalidOperationException(
                        $"Product #{item.ProductId} not found.");
                }

                totalWeight += product.WeightKg * item.Quantity;
                totalQuantity += item.Quantity;
            }

            order.ShippingCost = Math.Round(
                5m + (1.5m * totalQuantity) + (2m * totalWeight),
                2,
                MidpointRounding.AwayFromZero);
        }

        Log.Info(
            $"PricingService -> Order #{order.Id}: Shipping = {order.ShippingCost:F2}");

        return order.ShippingCost;
    }

    public async Task<decimal> CalculateTotalAsync(
        Order order,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await CalculateSubTotalAsync(order, ct);

        // Run discount and shipping API concurrently
        var discountTask = GetDiscountAsync(order, ct);
        var shippingTask = CalculateShippingCostAsync(order, ct);

        order.Discount = await discountTask;
        order.ShippingCost = await shippingTask;

        order.TotalAmount = Math.Round(
            order.SubTotal - order.Discount + order.ShippingCost,
            2,
            MidpointRounding.AwayFromZero);

        Log.Success(
            $"PricingService -> Order #{order.Id}: Total calculated = {order.TotalAmount:F2}");

        return order.TotalAmount;
    }
}
