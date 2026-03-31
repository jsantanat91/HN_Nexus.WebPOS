namespace HN_Nexus.WebPOS.Models;

public class StockTransfer
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int FromBranchId { get; set; }
    public Branch? FromBranch { get; set; }
    public int? FromWarehouseId { get; set; }
    public Warehouse? FromWarehouse { get; set; }

    public int ToBranchId { get; set; }
    public Branch? ToBranch { get; set; }
    public int? ToWarehouseId { get; set; }
    public Warehouse? ToWarehouse { get; set; }

    public int Quantity { get; set; }
    public string Notes { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
