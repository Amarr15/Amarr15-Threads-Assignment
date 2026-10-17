using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase3;

public interface IEmailService
{
    Task SendConfirmationAsync(Order order, Customer customer, CancellationToken ct);
    Task SendConfirmationWithRetryAsync(Order order, Customer customer, CancellationToken ct);
}
