using System;
using System.Threading;
using System.Threading.Tasks;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public class CustomerService
{
    private readonly CustomerRepository _repository;

    public CustomerService(CustomerRepository repository)
    {
        _repository = repository;
    }

    public async Task<Customer> GetCustomerAsync(int id, CancellationToken ct)
    {
        var customer = await _repository.GetByIdAsync(id, ct);

        if (customer is null)
        {
            throw new CustomerNotFoundException($"Customer #{id} not found");
        }

        return customer;
    }
}
