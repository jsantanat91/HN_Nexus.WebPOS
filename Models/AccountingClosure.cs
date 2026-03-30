namespace HN_Nexus.WebPOS.Models;

public class AccountingClosure
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }

    public DateTime ClosureDate { get; set; } // date in UTC (00:00)
    public string Status { get; set; } = "Closed"; // Closed | Reopened

    public int ClosedByUserId { get; set; }
    public User? ClosedByUser { get; set; }
    public DateTime ClosedAt { get; set; } = DateTime.UtcNow;
    public string Notes { get; set; } = string.Empty;

    public int? ReopenedByUserId { get; set; }
    public User? ReopenedByUser { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public string? ReopenReason { get; set; }
}

