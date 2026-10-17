
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Shared.Storage;

public class DataSeeder
{
    private readonly JsonFileStore _store = new();

    public async Task ResetAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (Directory.Exists(DataFiles.Root))
        {
            Directory.Delete(DataFiles.Root, recursive: true);
        }

        Directory.CreateDirectory(DataFiles.Root);

        var customers = new List<Customer>
        {
            new() { Id = 1, Name = "Ahmed Ali", Email = "ahmed.ali@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Gold },
            new() { Id = 2, Name = "Sara Mohamed", Email = "sara.mohamed@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Silver },
            new() { Id = 3, Name = "Omar Hassan", Email = "omar.hassan@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Bronze },
            new() { Id = 4, Name = "Mona Ibrahim", Email = "mona.ibrahim@example.com", IsActive = false, LoyaltyTier = LoyaltyTier.Silver },
            new() { Id = 5, Name = "Youssef Adel", Email = "youssef.adel@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Gold },
            new() { Id = 6, Name = "Nour Khaled", Email = "nour.khaled@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Bronze },
            new() { Id = 7, Name = "Karim Samir", Email = "karim.samir@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Silver },
            new() { Id = 8, Name = "Laila Mahmoud", Email = "laila.mahmoud@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Gold },
            new() { Id = 9, Name = "Hassan Tarek", Email = "hassan.tarek@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Bronze },
            new() { Id = 10, Name = "Dina Fathy", Email = "dina.fathy@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Silver }
        };

        var products = new List<Product>
        {
            new() { Id = 1, Name = "Laptop", Price = 1200.00m, StockQuantity = 10, WeightKg = 2.2m },
            new() { Id = 2, Name = "Smartphone", Price = 800.00m, StockQuantity = 15, WeightKg = 0.4m },
            new() { Id = 3, Name = "Wireless Mouse", Price = 25.00m, StockQuantity = 2, WeightKg = 0.1m },
            new() { Id = 4, Name = "Mechanical Keyboard", Price = 90.00m, StockQuantity = 1, WeightKg = 0.9m },
            new() { Id = 5, Name = "USB-C Hub", Price = 45.00m, StockQuantity = 30, WeightKg = 0.2m },
            new() { Id = 6, Name = "Headphones", Price = 150.00m, StockQuantity = 12, WeightKg = 0.3m },
            new() { Id = 7, Name = "27\" Monitor", Price = 350.00m, StockQuantity = 6, WeightKg = 5.0m },
            new() { Id = 8, Name = "Webcam", Price = 70.00m, StockQuantity = 20, WeightKg = 0.2m }
        };

        DateTime today = DateTime.Today;

        var coupons = new List<Coupon>
        {
            new() { Code = "SAVE10", DiscountPercent = 10, ExpiryDate = today.AddDays(180) },
            new() { Code = "WELCOME15", DiscountPercent = 15, ExpiryDate = today.AddDays(90) },
            new() { Code = "FLASH20", DiscountPercent = 20, ExpiryDate = today.AddDays(7) },
            new() { Code = "OLD20", DiscountPercent = 20, ExpiryDate = today.AddDays(-30) }
        };

        var incomingOrders = new List<Order>
        {
            CreateOrder(1, 1, "SAVE10", (1, 1), (3, 1)),
            CreateOrder(2, 2, null, (2, 1)),
            CreateOrder(3, 3, "WELCOME15", (5, 2), (8, 1)),
            CreateOrder(4, 4, null, (6, 1)),
            CreateOrder(5, 5, null, (7, 1)),
            CreateOrder(6, 6, "FLASH20", (5, 1)),
            CreateOrder(7, 7, null, (6, 1), (8, 1)),
            CreateOrder(8, 8, "SAVE10", (1, 1)),
            CreateOrder(9, 9, null, (3, 5)),
            CreateOrder(10, 10, null, (4, 1)),
            CreateOrder(11, 1, null, (4, 1)),
            CreateOrder(12, 2, "OLD20", (2, 1)),
            CreateOrder(13, 3, null, (6, 2)),
            CreateOrder(14, 5, "WELCOME15", (8, 2), (5, 1)),
            CreateOrder(15, 99, null, (2, 1)),
            CreateOrder(16, 6, null, (7, 1), (5, 1)),
            CreateOrder(17, 7, "SAVE10", (2, 1)),
            CreateOrder(18, 8, "FAKE50", (6, 1)),
            CreateOrder(19, 9, null, (8, 1)),
            CreateOrder(20, 10, "FLASH20", (1, 1), (6, 1))
        };

        await WriteAndLogAsync(DataFiles.Customers, customers, ct);
        await WriteAndLogAsync(DataFiles.Products, products, ct);
        await WriteAndLogAsync(DataFiles.Coupons, coupons, ct);
        await WriteAndLogAsync(DataFiles.IncomingOrders, incomingOrders, ct);

        await WriteAndLogAsync(DataFiles.Orders, new List<Order>(), ct);
        await WriteAndLogAsync(DataFiles.Payments, new List<PaymentTransaction>(), ct);
        await WriteAndLogAsync(DataFiles.Emails, new List<EmailMessage>(), ct);
        await WriteAndLogAsync(DataFiles.EmailRetry, new List<EmailMessage>(), ct);
    }

    private async Task WriteAndLogAsync<T>(
        string fileName,
        List<T> records,
        CancellationToken ct)
    {
        await _store.WriteAsync(fileName, records, ct);
        Console.WriteLine($"[Seeder] {fileName} -> {records.Count} records");
    }

    private static Order CreateOrder(
        int id,
        int customerId,
        string? couponCode,
        (int ProductId, int Quantity) firstItem,
        (int ProductId, int Quantity)? secondItem = null)
    {
        var items = new List<OrderItem>
        {
            new()
            {
                ProductId = firstItem.ProductId,
                Quantity = firstItem.Quantity,
                UnitPrice = 0m
            }
        };

        if (secondItem.HasValue)
        {
            items.Add(new OrderItem
            {
                ProductId = secondItem.Value.ProductId,
                Quantity = secondItem.Value.Quantity,
                UnitPrice = 0m
            });
        }

        return new Order
        {
            Id = id,
            CustomerId = customerId,
            Items = items,
            CouponCode = couponCode,
            SubTotal = 0m,
            Discount = 0m,
            ShippingCost = 0m,
            TotalAmount = 0m,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.Now,
            FailureReason = null,
            TransactionId = null
        };
    }
}
