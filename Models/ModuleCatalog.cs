namespace HN_Nexus.WebPOS.Models;

public static class ModuleCatalog
{
    public const string Dashboard = "dashboard";
    public const string Sales = "sales";
    public const string Products = "products";
    public const string Customers = "customers";
    public const string Expenses = "expenses";
    public const string CashCuts = "cashcuts";
    public const string SupplierOrders = "supplierorders";
    public const string Config = "config";

    public static readonly string[] All =
    [
        Dashboard,
        Sales,
        Products,
        Customers,
        Expenses,
        CashCuts,
        SupplierOrders,
        Config
    ];

    public static readonly Dictionary<string, string> Labels = new()
    {
        [Dashboard] = "Dashboard",
        [Sales] = "Ventas y Caja",
        [Products] = "Inventario",
        [Customers] = "Clientes",
        [Expenses] = "Gastos",
        [CashCuts] = "Cortes de Caja",
        [SupplierOrders] = "Pedidos a Proveedor",
        [Config] = "Configuración"
    };
}
