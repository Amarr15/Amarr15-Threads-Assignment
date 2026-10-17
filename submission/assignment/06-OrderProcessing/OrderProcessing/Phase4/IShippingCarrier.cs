using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase4;

public interface IShippingCarrier
{
    string Name { get; }
    Task<decimal> GetQuoteAsync(Order order, CancellationToken ct);
}
