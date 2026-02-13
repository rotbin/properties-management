using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BuildingManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Services;

public class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureBlobStorageService> _logger;

    public AzureBlobStorageService(string connectionString, string containerName, ILogger<AzureBlobStorageService> logger)
    {
        var serviceClient = new BlobServiceClient(connectionString);
        _containerClient = serviceClient.GetBlobContainerClient(containerName);
        _containerClient.CreateIfNotExists();
        _logger = logger;
    }

    public async Task<string> SaveFileAsync(string entityType, int entityId, string fileName, string contentType, Stream fileStream, CancellationToken ct = default)
    {
        var blobName = $"{entityType}/{entityId}/{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        var blobClient = _containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(fileStream, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);
        _logger.LogInformation("Uploaded blob: {BlobName}", blobName);

        return blobName;
    }

    public async Task<(Stream Stream, string ContentType)?> GetFileAsync(string storedPath, CancellationToken ct = default)
    {
        var blobClient = _containerClient.GetBlobClient(storedPath);
        if (!await blobClient.ExistsAsync(ct))
            return null;

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
        return (response.Value.Content, response.Value.Details.ContentType);
    }

    public async Task DeleteFileAsync(string storedPath, CancellationToken ct = default)
    {
        var blobClient = _containerClient.GetBlobClient(storedPath);
        await blobClient.DeleteIfExistsAsync(cancellationToken: ct);
        _logger.LogInformation("Deleted blob: {BlobName}", storedPath);
    }
}
