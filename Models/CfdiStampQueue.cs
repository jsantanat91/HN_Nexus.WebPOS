namespace HN_Nexus.WebPOS.Models;

public class CfdiStampQueue
{
    public long Id { get; set; }
    public int SaleId { get; set; }
    public string Status { get; set; } = "Pending"; // Pending | Processing | Done | Error
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
}
