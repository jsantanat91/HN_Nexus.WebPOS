using System.ComponentModel.DataAnnotations.Schema;

namespace HN_Nexus.WebPOS.Models;

public class ProductIngredient
{
    public int Id { get; set; }

    public int ParentProductId { get; set; }
    [ForeignKey(nameof(ParentProductId))]
    public Product? ParentProduct { get; set; }

    public int IngredientId { get; set; }
    [ForeignKey(nameof(IngredientId))]
    public Product? Ingredient { get; set; }

    public decimal Quantity { get; set; }
}
