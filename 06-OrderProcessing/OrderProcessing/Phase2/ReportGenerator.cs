using System.Text.Json.Serialization;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public class ReportGenerator
{
    private readonly OrderRepository _orderRepository;
    private readonly ProductRepository _productRepository;
    private readonly JsonFileStore _store;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    public ReportGenerator()
    {
        _store = new JsonFileStore();
        _orderRepository = new OrderRepository(_store);
        _productRepository = new ProductRepository(_store);
    }

    public async Task<Report> GenerateReportAsync(
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        Log.Info("ReportGenerator -> Starting report generation.");

        // Load orders and products concurrently.
        Task<List<Order>> ordersTask =
            _orderRepository.GetAllAsync(ct);

        Task<List<Product>> productsTask =
            _productRepository.GetAllAsync(ct);

        // Await each task individually.
        var orders = await ordersTask;
        var products = await productsTask;

        var completedOrders = orders
            .Where(order => order.Status == OrderStatus.Completed)
            .ToList();

        decimal totalRevenue = completedOrders.Sum(
            order => order.TotalAmount);

        decimal averageOrderValue = completedOrders.Count > 0
            ? totalRevenue / completedOrders.Count
            : 0m;

        var topSellingProduct = completedOrders
            .SelectMany(order => order.Items)
            .GroupBy(item => item.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                QuantitySold = group.Sum(item => item.Quantity)
            })
            .OrderByDescending(product => product.QuantitySold)
            .ThenBy(product => product.ProductId)
            .FirstOrDefault();

        var report = new Report
        {
            TotalOrders = orders.Count,
            CompletedCount = completedOrders.Count,
            RejectedCount = orders.Count(
                order => order.Status == OrderStatus.Rejected),
            FailedCount = orders.Count(
                order => order.Status == OrderStatus.Failed),
            PaymentFailedCount = orders.Count(
                order => order.Status == OrderStatus.PaymentFailed),
            TotalRevenue = totalRevenue,
            AverageOrderValue = Math.Round(
                averageOrderValue, 2),
            TopSellingProduct = products.Find(
                p => p.Id == (topSellingProduct?.ProductId ?? 0))?.Name ?? "Unknown",
            TopSellingQuantity = topSellingProduct?.QuantitySold ?? 0,
            GeneratedAt = DateTime.Now
        };

        string reportDirectory = Path.Combine(
            DataFiles.Root,
            "reports");

        Directory.CreateDirectory(reportDirectory);

        string reportPath = Path.Combine(
            reportDirectory,
            $"report_{DateTime.Now:yyyyMMdd_HHmmss}.json");

        await using var stream = new FileStream(
            reportPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            4096,
            useAsync: true);

        await JsonSerializer.SerializeAsync(
            stream,
            report,
            JsonOptions,
            ct);

        Log.Success(
            $"ReportGenerator -> Report saved to {reportPath}");


        Log.Info(
            $"ReportGenerator -> Top selling product: " +
            $"{report.TopSellingProduct}, " +
            $"quantity: {report.TopSellingQuantity}");


        return report;
    }
}