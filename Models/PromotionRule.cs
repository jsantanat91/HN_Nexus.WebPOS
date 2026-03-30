namespace HN_Nexus.WebPOS.Models;

public class PromotionRule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Types: ThreeForTwo | HappyHourPercent | ComboPrice
    public string RuleType { get; set; } = "ThreeForTwo";

    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    // For single-product rules
    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    // For combos
    public string? ProductIdsCsv { get; set; }
    public decimal ComboPrice { get; set; }

    // For quantity/percent rules
    public int BuyQty { get; set; } = 3;
    public int PayQty { get; set; } = 2;
    public decimal DiscountPercent { get; set; }

    // Hour rules
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? DaysOfWeekCsv { get; set; } // 1,2,3,4,5

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

