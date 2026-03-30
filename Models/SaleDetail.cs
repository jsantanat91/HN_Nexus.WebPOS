namespace HN_Nexus.WebPOS.Models;

public class SaleDetail
{
    public int Id { get; set; }

    public int SaleId { get; set; }
    public Sale? Sale { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }

    public decimal LineSubtotal => Quantity * UnitPrice;
    public decimal Total => LineSubtotal - DiscountAmount;
}

