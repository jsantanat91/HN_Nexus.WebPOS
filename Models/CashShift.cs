namespace HN_Nexus.WebPOS.Models;

public class CashShift
{
    public int Id { get; set; }

    public int BranchId { get; set; }
    public Branch? Branch { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public decimal OpeningFloat { get; set; }

    public DateTime? ClosedAt { get; set; }
    public decimal? ClosingDeclared { get; set; }
    public decimal? SystemCashTotal { get; set; }
    public decimal? Difference { get; set; }

    public string Status { get; set; } = "Open";
}
