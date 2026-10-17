using System;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase1;

public class OrderValidator
{
    private readonly CustomerRepository _customers;
    private readonly ProductRepository _products;
    private readonly CouponRepository _coupons;

    public OrderValidator(
        CustomerRepository customers,
        ProductRepository products,
        CouponRepository coupons)
    {
        _customers = customers;
        _products = products;
        _coupons = coupons;
    }

    public async Task<bool> ValidateOrderAsync(
        Order order,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var customer = await _customers.GetByIdAsync(
            order.CustomerId, ct);

        if (customer is null)
        {
            order.Status = OrderStatus.Rejected;
            order.FailureReason =
                $"Customer #{order.CustomerId} not found";
            return false;
        }

        if (!customer.IsActive)
        {
            order.Status = OrderStatus.Rejected;
            order.FailureReason = "Customer is inactive";
            return false;
        }

        foreach (var item in order.Items)
        {
            ct.ThrowIfCancellationRequested();

            var product = await _products.GetByIdAsync(
                item.ProductId, ct);

            if (product is null)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason =
                    $"Product #{item.ProductId} not found";
                return false;
            }

            if (product.StockQuantity < item.Quantity)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason =
                    $"Insufficient stock for '{product.Name}'";
                return false;
            }

            item.UnitPrice = product.Price;
        }

        if (!string.IsNullOrWhiteSpace(order.CouponCode))
        {
            var coupon = await _coupons.GetByCodeAsync(
                order.CouponCode, ct);

            if (coupon is null)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason =
                    $"Coupon '{order.CouponCode}' does not exist";
                return false;
            }

            if (coupon.ExpiryDate.Date < DateTime.Today)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason =
                    $"Coupon '{order.CouponCode}' has expired";
                return false;
            }
        }

        order.Status = OrderStatus.Validated;
        order.FailureReason = null;

        return true;
    }
}