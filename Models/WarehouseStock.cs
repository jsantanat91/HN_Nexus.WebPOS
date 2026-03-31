namespace HN_Nexus.WebPOS.Models;

public class WarehouseStock
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Stock { get; set; }
    public int MinStock { get; set; } = 5;
}

