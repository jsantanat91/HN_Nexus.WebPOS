namespace HN_Nexus.WebPOS.Models;

public class ReplenishmentRule
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int MinLevel { get; set; }
    public int MaxLevel { get; set; }
    public int SuggestedOrderQty { get; set; }
    public bool AutoEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
