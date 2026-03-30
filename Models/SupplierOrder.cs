namespace HN_Nexus.WebPOS.Models;

public class SupplierOrder
{
    public int Id { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Status { get; set; } = "Pendiente";
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
}
