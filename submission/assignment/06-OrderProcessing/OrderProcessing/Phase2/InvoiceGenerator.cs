
using System.Text;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public class InvoiceGenerator
{
    public async Task<string> GenerateAsync(
        Order order,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        Log.Info($"InvoiceGenerator -> Generating invoice for Order #{order.Id}.");

        // CPU-bound work: run on a thread-pool thread
        long checksum = await Task.Run(() =>
        {
            long result = 0;

            for (int i = 0; i < 20_000_000; i++)
            {
                ct.ThrowIfCancellationRequested();
                result += i % 7;
            }

            return result;
        }, ct);

        var invoice = new StringBuilder();
        invoice.AppendLine($"===== INVOICE #{order.Id} =====");
        invoice.AppendLine($"Customer ID: {order.CustomerId}");
        invoice.AppendLine($"Created At: {order.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        invoice.AppendLine("--------------------------------");
        invoice.AppendLine($"Subtotal:      {order.SubTotal:F2}");
        invoice.AppendLine($"Discount:      {order.Discount:F2}");
        invoice.AppendLine($"Shipping:      {order.ShippingCost:F2}");
        invoice.AppendLine("--------------------------------");
        invoice.AppendLine($"TOTAL:         {order.TotalAmount:F2}");
        invoice.AppendLine($"Status:        {order.Status}");
        invoice.AppendLine($"Checksum:      {checksum}");
        invoice.AppendLine("================================");

        string invoiceDirectory = Path.Combine(DataFiles.Root, "invoices");
        Directory.CreateDirectory(invoiceDirectory);

        string invoicePath = Path.Combine(
            invoiceDirectory,
            $"invoice_{order.Id}.txt");

        await File.WriteAllTextAsync(
            invoicePath,
            invoice.ToString(),
            ct);

        Log.Success(
            $"InvoiceGenerator -> Invoice saved for Order #{order.Id}.");

        return invoice.ToString();
    }
}