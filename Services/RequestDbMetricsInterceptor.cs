using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HN_Nexus.WebPOS.Services;

public class RequestDbMetricsInterceptor(IHttpContextAccessor httpContextAccessor) : DbCommandInterceptor
{
    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        AddMetrics(eventData.Duration);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        AddMetrics(eventData.Duration);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override object ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object result)
    {
        AddMetrics(eventData.Duration);
        return base.ScalarExecuted(command, eventData, result);
    }

    private void AddMetrics(TimeSpan duration)
    {
        var ctx = httpContextAccessor.HttpContext;
        if (ctx is null)
        {
            return;
        }

        var commands = (int?)ctx.Items["db_cmd_count"] ?? 0;
        var durationMs = (long?)ctx.Items["db_duration_ms"] ?? 0L;
        ctx.Items["db_cmd_count"] = commands + 1;
        ctx.Items["db_duration_ms"] = durationMs + (long)duration.TotalMilliseconds;
    }
}
