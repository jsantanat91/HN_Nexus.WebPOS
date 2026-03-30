using System.ComponentModel.DataAnnotations;

namespace HN_Nexus.WebPOS.Models;

public class User
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Role { get; set; } = "Cajero";

    public int? PermissionProfileId { get; set; }
    public PermissionProfile? PermissionProfile { get; set; }

    public string ModulePermissions { get; set; } = "dashboard,sales,products,customers,expenses,cashcuts,supplierorders";

    public bool IsActive { get; set; } = true;
}

