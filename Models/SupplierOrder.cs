namespace HN_Nexus.WebPOS.Models;

public class SupplierOrder
{
    public int Id { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }

    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }

    public string Status { get; set; } = "Pendiente";
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
}

