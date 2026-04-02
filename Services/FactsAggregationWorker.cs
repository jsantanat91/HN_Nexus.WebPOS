using HN_Nexus.WebPOS.Data;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Services;

public class FactsAggregationWorker(IServiceProvider services, ILogger<FactsAggregationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AggregateAllTenantsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error consolidando facts diarios/mensuales.");
            }

            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }

    private async Task AggregateAllTenantsAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var schemas = await db.Tenants
            .Where(t => t.IsActive)
            .Select(t => t.SchemaName)
            .Distinct()
            .ToListAsync(ct);

        if (schemas.Count == 0)
        {
            schemas.Add("public");
        }

        foreach (var schema in schemas)
        {
            var clean = SanitizeSchema(schema);
            await db.Database.ExecuteSqlRawAsync($"SET search_path TO \"{clean}\", public;", ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                DELETE FROM "FactDailyBranches";

                INSERT INTO "FactDailyBranches"
                ("PeriodDate","BranchId","SalesCount","SalesSubtotal","SalesTax","SalesTotal","CostTotal","ExpenseTotal","UpdatedAt")
                SELECT
                    s."PeriodDate",
                    s."BranchId",
                    s."SalesCount",
                    s."SalesSubtotal",
                    s."SalesTax",
                    s."SalesTotal",
                    COALESCE(c."CostTotal", 0),
                    COALESCE(e."ExpenseTotal", 0),
                    NOW()
                FROM (
                    SELECT
                        date_trunc('day', sa."Date")::date AS "PeriodDate",
                        sa."BranchId" AS "BranchId",
                        COUNT(*)::numeric(18,2) AS "SalesCount",
                        COALESCE(SUM(sa."SubtotalAmount"), 0)::numeric(18,2) AS "SalesSubtotal",
                        COALESCE(SUM(sa."TaxAmount"), 0)::numeric(18,2) AS "SalesTax",
                        COALESCE(SUM(sa."TotalAmount"), 0)::numeric(18,2) AS "SalesTotal"
                    FROM "Sales" sa
                    WHERE sa."Status" = 'Completed'
                    GROUP BY 1,2
                ) s
                LEFT JOIN (
                    SELECT
                        date_trunc('day', sa."Date")::date AS "PeriodDate",
                        sa."BranchId" AS "BranchId",
                        COALESCE(SUM(sd."Quantity" * p."Cost"), 0)::numeric(18,2) AS "CostTotal"
                    FROM "SaleDetails" sd
                    INNER JOIN "Sales" sa ON sa."Id" = sd."SaleId"
                    INNER JOIN "Products" p ON p."Id" = sd."ProductId"
                    WHERE sa."Status" = 'Completed'
                    GROUP BY 1,2
                ) c ON c."PeriodDate" = s."PeriodDate" AND c."BranchId" = s."BranchId"
                LEFT JOIN (
                    SELECT
                        date_trunc('day', ex."Date")::date AS "PeriodDate",
                        COALESCE(SUM(ex."Amount"), 0)::numeric(18,2) AS "ExpenseTotal"
                    FROM "Expenses" ex
                    GROUP BY 1
                ) e ON e."PeriodDate" = s."PeriodDate";

                DELETE FROM "FactMonthlyBranches";

                INSERT INTO "FactMonthlyBranches"
                ("PeriodMonth","BranchId","SalesCount","SalesSubtotal","SalesTax","SalesTotal","CostTotal","ExpenseTotal","UpdatedAt")
                SELECT
                    date_trunc('month', d."PeriodDate")::date AS "PeriodMonth",
                    d."BranchId",
                    SUM(d."SalesCount")::numeric(18,2),
                    SUM(d."SalesSubtotal")::numeric(18,2),
                    SUM(d."SalesTax")::numeric(18,2),
                    SUM(d."SalesTotal")::numeric(18,2),
                    SUM(d."CostTotal")::numeric(18,2),
                    SUM(d."ExpenseTotal")::numeric(18,2),
                    NOW()
                FROM "FactDailyBranches" d
                GROUP BY 1,2;
                """, ct);
        }

        await db.Database.ExecuteSqlRawAsync("SET search_path TO \"public\", public;", ct);
    }

    private static string SanitizeSchema(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "public";
        }

        var clean = raw.Trim().ToLowerInvariant();
        return System.Text.RegularExpressions.Regex.IsMatch(clean, "^[a-z_][a-z0-9_]{0,62}$") ? clean : "public";
    }
}
