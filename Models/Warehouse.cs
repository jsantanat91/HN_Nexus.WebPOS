namespace HN_Nexus.WebPOS.Models;

public class Warehouse
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

