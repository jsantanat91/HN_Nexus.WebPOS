namespace HN_Nexus.WebPOS.Models;

public class AuditLog
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public int? BranchId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
