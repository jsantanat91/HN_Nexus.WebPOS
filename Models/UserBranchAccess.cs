namespace HN_Nexus.WebPOS.Models;

public class UserBranchAccess
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    public int BranchId { get; set; }
    public Branch? Branch { get; set; }
}
