namespace HN_Nexus.WebPOS.Models;

public class ProductLot
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int BranchId { get; set; }
    public Branch? Branch { get; set; }

    public string LotNumber { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

