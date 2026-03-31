namespace HN_Nexus.WebPOS.Models;

public class PriceList
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsWholesale { get; set; }
}

