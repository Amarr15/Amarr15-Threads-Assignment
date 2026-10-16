using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public class PaymentGateway : IPaymentGateway
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _timeoutOrderIds;
    private readonly HashSet<int> _declineOrderIds;

    public PaymentGateway(
        JsonFileStore? store = null,
        IEnumerable<int>? timeoutOrderIds = null,
        IEnumerable<int>? declineOrderIds = null)
    {
        _store = store ?? new JsonFileStore();
        _timeoutOrderIds = new HashSet<int>(timeoutOrderIds ?? Enumerable.Empty<int>());
        _declineOrderIds = new HashSet<int>(declineOrderIds ?? Enumerable.Empty<int>());
    }

    public async Task<PaymentResult> ProcessPaymentAsync(
        int orderId,
        decimal amount,
        CancellationToken ct)
    {
        Log.Info($"PaymentGateway -> Authorizing payment of {amount:F2} for Order #{orderId}...");
        await Task.Delay(800, ct);

        Log.Info($"PaymentGateway -> Fraud check for Order #{orderId}...");
        await Task.Delay(500, ct);

        string txnId = "TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpper();

        if (_declineOrderIds.Contains(orderId))
        {
            Log.Error($"PaymentGateway -> Payment declined for Order #{orderId}.");

            var declinedTxn = new PaymentTransaction
            {
                OrderId = orderId,
                Amount = amount,
                Status = PaymentStatus.Declined,
                TransactionId = txnId,
                CreatedAt = DateTime.Now
            };

            await _store.UpdateAsync<PaymentTransaction>(
                DataFiles.Payments,
                list =>
                {
                    list.Add(declinedTxn);
                    return list;
                },
                ct);

            return new PaymentResult
            {
                Success = false,
                TransactionId = txnId,
                Status = PaymentStatus.Declined,
                Message = "Card declined by issuer"
            };
        }

        int captureDelay = _timeoutOrderIds.Contains(orderId) ? 5000 : 700;

        Log.Info($"PaymentGateway -> Capturing payment for Order #{orderId} (delay: {captureDelay} ms)...");
        await Task.Delay(captureDelay, ct);

        var capturedTxn = new PaymentTransaction
        {
            OrderId = orderId,
            Amount = amount,
            Status = PaymentStatus.Captured,
            TransactionId = txnId,
            CreatedAt = DateTime.Now
        };

        await _store.UpdateAsync<PaymentTransaction>(
            DataFiles.Payments,
            list =>
            {
                list.Add(capturedTxn);
                return list;
            },
            ct);

        Log.Success($"PaymentGateway -> Captured payment {txnId} for Order #{orderId}.");

        return new PaymentResult
        {
            Success = true,
            TransactionId = txnId,
            Status = PaymentStatus.Captured,
            Message = null
        };
    }

    public async Task RefundAsync(string transactionId, CancellationToken ct)
    {
        Log.Info($"PaymentGateway -> External API refund DNS/handshake for {transactionId}...");
        await Task.Delay(150, ct);

        Log.Info($"PaymentGateway -> Sending refund request for {transactionId}...");
        await Task.Delay(300, ct);

        Log.Info($"PaymentGateway -> Waiting for refund response for {transactionId}...");
        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        await _store.UpdateAsync<PaymentTransaction>(
            DataFiles.Payments,
            list =>
            {
                var txn = list.Find(t => t.TransactionId == transactionId);
                if (txn is not null)
                {
                    txn.Status = PaymentStatus.Refunded;
                }
                return list;
            },
            ct);

        Log.Success($"PaymentGateway -> Refunded transaction {transactionId}.");
    }
}
