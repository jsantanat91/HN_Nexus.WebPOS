namespace HN_Nexus.WebPOS.Models;

public class Customer
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Rfc { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CfdiUse { get; set; } = "G03";
    public string FiscalRegime { get; set; } = "601";
    public string InvoiceType { get; set; } = "I";
    public string PaymentForm { get; set; } = "01";
    public string PaymentMethodSat { get; set; } = "PUE";
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}

