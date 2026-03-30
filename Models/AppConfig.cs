namespace HN_Nexus.WebPOS.Models;

public class AppConfig
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = "HN Nexus";
    public string TaxId { get; set; } = "XAXX010101000";
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public string CurrencySymbol { get; set; } = "$";
    public decimal TaxRate { get; set; } = 16m;
    public string? TicketPrinterName { get; set; }

    public string? TicketHeader { get; set; }
    public string? TicketFooter { get; set; }
    public string? CerPath { get; set; }
    public string? KeyPath { get; set; }
    public string? PrivateKeyPassword { get; set; }
    public string? Street { get; set; }
    public string? PostalCode { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; } = "Mexico";

    public string? PacProvider { get; set; }
    public string? PacApiUrl { get; set; }
    public string? PacApiKey { get; set; }
    public string? PacApiSecret { get; set; }
    public bool PacTestMode { get; set; } = true;
}

