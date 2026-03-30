namespace HN_Nexus.WebPOS.Models;

public static class ModuleCatalog
{
    public const string Dashboard = "dashboard";
    public const string Sales = "sales";
    public const string Products = "products";
    public const string Customers = "customers";
    public const string Expenses = "expenses";
    public const string CashCuts = "cashcuts";
    public const string CashShifts = "cashshifts";
    public const string SupplierOrders = "supplierorders";
    public const string Reports = "reports";
    public const string Config = "config";
    public const string Users = "users";
    public const string Branches = "branches";

    public static readonly string[] All =
    [
        Dashboard,
        Sales,
        Products,
        Customers,
        Expenses,
        CashCuts,
        CashShifts,
        SupplierOrders,
        Reports,
        Config,
        Users,
        Branches
    ];

    public static readonly Dictionary<string, string> Labels = new()
    {
        [Dashboard] = "Inicio",
        [Sales] = "Ventas y Caja",
        [Products] = "Inventario",
        [Customers] = "Clientes",
        [Expenses] = "Gastos",
        [CashCuts] = "Corte",
        [CashShifts] = "Turno",
        [SupplierOrders] = "Órdenes de Compra",
        [Reports] = "Reportes",
        [Config] = "Configuración",
        [Users] = "Usuarios",
        [Branches] = "Sucursales"
    };
}

