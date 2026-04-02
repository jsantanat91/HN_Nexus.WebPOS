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

    public string SalesAccount { get; set; } = "4000-000-000";
    public string VatAccount { get; set; } = "2080-000-000";
    public string CashAccount { get; set; } = "1010-000-000";
    public string CardAccount { get; set; } = "1020-000-000";
    public string TransferAccount { get; set; } = "1030-000-000";

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseSsl { get; set; } = true;
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }
    public string? AlertFromEmail { get; set; }
    public string? AlertToEmails { get; set; } // separados por coma
}
