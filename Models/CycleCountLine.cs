namespace HN_Nexus.WebPOS.Models;

public class CycleCountLine
{
    public int Id { get; set; }
    public int CycleCountId { get; set; }
    public CycleCount? CycleCount { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int SystemQty { get; set; }
    public int CountedQty { get; set; }
    public int Difference => CountedQty - SystemQty;
}
