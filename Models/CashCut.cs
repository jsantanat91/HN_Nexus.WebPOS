using System.ComponentModel.DataAnnotations;

namespace HN_Nexus.WebPOS.Models;

public class CashCut
{
    [Key]
    public int Id { get; set; }
    public DateTime CutDate { get; set; } = DateTime.UtcNow;

    public int BranchId { get; set; }
    public Branch? Branch { get; set; }

    public decimal TotalSystem { get; set; }
    public decimal TotalPhysical { get; set; }
    public decimal Difference { get; set; }

    public decimal CashSales { get; set; }
    public decimal CardSales { get; set; }
    public decimal TransferSales { get; set; }

    public string User { get; set; } = string.Empty;
}

