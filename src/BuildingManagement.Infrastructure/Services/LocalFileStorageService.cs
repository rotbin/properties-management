using BuildingManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(string basePath, ILogger<LocalFileStorageService> logger)
    {
        _basePath = basePath;
        _logger = logger;
    }

    public async Task<string> SaveFileAsync(string entityType, int entityId, string fileName, string contentType, Stream fileStream, CancellationToken ct = default)
    {
        var relativePath = Path.Combine("uploads", entityType, entityId.ToString(), $"{Guid.NewGuid()}{Path.GetExtension(fileName)}");
        var fullPath = Path.Combine(_basePath, relativePath);

        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);

        await using var fs = new FileStream(fullPath, FileMode.Create);
        await fileStream.CopyToAsync(fs, ct);

        _logger.LogInformation("Saved file to local storage: {Path}", relativePath);
        return relativePath;
    }

    public Task<(Stream Stream, string ContentType)?> GetFileAsync(string storedPath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, storedPath);
        if (!File.Exists(fullPath))
            return Task.FromResult<(Stream, string)?>(null);

        var stream = (Stream)new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        var contentType = GetContentType(storedPath);
        return Task.FromResult<(Stream, string)?>((stream, contentType));
    }

    public Task DeleteFileAsync(string storedPath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, storedPath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Deleted file from local storage: {Path}", storedPath);
        }
        return Task.CompletedTask;
    }

    private static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }
}
