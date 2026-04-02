namespace HN_Nexus.WebPOS.Models;

public class CycleCount
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public int CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public int? AuthorizedByUserId { get; set; }
    public User? AuthorizedByUser { get; set; }
    public string Status { get; set; } = "Open"; // Open|Authorized|Closed
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AuthorizedAt { get; set; }

    public List<CycleCountLine> Lines { get; set; } = new();
}
