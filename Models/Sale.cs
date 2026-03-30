namespace HN_Nexus.WebPOS.Models;

public class Sale
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public string Status { get; set; } = "Completed";
    public bool IsInvoice { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public List<SaleDetail> Details { get; set; } = new();
}
