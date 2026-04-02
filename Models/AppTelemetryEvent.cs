namespace HN_Nexus.WebPOS.Models;

public class AppTelemetryEvent
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Error { get; set; }
}
