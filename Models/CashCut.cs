using System.ComponentModel.DataAnnotations;

namespace HN_Nexus.WebPOS.Models;

public class CashCut
{
    [Key]
    public int Id { get; set; }
    public DateTime CutDate { get; set; } = DateTime.UtcNow;
    public decimal TotalSystem { get; set; }
    public decimal TotalPhysical { get; set; }
    public decimal Difference { get; set; }
    public string User { get; set; } = string.Empty;
}
