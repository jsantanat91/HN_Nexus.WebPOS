using HN_Nexus.WebPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<PermissionProfile> PermissionProfiles => Set<PermissionProfile>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductStock> ProductStocks => Set<ProductStock>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleDetail> SaleDetails => Set<SaleDetail>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<AppConfig> AppConfigs => Set<AppConfig>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<UserBranchAccess> UserBranchAccesses => Set<UserBranchAccess>();
    public DbSet<CashShift> CashShifts => Set<CashShift>();
    public DbSet<CashCut> CashCuts => Set<CashCut>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<SupplierOrder> SupplierOrders => Set<SupplierOrder>();
    public DbSet<ProductIngredient> ProductIngredients => Set<ProductIngredient>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<ProductLot> ProductLots => Set<ProductLot>();
    public DbSet<PromotionRule> PromotionRules => Set<PromotionRule>();
    public DbSet<CfdiDocument> CfdiDocuments => Set<CfdiDocument>();
    public DbSet<AccountingClosure> AccountingClosures => Set<AccountingClosure>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseStock> WarehouseStocks => Set<WarehouseStock>();
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<PriceListItem> PriceListItems => Set<PriceListItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProductIngredient>()
            .HasOne(p => p.ParentProduct)
            .WithMany()
            .HasForeignKey(p => p.ParentProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductIngredient>()
            .HasOne(p => p.Ingredient)
            .WithMany()
            .HasForeignKey(p => p.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserBranchAccess>()
            .HasIndex(x => new { x.UserId, x.BranchId })
            .IsUnique();

        modelBuilder.Entity<ProductStock>()
            .HasIndex(x => new { x.ProductId, x.BranchId })
            .IsUnique();

        modelBuilder.Entity<Product>()
            .HasIndex(x => x.ProductNumber)
            .IsUnique();

        modelBuilder.Entity<ProductLot>()
            .HasIndex(x => new { x.BranchId, x.ProductId, x.LotNumber, x.SerialNumber });

        modelBuilder.Entity<CfdiDocument>()
            .HasIndex(x => x.SaleId)
            .IsUnique();

        modelBuilder.Entity<Warehouse>()
            .HasIndex(x => new { x.BranchId, x.Code })
            .IsUnique();

        modelBuilder.Entity<WarehouseStock>()
            .HasIndex(x => new { x.WarehouseId, x.ProductId })
            .IsUnique();

        modelBuilder.Entity<PriceListItem>()
            .HasIndex(x => new { x.PriceListId, x.ProductId, x.MinQty })
            .IsUnique();

        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "General" },
            new Category { Id = 2, Name = "Textil" },
            new Category { Id = 3, Name = "Cerámica" },
            new Category { Id = 4, Name = "Tecnología" }
        );

        modelBuilder.Entity<PermissionProfile>().HasData(
            new PermissionProfile
            {
                Id = 1,
                Name = "Administrador",
                IsAdmin = true,
                IsActive = true,
                ModulePermissions = string.Join(',', ModuleCatalog.All)
            },
            new PermissionProfile
            {
                Id = 2,
                Name = "Caja",
                IsAdmin = false,
                IsActive = true,
                ModulePermissions = string.Join(',', new[]
                {
                    ModuleCatalog.Dashboard,
                    ModuleCatalog.Sales,
                    ModuleCatalog.Customers,
                    ModuleCatalog.CashCuts,
                    ModuleCatalog.CashShifts
                })
            }
        );

        modelBuilder.Entity<Branch>().HasData(new Branch
        {
            Id = 1,
            Code = "MATRIZ",
            Name = "Sucursal Matriz",
            Address = "Pendiente por configurar",
            IsActive = true
        });

        modelBuilder.Entity<Supplier>().HasData(new Supplier
        {
            Id = 1,
            Name = "Proveedor General",
            ContactName = "",
            Phone = "",
            Email = "",
            IsActive = true
        });

        modelBuilder.Entity<User>().HasData(new User
        {
            Id = 1,
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            FullName = "Administrador",
            Role = "Admin",
            ModulePermissions = string.Join(',', ModuleCatalog.All),
            IsActive = true
        });

        modelBuilder.Entity<UserBranchAccess>().HasData(new UserBranchAccess
        {
            Id = 1,
            UserId = 1,
            BranchId = 1
        });

        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, ProductNumber = 1, Name = "Taza Cerámica 11oz", Barcode = "1001", Price = 70m, Cost = 35m, Stock = 50, PriceIncludesTax = false, CategoryId = 3, SatProductCode = "01010101", SatUnitCode = "H87" },
            new Product { Id = 2, ProductNumber = 2, Name = "Playera Estampada", Barcode = "1002", Price = 150m, Cost = 80m, Stock = 20, PriceIncludesTax = false, CategoryId = 2, SatProductCode = "01010101", SatUnitCode = "H87" },
            new Product { Id = 3, ProductNumber = 3, Name = "Gorra Trucker", Barcode = "1003", Price = 90m, Cost = 40m, Stock = 15, PriceIncludesTax = false, CategoryId = 2, SatProductCode = "01010101", SatUnitCode = "H87" },
            new Product { Id = 4, ProductNumber = 4, Name = "Termo Digital", Barcode = "1004", Price = 220m, Cost = 120m, Stock = 10, PriceIncludesTax = false, CategoryId = 4, SatProductCode = "01010101", SatUnitCode = "H87" }
        );

        modelBuilder.Entity<ProductStock>().HasData(
            new ProductStock { Id = 1, ProductId = 1, BranchId = 1, Stock = 50, MinStock = 5 },
            new ProductStock { Id = 2, ProductId = 2, BranchId = 1, Stock = 20, MinStock = 5 },
            new ProductStock { Id = 3, ProductId = 3, BranchId = 1, Stock = 15, MinStock = 5 },
            new ProductStock { Id = 4, ProductId = 4, BranchId = 1, Stock = 10, MinStock = 5 }
        );

        modelBuilder.Entity<AppConfig>().HasData(new AppConfig
        {
            Id = 1,
            CompanyName = "HN Nexus",
            TaxId = "XAXX010101000",
            Address = "Pendiente de configurar",
            Phone = "",
            CurrencySymbol = "$",
            TaxRate = 16m,
            TicketHeader = "Bienvenido",
            TicketFooter = "Gracias por su compra"
        });

        modelBuilder.Entity<Warehouse>().HasData(new Warehouse
        {
            Id = 1,
            BranchId = 1,
            Code = "PRINCIPAL",
            Name = "Almacén Principal",
            IsActive = true
        });

        modelBuilder.Entity<WarehouseStock>().HasData(
            new WarehouseStock { Id = 1, WarehouseId = 1, ProductId = 1, Stock = 50, MinStock = 5 },
            new WarehouseStock { Id = 2, WarehouseId = 1, ProductId = 2, Stock = 20, MinStock = 5 },
            new WarehouseStock { Id = 3, WarehouseId = 1, ProductId = 3, Stock = 15, MinStock = 5 },
            new WarehouseStock { Id = 4, WarehouseId = 1, ProductId = 4, Stock = 10, MinStock = 5 }
        );

        modelBuilder.Entity<PriceList>().HasData(
            new PriceList { Id = 1, Name = "Mostrador", IsActive = true, IsWholesale = false },
            new PriceList { Id = 2, Name = "Mayoreo", IsActive = true, IsWholesale = true }
        );
    }
}
