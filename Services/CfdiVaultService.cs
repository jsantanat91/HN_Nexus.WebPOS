using System.Security.Cryptography;

namespace HN_Nexus.WebPOS.Services;

public class CfdiVaultService(IWebHostEnvironment env) : ICfdiVaultService
{
    public async Task<(string storagePath, string sha256, long sizeBytes)> SaveAsync(string documentType, string originalFileName, byte[] content)
    {
        var safeType = string.IsNullOrWhiteSpace(documentType) ? "misc" : documentType.Trim().ToLowerInvariant();
        var ext = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = safeType switch
            {
                "xml" => ".xml",
                "pdf" => ".pdf",
                _ => ".txt"
            };
        }

        var now = DateTime.UtcNow;
        var relativeDir = Path.Combine("App_Data", "cfdi-vault", now.ToString("yyyy"), now.ToString("MM"), safeType);
        var fullDir = Path.Combine(env.ContentRootPath, relativeDir);
        Directory.CreateDirectory(fullDir);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(fullDir, fileName);
        await System.IO.File.WriteAllBytesAsync(fullPath, content);

        var hash = Convert.ToHexString(SHA256.HashData(content));
        var relativePath = Path.Combine(relativeDir, fileName).Replace("\\", "/");
        return (relativePath, hash, content.LongLength);
    }

    public async Task<(byte[] content, string contentType, string fileName)?> ReadAsync(string storagePath, string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return null;
        }

        var normalized = storagePath.Replace("/", Path.DirectorySeparatorChar.ToString());
        var fullPath = Path.Combine(env.ContentRootPath, normalized);
        if (!System.IO.File.Exists(fullPath))
        {
            return null;
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var contentType = ext switch
        {
            ".xml" => "application/xml",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };

        return (bytes, contentType, originalFileName);
    }
}
