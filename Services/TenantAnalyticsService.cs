using System.Data;
using System.Text.RegularExpressions;
using HN_Nexus.WebPOS.Models;
using Npgsql;

namespace HN_Nexus.WebPOS.Services;

public interface ITenantAnalyticsService
{
    Task<List<TenantSalesMetric>> GetTenantSalesMetricsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

public class TenantAnalyticsService(IConfiguration configuration) : ITenantAnalyticsService
{
    public async Task<List<TenantSalesMetric>> GetTenantSalesMetricsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var connStr = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connStr))
        {
            return [];
        }

        var list = new List<TenantSalesMetric>();
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        var tenants = new List<(string Code, string Name, string Schema)>();
        await using (var cmd = new NpgsqlCommand("SELECT \"Code\",\"Name\",\"SchemaName\" FROM public.\"Tenants\" WHERE \"IsActive\" = true ORDER BY \"Name\";", conn))
        await using (var rd = await cmd.ExecuteReaderAsync(ct))
        {
            while (await rd.ReadAsync(ct))
            {
                tenants.Add((rd.GetString(0), rd.GetString(1), rd.GetString(2)));
            }
        }

        foreach (var t in tenants)
        {
            var schema = SanitizeSchema(t.Schema);
            if (schema is null)
            {
                continue;
            }

            var sql = $"""
SELECT
  COUNT(*)::bigint AS sales_count,
  COALESCE(SUM(s."TotalAmount"),0)::numeric AS total_sales,
  COALESCE(AVG(s."TotalAmount"),0)::numeric AS avg_ticket
FROM "{schema}"."Sales" s
WHERE s."Status" = 'Completed' AND s."Date" >= @from AND s."Date" < @to;
""";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("from", NpgsqlTypes.NpgsqlDbType.TimestampTz, fromUtc);
            cmd.Parameters.AddWithValue("to", NpgsqlTypes.NpgsqlDbType.TimestampTz, toUtc);

            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (await rd.ReadAsync(ct))
            {
                list.Add(new TenantSalesMetric
                {
                    TenantCode = t.Code,
                    TenantName = t.Name,
                    SalesCount = rd.GetInt64(0),
                    TotalSales = rd.GetDecimal(1),
                    AvgTicket = rd.GetDecimal(2)
                });
            }
        }

        return list.OrderByDescending(x => x.TotalSales).ToList();
    }

    private static string? SanitizeSchema(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var schema = value.Trim().ToLowerInvariant();
        return Regex.IsMatch(schema, "^[a-z_][a-z0-9_]{0,62}$") ? schema : null;
    }
}

public class TenantSalesMetric
{
    public string TenantCode { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public long SalesCount { get; set; }
    public decimal TotalSales { get; set; }
    public decimal AvgTicket { get; set; }
}
