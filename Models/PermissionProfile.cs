namespace HN_Nexus.WebPOS.Models;

public class PermissionProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ModulePermissions { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; } = true;
}
