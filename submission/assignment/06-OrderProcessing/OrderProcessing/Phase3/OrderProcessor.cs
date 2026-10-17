using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase3;

public class OrderProcessor
{
    private readonly CustomerService _customerService;
    private readonly InventoryService _inventoryService;
    private readonly PricingService _pricingService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly OrderRepository _orderRepository;
    private readonly IEmailService _emailService;

    public OrderProcessor(
        CustomerService customerService,
        InventoryService inventoryService,
        PricingService pricingService,
        IPaymentGateway paymentGateway,
        OrderRepository orderRepository,
        IEmailService emailService)
    {
        _customerService = customerService;
        _inventoryService = inventoryService;
        _pricingService = pricingService;
        _paymentGateway = paymentGateway;
        _orderRepository = orderRepository;
        _emailService = emailService;
    }

    public async Task<bool> ProcessOrderAsync(Order order, CancellationToken ct)
    {
        bool stockReserved = false;
        Customer? customer = null;
        var stepStopwatch = Stopwatch.StartNew();

        try
        {
            ct.ThrowIfCancellationRequested();

            // ==========================================
            // Step 1: Customer Validation
            // ==========================================
            stepStopwatch.Restart();
            try
            {
                customer = await _customerService.GetCustomerAsync(order.CustomerId, ct);
            }
            catch (CustomerNotFoundException ex)
            {
                stepStopwatch.Stop();
                order.Status = OrderStatus.Rejected;
                order.FailureReason = ex.Message;
                await _orderRepository.SaveAsync(order, ct);
                Log.Info($"Order #{order.Id} -> Step 1/6 Customer ... REJECTED ({ex.Message}) in {stepStopwatch.ElapsedMilliseconds} ms");
                return false;
            }

            if (!customer.IsActive)
            {
                stepStopwatch.Stop();
                order.Status = OrderStatus.Rejected;
                order.FailureReason = "Customer is inactive";
                await _orderRepository.SaveAsync(order, ct);
                Log.Info($"Order #{order.Id} -> Step 1/6 Customer ... REJECTED (Customer is inactive) in {stepStopwatch.ElapsedMilliseconds} ms");
                return false;
            }

            stepStopwatch.Stop();
            Log.Info($"Order #{order.Id} -> Step 1/6 Customer ... OK ({customer.Name}) in {stepStopwatch.ElapsedMilliseconds} ms");

            // ==========================================
            // Step 2: Reserve Stock
            // ==========================================
            ct.ThrowIfCancellationRequested();
            stepStopwatch.Restart();
            stockReserved = await _inventoryService.ReserveStockAsync(order, ct);
            stepStopwatch.Stop();

            if (!stockReserved)
            {
                order.Status = OrderStatus.Rejected;
                // failureReason set by InventoryService
                await _orderRepository.SaveAsync(order, ct);
                Log.Info($"Order #{order.Id} -> Step 2/6 Stock ... REJECTED ({order.FailureReason}) in {stepStopwatch.ElapsedMilliseconds} ms");
                return false;
            }

            Log.Info($"Order #{order.Id} -> Step 2/6 Stock ... OK in {stepStopwatch.ElapsedMilliseconds} ms");

            // ==========================================
            // Step 3: Calculate Pricing / Coupons
            // ==========================================
            ct.ThrowIfCancellationRequested();
            stepStopwatch.Restart();
            try
            {
                await _pricingService.CalculateTotalAsync(order, ct);
            }
            catch (InvalidCouponException ex)
            {
                stepStopwatch.Stop();
                await _inventoryService.ReleaseStockAsync(order, ct);
                stockReserved = false;

                order.Status = OrderStatus.Rejected;
                order.FailureReason = ex.Message;
                await _orderRepository.SaveAsync(order, ct);
                Log.Info($"Order #{order.Id} -> Step 3/6 Pricing ... REJECTED ({ex.Message}) in {stepStopwatch.ElapsedMilliseconds} ms");
                return false;
            }

            stepStopwatch.Stop();
            Log.Info($"Order #{order.Id} -> Step 3/6 Pricing ... OK (Total: {order.TotalAmount:F2}) in {stepStopwatch.ElapsedMilliseconds} ms");

            // ==========================================
            // Step 4: Process Payment (with 3-second timeout)
            // ==========================================
            ct.ThrowIfCancellationRequested();
            stepStopwatch.Restart();

            using var paymentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            paymentCts.CancelAfter(3000);

            PaymentResult paymentResult;
            try
            {
                paymentResult = await _paymentGateway.ProcessPaymentAsync(order.Id, order.TotalAmount, paymentCts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Global cancellation requested
                throw;
            }
            catch (OperationCanceledException)
            {
                // Payment timeout (3 seconds)
                stepStopwatch.Stop();
                await _inventoryService.ReleaseStockAsync(order, ct);
                stockReserved = false;

                order.Status = OrderStatus.PaymentFailed;
                order.FailureReason = "Payment gateway timeout (3s)";
                await _orderRepository.SaveAsync(order, ct);
                Log.Info($"Order #{order.Id} -> Step 4/6 Payment ... TIMEOUT after {stepStopwatch.ElapsedMilliseconds} ms, released");
                return false;
            }

            stepStopwatch.Stop();

            if (!paymentResult.Success)
            {
                await _inventoryService.ReleaseStockAsync(order, ct);
                stockReserved = false;

                order.Status = OrderStatus.PaymentFailed;
                order.FailureReason = paymentResult.Message ?? "Card declined by issuer";
                await _orderRepository.SaveAsync(order, ct);
                Log.Info($"Order #{order.Id} -> Step 4/6 Payment ... DECLINED ({order.FailureReason}) in {stepStopwatch.ElapsedMilliseconds} ms");
                return false;
            }

            order.TransactionId = paymentResult.TransactionId;
            Log.Info($"Order #{order.Id} -> Step 4/6 Payment ... OK ({order.TransactionId}) in {stepStopwatch.ElapsedMilliseconds} ms");

            // ==========================================
            // Step 5: Save Completed Order (with compensation)
            // ==========================================
            ct.ThrowIfCancellationRequested();
            stepStopwatch.Restart();
            order.Status = OrderStatus.Completed;
            order.FailureReason = null;

            try
            {
                await _orderRepository.SaveAsync(order, ct);
            }
            catch (DatabaseWriteException)
            {
                stepStopwatch.Stop();
                Log.Error($"Order #{order.Id} -> Step 5/6 Save ... FAILED, compensating...");

                // Compensate: refund payment and release stock
                await _paymentGateway.RefundAsync(order.TransactionId!, ct);
                await _inventoryService.ReleaseStockAsync(order, ct);
                stockReserved = false;

                order.Status = OrderStatus.Failed;
                order.FailureReason = $"Database save failed after payment; payment {order.TransactionId} refunded";
                await _orderRepository.SaveAsync(order, ct);

                Log.Info($"Order #{order.Id} -> Compensated: refunded and released in {stepStopwatch.ElapsedMilliseconds} ms");
                return false;
            }

            stepStopwatch.Stop();
            Log.Info($"Order #{order.Id} -> Step 5/6 Save ... OK in {stepStopwatch.ElapsedMilliseconds} ms");

            // ==========================================
            // Step 6: Confirmation Email (with retry)
            // ==========================================
            ct.ThrowIfCancellationRequested();
            stepStopwatch.Restart();
            await _emailService.SendConfirmationWithRetryAsync(order, customer, ct);
            stepStopwatch.Stop();
            Log.Info($"Order #{order.Id} -> Step 6/6 Email ... finished in {stepStopwatch.ElapsedMilliseconds} ms");

            // Step 7: Return true only if completed
            return order.Status == OrderStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            // Requirement 6.6: cancellation handling
            if (stockReserved)
            {
                await _inventoryService.ReleaseStockAsync(order, CancellationToken.None);
            }

            if (!string.IsNullOrEmpty(order.TransactionId))
            {
                await _paymentGateway.RefundAsync(order.TransactionId, CancellationToken.None);
            }

            order.Status = OrderStatus.Pending;
            order.FailureReason = "Cancelled";
            await _orderRepository.SaveAsync(order, CancellationToken.None);

            Log.Info($"Order #{order.Id} -> Cancelled and rolled back.");
            return false;
        }
    }
}
