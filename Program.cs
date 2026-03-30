using System.Globalization;
using System.Security.Claims;
using HN_Nexus.WebPOS.Data;
using HN_Nexus.WebPOS.Models;
using HN_Nexus.WebPOS.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IModuleAccessService, ModuleAccessService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Denied";
        options.Cookie.Name = "HNNexus.WebPOS.Auth";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim(ClaimTypes.Role, "Admin"));
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToFolder("/Account");
    options.Conventions.AuthorizeFolder("/Config", "AdminOnly");
});

var moduleRoutes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["/"] = ModuleCatalog.Dashboard,
    ["/Index"] = ModuleCatalog.Dashboard,
    ["/Sales"] = ModuleCatalog.Sales,
    ["/Products"] = ModuleCatalog.Products,
    ["/Customers"] = ModuleCatalog.Customers,
    ["/Expenses"] = ModuleCatalog.Expenses,
    ["/CashCuts"] = ModuleCatalog.CashCuts,
    ["/SupplierOrders"] = ModuleCatalog.SupplierOrders,
    ["/Config"] = ModuleCatalog.Config
};

var mx = new CultureInfo("es-MX");
CultureInfo.DefaultThreadCurrentCulture = mx;
CultureInfo.DefaultThreadCurrentUICulture = mx;

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    await db.Database.ExecuteSqlRawAsync(
        """
        ALTER TABLE "Sales" ADD COLUMN IF NOT EXISTS "SubtotalAmount" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "Sales" ADD COLUMN IF NOT EXISTS "TaxAmount" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "Sales" ADD COLUMN IF NOT EXISTS "DiscountAmount" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "Sales" ADD COLUMN IF NOT EXISTS "PaymentMethod" text NOT NULL DEFAULT 'Cash';
        ALTER TABLE "Sales" ADD COLUMN IF NOT EXISTS "AmountReceived" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "Sales" ADD COLUMN IF NOT EXISTS "ChangeAmount" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "Sales" ADD COLUMN IF NOT EXISTS "CancelledAt" timestamp with time zone NULL;
        ALTER TABLE "Sales" ADD COLUMN IF NOT EXISTS "CancelReason" text NULL;

        ALTER TABLE "SaleDetails" ADD COLUMN IF NOT EXISTS "DiscountPercent" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "SaleDetails" ADD COLUMN IF NOT EXISTS "DiscountAmount" numeric(18,2) NOT NULL DEFAULT 0;

        ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "ModulePermissions" text NOT NULL DEFAULT 'dashboard,sales,products,customers,expenses,cashcuts,supplierorders';

        ALTER TABLE "AppConfigs" ADD COLUMN IF NOT EXISTS "CurrencySymbol" text NOT NULL DEFAULT '$';
        ALTER TABLE "AppConfigs" ADD COLUMN IF NOT EXISTS "TaxRate" numeric(18,2) NOT NULL DEFAULT 16;
        ALTER TABLE "AppConfigs" ADD COLUMN IF NOT EXISTS "TicketPrinterName" text NULL;

        ALTER TABLE "CashCuts" ADD COLUMN IF NOT EXISTS "CashSales" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "CashCuts" ADD COLUMN IF NOT EXISTS "CardSales" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "CashCuts" ADD COLUMN IF NOT EXISTS "TransferSales" numeric(18,2) NOT NULL DEFAULT 0;
        """);

    if (!await db.Users.AnyAsync(u => u.Username == "admin"))
    {
        db.Users.Add(new User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            FullName = "Administrador",
            Role = "Admin",
            ModulePermissions = string.Join(',', ModuleCatalog.All),
            IsActive = true
        });
        await db.SaveChangesAsync();
    }

    var admin = await db.Users.FirstOrDefaultAsync(u => u.Username == "admin");
    if (admin is not null)
    {
        admin.ModulePermissions = string.Join(',', ModuleCatalog.All);
        await db.SaveChangesAsync();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (ctx, next) =>
{
    if (ctx.User.Identity?.IsAuthenticated == true && !ctx.User.IsInRole("Admin"))
    {
        var path = ctx.Request.Path.Value ?? "/";
        var route = moduleRoutes
            .OrderByDescending(x => x.Key.Length)
            .FirstOrDefault(x => path.StartsWith(x.Key, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(route.Key))
        {
            var modules = (ctx.User.FindFirstValue("modules") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.ToLowerInvariant())
                .ToHashSet();

            if (!modules.Contains(route.Value))
            {
                ctx.Response.Redirect("/Account/Denied");
                return;
            }
        }
    }

    await next();
});

app.MapRazorPages();

app.Run();
