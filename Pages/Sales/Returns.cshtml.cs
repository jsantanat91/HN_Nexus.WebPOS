using System.Security.Cryptography;
using System.Text;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Pages.Sales;

public class ReturnsModel(AppDbContext db, IUserContextService userContext) : PageModel
{
    public List<SelectListItem> Branches { get; private set; } = new();
    public List<Sale> Sales { get; private set; } = new();
    public Sale? SelectedSale { get; private set; }
    public Dictionary<int, int> ReservedByDetail { get; private set; } = new();
    public List<SaleReturn> Requests { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int SaleId { get; set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostRequestAsync(int saleId, int saleDetailId, int quantity, string reason, string requesterSignature, int branchId)
    {
        BranchId = branchId;
        SaleId = saleId;

        var sale = await db.Sales.Include(x => x.Details).FirstOrDefaultAsync(x => x.Id == saleId && x.Status == "Completed");
        if (sale is null)
        {
            TempData["Flash"] = "Venta no válida para devolución.";
            return RedirectToPage(new { branchId });
        }

        var line = sale.Details.FirstOrDefault(x => x.Id == saleDetailId);
        if (line is null || quantity <= 0)
        {
            TempData["Flash"] = "Línea inválida para devolución.";
            return RedirectToPage(new { branchId, saleId });
        }

        if (string.IsNullOrWhiteSpace(requesterSignature))
        {
            TempData["Flash"] = "Firma de solicitante requerida.";
            return RedirectToPage(new { branchId, saleId });
        }

        var reserved = await db.SaleReturnLines
            .Include(x => x.SaleReturn)
            .Where(x => x.SaleDetailId == saleDetailId && x.SaleReturn != null && x.SaleReturn.Status != "Rejected")
            .SumAsync(x => (int?)x.Quantity) ?? 0;
        var available = line.Quantity - reserved;
        if (quantity > available)
        {
            TempData["Flash"] = $"Solo puedes solicitar hasta {available} unidades en esta línea.";
            return RedirectToPage(new { branchId, saleId });
        }

        var currentUserId = userContext.GetUserId(User);
        if (currentUserId is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var ret = new SaleReturn
        {
            SaleId = sale.Id,
            BranchId = sale.BranchId,
            CreatedByUserId = currentUserId.Value,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Devolución parcial" : reason.Trim(),
            RequestedSignature = requesterSignature.Trim(),
            CreatedAt = DateTime.UtcNow,
            Status = "Requested",
            TotalReturned = line.UnitPrice * quantity
        };
        ret.Lines.Add(new SaleReturnLine
        {
            SaleDetailId = line.Id,
            Quantity = quantity,
            UnitPrice = line.UnitPrice
        });
        db.SaleReturns.Add(ret);

        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "RETURN_REQUEST",
            Entity = "Sale",
            EntityId = sale.Id,
            BranchId = sale.BranchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = ClientIpResolver.Get(HttpContext),
            Details = $"Solicitud devolución. Producto={line.ProductId}, Qty={quantity}."
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Solicitud de devolución creada.";
        return RedirectToPage(new { branchId, saleId });
    }

    public async Task<IActionResult> OnPostApproveAsync(int returnId, string supervisorUser, string supervisorPassword, string supervisorSignature, int branchId, int saleId)
    {
        var req = await db.SaleReturns.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == returnId);
        if (req is null || req.Status != "Requested")
        {
            return RedirectToPage(new { branchId, saleId });
        }

        if (string.IsNullOrWhiteSpace(supervisorSignature))
        {
            TempData["Flash"] = "Firma digital del supervisor requerida.";
            return RedirectToPage(new { branchId, saleId });
        }

        var supervisor = await db.Users.FirstOrDefaultAsync(x => x.IsActive && x.Username == supervisorUser && x.Role == "Admin");
        if (supervisor is null || !BCrypt.Net.BCrypt.Verify(supervisorPassword, supervisor.PasswordHash))
        {
            TempData["Flash"] = "Autorización inválida. Se requiere administrador.";
            return RedirectToPage(new { branchId, saleId });
        }

        var payload = $"{req.Id}|{supervisor.Id}|{supervisorSignature.Trim()}|{DateTime.UtcNow:O}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

        req.AuthorizedByUserId = supervisor.Id;
        req.SupervisorSignature = supervisorSignature.Trim();
        req.SupervisorSignatureHash = hash;
        req.Status = "Approved";
        req.ApprovedAt = DateTime.UtcNow;

        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "RETURN_APPROVE",
            Entity = "SaleReturn",
            EntityId = req.Id,
            BranchId = req.BranchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = ClientIpResolver.Get(HttpContext),
            Details = $"Solicitud #{req.Id} aprobada por {supervisor.Username}. FirmaHash={hash[..12]}..."
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Solicitud aprobada con firma digital.";
        return RedirectToPage(new { branchId, saleId });
    }

    public async Task<IActionResult> OnPostApplyAsync(int returnId, int branchId, int saleId)
    {
        var req = await db.SaleReturns
            .Include(x => x.Sale)
            .Include(x => x.Lines)
                .ThenInclude(x => x.SaleDetail)
            .FirstOrDefaultAsync(x => x.Id == returnId);

        if (req is null || req.Sale is null || req.Status != "Approved")
        {
            return RedirectToPage(new { branchId, saleId });
        }

        var currentUserId = userContext.GetUserId(User);
        if (currentUserId is null)
        {
            return RedirectToPage("/Account/Login");
        }

        foreach (var line in req.Lines)
        {
            var detail = line.SaleDetail;
            if (detail is null)
            {
                continue;
            }

            var productId = detail.ProductId;
            var qty = line.Quantity;

            var branchStock = await db.ProductStocks.FirstOrDefaultAsync(x => x.BranchId == req.BranchId && x.ProductId == productId);
            if (branchStock is not null) branchStock.Stock += qty;

            if (req.Sale.WarehouseId.HasValue)
            {
                var wh = await db.WarehouseStocks.FirstOrDefaultAsync(x => x.WarehouseId == req.Sale.WarehouseId.Value && x.ProductId == productId);
                if (wh is null)
                {
                    db.WarehouseStocks.Add(new WarehouseStock { WarehouseId = req.Sale.WarehouseId.Value, ProductId = productId, Stock = qty, MinStock = 5 });
                }
                else
                {
                    wh.Stock += qty;
                }
            }

            var product = await db.Products.FirstOrDefaultAsync(x => x.Id == productId);
            if (product is not null) product.Stock += qty;

            var lot = await db.ProductLots
                .Where(x => x.BranchId == req.BranchId && x.ProductId == productId)
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync();
            if (lot is null)
            {
                lot = new ProductLot
                {
                    BranchId = req.BranchId,
                    ProductId = productId,
                    LotNumber = $"DEV-{req.SaleId}",
                    Quantity = qty,
                    UnitCost = line.UnitPrice,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.ProductLots.Add(lot);
            }
            else
            {
                lot.Quantity += qty;
                lot.UpdatedAt = DateTime.UtcNow;
            }

            db.LotTraces.Add(new LotTrace
            {
                SaleId = req.SaleId,
                BranchId = req.BranchId,
                ProductId = productId,
                ProductLotId = lot.Id,
                Quantity = qty,
                MovementType = "Return",
                Reference = $"DEV aplicada #{req.Id}",
                CreatedAt = DateTime.UtcNow
            });
        }

        req.Status = "Applied";
        req.AppliedAt = DateTime.UtcNow;
        req.AppliedByUserId = currentUserId.Value;

        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            Action = "RETURN_APPLY",
            Entity = "SaleReturn",
            EntityId = req.Id,
            BranchId = req.BranchId,
            Username = User.Identity?.Name ?? "sistema",
            IpAddress = ClientIpResolver.Get(HttpContext),
            Details = $"Solicitud #{req.Id} aplicada a inventario."
        });

        await db.SaveChangesAsync();
        TempData["Flash"] = "Devolución aplicada correctamente.";
        return RedirectToPage(new { branchId, saleId });
    }

    private async Task LoadAsync()
    {
        var allowedBranches = await userContext.GetAccessibleBranchesAsync(User);
        Branches = allowedBranches.Select(b => new SelectListItem($"{b.Code} - {b.Name}", b.Id.ToString())).ToList();
        if (BranchId <= 0 && Branches.Count > 0)
        {
            BranchId = int.Parse(Branches[0].Value!);
        }

        Sales = await db.Sales
            .Include(x => x.Customer)
            .Where(x => x.BranchId == BranchId && x.Status == "Completed")
            .OrderByDescending(x => x.Date)
            .Take(100)
            .ToListAsync();

        if (SaleId <= 0 && Sales.Count > 0)
        {
            SaleId = Sales[0].Id;
        }

        SelectedSale = await db.Sales
            .Include(x => x.Customer)
            .Include(x => x.Details).ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(x => x.Id == SaleId && x.BranchId == BranchId);

        if (SelectedSale is not null)
        {
            var detailIds = SelectedSale.Details.Select(d => d.Id).ToList();
            ReservedByDetail = await db.SaleReturnLines
                .Include(x => x.SaleReturn)
                .Where(x => detailIds.Contains(x.SaleDetailId) && x.SaleReturn != null && x.SaleReturn.Status != "Rejected")
                .GroupBy(x => x.SaleDetailId)
                .ToDictionaryAsync(g => g.Key, g => g.Sum(x => x.Quantity));
        }

        Requests = await db.SaleReturns
            .Include(x => x.CreatedByUser)
            .Include(x => x.AuthorizedByUser)
            .Where(x => x.BranchId == BranchId && x.SaleId == SaleId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync();
    }
}
