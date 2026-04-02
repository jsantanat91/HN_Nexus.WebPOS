using System.ComponentModel.DataAnnotations;

namespace HN_Nexus.WebPOS.Models;

public class Tenant
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(63)]
    public string SchemaName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? Host { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
