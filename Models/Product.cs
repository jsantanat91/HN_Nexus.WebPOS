using System.ComponentModel.DataAnnotations;

namespace HN_Nexus.WebPOS.Models;

public class Product
{
    public int Id { get; set; }

    // Consecutivo interno amigable para operación (PROD-000001)
    public int ProductNumber { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string Barcode { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public int Stock { get; set; }
    public bool PriceIncludesTax { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public string SatProductCode { get; set; } = "01010101";
    public string SatUnitCode { get; set; } = "H87";
}
