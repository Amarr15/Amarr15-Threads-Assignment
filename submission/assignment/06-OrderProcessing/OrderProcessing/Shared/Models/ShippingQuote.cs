
namespace OrderProcessing.Shared.Models;

public class ShippingQuote
{
    public string Carrier { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public double ElapsedMs { get; set; }
}
