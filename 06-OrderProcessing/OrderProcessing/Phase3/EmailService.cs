using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public class EmailService : IEmailService
{
    private readonly JsonFileStore _store;
    private readonly Dictionary<int, int> _failAttempts;
    private readonly ConcurrentDictionary<int, int> _attemptCounts = new();

    public EmailService(
        JsonFileStore? store = null,
        IDictionary<int, int>? failAttempts = null)
    {
        _store = store ?? new JsonFileStore();
        _failAttempts = failAttempts is not null
            ? new Dictionary<int, int>(failAttempts)
            : new Dictionary<int, int>();
    }

    public async Task SendConfirmationAsync(
        Order order,
        Customer customer,
        CancellationToken ct)
    {
        Log.Info($"EmailService -> Rendering email template for Order #{order.Id}...");
        await Task.Delay(200, ct);

        Log.Info($"EmailService -> Connecting to SMTP server for Order #{order.Id}...");
        await Task.Delay(300, ct);

        int currentAttempt = _attemptCounts.AddOrUpdate(order.Id, 1, (_, count) => count + 1);

        if (_failAttempts.TryGetValue(order.Id, out int maxFails) && currentAttempt <= maxFails)
        {
            Log.Error($"EmailService -> SMTP connection failed for Order #{order.Id} (attempt {currentAttempt}).");
            throw new SmtpUnavailableException("SMTP server unavailable");
        }

        Log.Info($"EmailService -> Sending email to {customer.Email} for Order #{order.Id}...");
        await Task.Delay(500, ct);

        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order Confirmation #{order.Id}",
            Body = $"Dear {customer.Name}, your order total is {order.TotalAmount:F2}.",
            SentAt = DateTime.Now,
            Attempts = currentAttempt,
            LastError = null
        };

        await _store.UpdateAsync<EmailMessage>(
            DataFiles.Emails,
            list =>
            {
                list.Add(message);
                return list;
            },
            ct);

        Log.Success($"EmailService -> Confirmation email sent for Order #{order.Id} on attempt {currentAttempt}.");
    }

    public async Task SendConfirmationWithRetryAsync(
        Order order,
        Customer customer,
        CancellationToken ct)
    {
        int[] delays = { 0, 500, 1000, 2000 };
        string? lastError = null;
        int attempt = 0;

        for (int i = 0; i < delays.Length; i++)
        {
            attempt = i + 1;

            if (delays[i] > 0)
            {
                Log.Info($"EmailService -> Waiting {delays[i]} ms before retry #{attempt} for Order #{order.Id}...");
                await Task.Delay(delays[i], ct);
            }

            try
            {
                await SendConfirmationAsync(order, customer, ct);
                return; // Successfully sent
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                Log.Error($"EmailService -> Attempt {attempt} failed for Order #{order.Id}: {ex.Message}");
            }
        }

        // All 4 attempts failed
        var failedMessage = new EmailMessage
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order Confirmation #{order.Id}",
            Body = $"Dear {customer.Name}, your order total is {order.TotalAmount:F2}.",
            SentAt = DateTime.Now,
            Attempts = attempt,
            LastError = lastError
        };

        await _store.UpdateAsync<EmailMessage>(
            DataFiles.EmailRetry,
            list =>
            {
                list.Add(failedMessage);
                return list;
            },
            ct);

        Log.Info($"Email for Order #{order.Id} queued for retry later");
    }
}
