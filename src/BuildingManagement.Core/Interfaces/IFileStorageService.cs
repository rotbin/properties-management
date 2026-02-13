namespace BuildingManagement.Core.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(string entityType, int entityId, string fileName, string contentType, Stream fileStream, CancellationToken ct = default);
    Task<(Stream Stream, string ContentType)?> GetFileAsync(string storedPath, CancellationToken ct = default);
    Task DeleteFileAsync(string storedPath, CancellationToken ct = default);
}
