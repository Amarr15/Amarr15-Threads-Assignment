using System;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public class FireAndForgetDemo
{
    private readonly OrderRepository _orderRepository;
    private readonly IEmailService _emailService;

    public FireAndForgetDemo(OrderRepository orderRepository, IEmailService emailService)
    {
        _orderRepository = orderRepository;
        _emailService = emailService;
    }

    // Broken method exactly as specified in the assignment
#pragma warning disable CS4014
    public async Task ProcessOrderBrokenAsync(Order order, Customer customer)
    {
        await _orderRepository.SaveAsync(order, CancellationToken.None);
        _emailService.SendConfirmationAsync(order, customer, CancellationToken.None); // not awaited!
        Console.WriteLine("[FireAndForget] Order processing finished (caller returned without waiting for email)");
    }
#pragma warning restore CS4014

    // Fixed method
    public async Task ProcessOrderFixedAsync(Order order, Customer customer)
    {
        await _orderRepository.SaveAsync(order, CancellationToken.None);
        await _emailService.SendConfirmationAsync(order, customer, CancellationToken.None);
        Console.WriteLine("[FireAndForget] Order processing finished (email guaranteed sent before return)");
    }

    public async Task RunDemoAsync(CancellationToken ct)
    {
        Console.WriteLine("\n===== Task 9.7: Fire-and-Forget Investigation =====");

        var order = new Order
        {
            Id = 999,
            CustomerId = 1,
            TotalAmount = 100m,
            Status = OrderStatus.Completed,
            CreatedAt = DateTime.Now
        };

        var customer = new Customer
        {
            Id = 1,
            Name = "Ahmed Ali",
            Email = "ahmed.ali@example.com",
            IsActive = true
        };

        Console.WriteLine("--- Running Broken Version (not awaited) ---");
        await ProcessOrderBrokenAsync(order, customer);

        // Small delay to allow the background task to demonstrate unobserved completion or race
        await Task.Delay(1500, ct);

        Console.WriteLine("\n--- Running Fixed Version (properly awaited) ---");
        await ProcessOrderFixedAsync(order, customer);

        Console.WriteLine("===================================================\n");
    }
}
