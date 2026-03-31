namespace HN_Nexus.WebPOS.Models;

public class PriceListItem
{
    public int Id { get; set; }
    public int PriceListId { get; set; }
    public PriceList? PriceList { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int MinQty { get; set; } = 1;
    public decimal Price { get; set; }
}

