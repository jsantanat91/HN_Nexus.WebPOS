namespace HN_Nexus.WebPOS.Models;

public class ProductStock
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int BranchId { get; set; }
    public Branch? Branch { get; set; }

    public int Stock { get; set; }
    public int MinStock { get; set; } = 5;
}
