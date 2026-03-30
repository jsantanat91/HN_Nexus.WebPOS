using System.ComponentModel.DataAnnotations;

namespace HN_Nexus.WebPOS.Models;

public class Branch
{
    [Key]
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
