using BuildingManagement.Core.Interfaces;
using BuildingManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BuildingManagement.Api.Controllers;

[ApiController]
[Route("api/files")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _fileStorage;

    public FilesController(AppDbContext db, IFileStorageService fileStorage)
    {
        _db = db;
        _fileStorage = fileStorage;
    }

    [HttpGet("sr-{id}")]
    public async Task<IActionResult> GetServiceRequestAttachment(int id)
    {
        var attachment = await _db.ServiceRequestAttachments.FindAsync(id);
        if (attachment == null) return NotFound();

        var result = await _fileStorage.GetFileAsync(attachment.StoredPath);
        if (result == null) return NotFound();

        return File(result.Value.Stream, result.Value.ContentType, attachment.FileName);
    }

    [HttpGet("wo-{id}")]
    public async Task<IActionResult> GetWorkOrderAttachment(int id)
    {
        var attachment = await _db.WorkOrderAttachments.FindAsync(id);
        if (attachment == null) return NotFound();

        var result = await _fileStorage.GetFileAsync(attachment.StoredPath);
        if (result == null) return NotFound();

        return File(result.Value.Stream, result.Value.ContentType, attachment.FileName);
    }

    [HttpDelete("sr-{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteServiceRequestAttachment(int id)
    {
        var attachment = await _db.ServiceRequestAttachments.FindAsync(id);
        if (attachment == null) return NotFound();

        await _fileStorage.DeleteFileAsync(attachment.StoredPath);
        _db.ServiceRequestAttachments.Remove(attachment);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("wo-{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteWorkOrderAttachment(int id)
    {
        var attachment = await _db.WorkOrderAttachments.FindAsync(id);
        if (attachment == null) return NotFound();

        await _fileStorage.DeleteFileAsync(attachment.StoredPath);
        _db.WorkOrderAttachments.Remove(attachment);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
