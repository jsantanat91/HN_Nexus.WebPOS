using HN_Nexus.WebPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleDetail> SaleDetails => Set<SaleDetail>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<AppConfig> AppConfigs => Set<AppConfig>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CashCut> CashCuts => Set<CashCut>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<SupplierOrder> SupplierOrders => Set<SupplierOrder>();
    public DbSet<ProductIngredient> ProductIngredients => Set<ProductIngredient>();

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

        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "General" },
            new Category { Id = 2, Name = "Textil" },
            new Category { Id = 3, Name = "Ceramica" },
            new Category { Id = 4, Name = "Tecnologia" }
        );

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

        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Taza Ceramica 11oz", Barcode = "1001", Price = 70m, Cost = 35m, Stock = 50, CategoryId = 3, SatProductCode = "01010101", SatUnitCode = "H87" },
            new Product { Id = 2, Name = "Playera Estampada", Barcode = "1002", Price = 150m, Cost = 80m, Stock = 20, CategoryId = 2, SatProductCode = "01010101", SatUnitCode = "H87" },
            new Product { Id = 3, Name = "Gorra Trucker", Barcode = "1003", Price = 90m, Cost = 40m, Stock = 15, CategoryId = 2, SatProductCode = "01010101", SatUnitCode = "H87" },
            new Product { Id = 4, Name = "Termo Digital", Barcode = "1004", Price = 220m, Cost = 120m, Stock = 10, CategoryId = 4, SatProductCode = "01010101", SatUnitCode = "H87" }
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
    }
}
