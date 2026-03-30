using HN_Nexus.WebPOS.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HN_Nexus.WebPOS.Services;

public interface IReportPdfService
{
    byte[] BuildDailySalesPdf(DateTime date, string branchName, string currencySymbol, IReadOnlyList<Sale> sales);
}

public class ReportPdfService : IReportPdfService
{
    public byte[] BuildDailySalesPdf(DateTime date, string branchName, string currencySymbol, IReadOnlyList<Sale> sales)
    {
        static string Money(string symbol, decimal value) => $"{symbol}{value:N2}";

        var completed = sales.Where(s => s.Status == "Completed").ToList();
        var total = completed.Sum(x => x.TotalAmount);
        var cash = completed.Where(x => x.PaymentMethod == "Cash").Sum(x => x.TotalAmount);
        var card = completed.Where(x => x.PaymentMethod == "Card").Sum(x => x.TotalAmount);
        var transfer = completed.Where(x => x.PaymentMethod == "Transfer").Sum(x => x.TotalAmount);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(24);
                page.Size(PageSizes.A4);

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Reporte Diario de Ventas").FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                        col.Item().Text($"Fecha: {date:dd/MM/yyyy}").FontSize(11);
                        col.Item().Text($"Sucursal: {branchName}").FontSize(11);
                    });

                    row.ConstantItem(160).Background(Colors.Grey.Lighten4).Padding(8).Column(col =>
                    {
                        col.Item().Text("Resumen").SemiBold();
                        col.Item().Text($"Total: {Money(currencySymbol, total)}");
                        col.Item().Text($"Efectivo: {Money(currencySymbol, cash)}");
                        col.Item().Text($"Tarjeta: {Money(currencySymbol, card)}");
                        col.Item().Text($"Transfer: {Money(currencySymbol, transfer)}");
                    });
                });

                page.Content().PaddingTop(16).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(42);
                        columns.ConstantColumn(78);
                        columns.RelativeColumn();
                        columns.ConstantColumn(72);
                        columns.ConstantColumn(82);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Folio").SemiBold();
                        header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Hora").SemiBold();
                        header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Cliente").SemiBold();
                        header.Cell().Background(Colors.Blue.Lighten4).Padding(5).Text("Pago").SemiBold();
                        header.Cell().Background(Colors.Blue.Lighten4).Padding(5).AlignRight().Text("Total").SemiBold();
                    });

                    foreach (var sale in sales.OrderByDescending(s => s.Date))
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(sale.Id.ToString());
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(sale.Date.ToLocalTime().ToString("HH:mm"));
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(sale.Customer?.FullName ?? "Publico General");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(sale.PaymentMethod);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(Money(currencySymbol, sale.TotalAmount));
                    }
                });

                page.Footer().AlignCenter().Text(txt =>
                {
                    txt.Span("HN Nexus POS - ");
                    txt.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                });
            });
        }).GeneratePdf();
    }
}

