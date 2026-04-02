namespace HN_Nexus.WebPOS.Models;

public class LotTrace
{
    public long Id { get; set; }
    public int? SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int? ProductLotId { get; set; }
    public ProductLot? ProductLot { get; set; }
    public int BranchId { get; set; }
    public int Quantity { get; set; }
    public string MovementType { get; set; } = "Sale"; // Sale|Return|Adjust
    public string Reference { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
