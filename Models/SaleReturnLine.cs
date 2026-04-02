namespace HN_Nexus.WebPOS.Models;

public class SaleReturnLine
{
    public int Id { get; set; }
    public int SaleReturnId { get; set; }
    public SaleReturn? SaleReturn { get; set; }
    public int SaleDetailId { get; set; }
    public SaleDetail? SaleDetail { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount => Quantity * UnitPrice;
}
