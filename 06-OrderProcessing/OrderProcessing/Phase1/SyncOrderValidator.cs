using System;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase1;

public class SyncOrderValidator
{
    private readonly SyncCustomerRepository _customers;
    private readonly SyncProductRepository _products;
    private readonly SyncCouponRepository _coupons;

    public SyncOrderValidator(
        SyncCustomerRepository customers,
        SyncProductRepository products,
        SyncCouponRepository coupons)
    {
        _customers = customers;
        _products = products;
        _coupons = coupons;
    }

    public bool Validate(Order order)
    {
        var customer = _customers.GetById(order.CustomerId);

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
            var product = _products.GetById(item.ProductId);

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
            var coupon = _coupons.GetByCode(order.CouponCode);

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