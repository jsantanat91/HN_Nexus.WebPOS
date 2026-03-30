namespace HN_Nexus.WebPOS.Models;

public class Sale
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;

    public int BranchId { get; set; }
    public Branch? Branch { get; set; }

    public decimal SubtotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public string PaymentMethod { get; set; } = "Cash";
    public string? AuthorizationCode { get; set; }
    public bool PricesIncludeTax { get; set; }
    public decimal AmountReceived { get; set; }
    public decimal ChangeAmount { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public string Status { get; set; } = "Completed";
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public bool IsInvoice { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public List<SaleDetail> Details { get; set; } = new();
}
