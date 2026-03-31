namespace HN_Nexus.WebPOS.Services;

public static class ClientIpResolver
{
    public static string Get(HttpContext? context)
    {
        if (context is null)
        {
            return "-";
        }

        var fromForwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fromForwarded))
        {
            var ip = fromForwarded.Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(ip))
            {
                return ip;
            }
        }

        var fromReal = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fromReal))
        {
            return fromReal.Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "-";
    }
}
