namespace HN_Nexus.WebPOS.Models;

public class CfdiDocument
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }

    public string PacProvider { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft"; // Draft | Stamped | Cancelled | Error
    public string? Uuid { get; set; }
    public string? XmlPath { get; set; }
    public string? PdfPath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StampedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}

