using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderProcessing.Shared.Models
{
    public class PaymentTransaction
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; }
        public string? TransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
