namespace HN_Nexus.WebPOS.Services;

public interface ICfdiVaultService
{
    Task<(string storagePath, string sha256, long sizeBytes)> SaveAsync(string documentType, string originalFileName, byte[] content);
    Task<(byte[] content, string contentType, string fileName)?> ReadAsync(string storagePath, string originalFileName);
}
