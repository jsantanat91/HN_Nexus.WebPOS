namespace HN_Nexus.WebPOS.Models;

public class SaleReturn
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int BranchId { get; set; }

    public int CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public int? AuthorizedByUserId { get; set; }
    public User? AuthorizedByUser { get; set; }

    public int? AppliedByUserId { get; set; }
    public User? AppliedByUser { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Requested"; // Requested|Approved|Applied|Rejected

    public string RequestedSignature { get; set; } = string.Empty;
    public string? SupervisorSignature { get; set; }
    public string? SupervisorSignatureHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? AppliedAt { get; set; }

    public decimal TotalReturned { get; set; }

    public List<SaleReturnLine> Lines { get; set; } = new();
}
