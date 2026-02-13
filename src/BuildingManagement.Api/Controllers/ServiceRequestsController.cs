using BuildingManagement.Core.DTOs;
using BuildingManagement.Core.Entities;
using BuildingManagement.Core.Enums;
using BuildingManagement.Core.Interfaces;
using BuildingManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BuildingManagement.Api.Controllers;

[ApiController]
[Route("api/servicerequests")]
[Authorize]
public class ServiceRequestsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _fileStorage;
    private readonly IEmailService _emailService;
    private readonly ILogger<ServiceRequestsController> _logger;

    public ServiceRequestsController(AppDbContext db, IFileStorageService fileStorage, IEmailService emailService, ILogger<ServiceRequestsController> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Tenant},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<ServiceRequestDto>> Create([FromBody] CreateServiceRequestRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        var sr = new ServiceRequest
        {
            BuildingId = request.BuildingId,
            UnitId = request.UnitId,
            SubmittedByUserId = userId,
            SubmittedByName = user.FullName,
            Phone = request.Phone ?? user.Phone,
            Email = user.Email,
            Area = request.Area,
            Category = request.Category,
            Priority = request.Priority,
            IsEmergency = request.IsEmergency,
            Description = request.Description,
            Status = ServiceRequestStatus.New,
            CreatedBy = userId
        };

        _db.ServiceRequests.Add(sr);
        await _db.SaveChangesAsync();

        await _emailService.SendEmailAsync(
            "manager@example.com",
            $"New Service Request #{sr.Id}",
            $"A new service request has been submitted by {user.FullName}: {request.Description}");

        return CreatedAtAction(nameof(GetById), new { id = sr.Id }, MapToDto(sr));
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<List<ServiceRequestDto>>> GetAll(
        [FromQuery] int? buildingId,
        [FromQuery] int? unitId,
        [FromQuery] ServiceRequestStatus? status)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAdmin = User.IsInRole(AppRoles.Admin);

        IQueryable<ServiceRequest> query = _db.ServiceRequests
            .Include(sr => sr.Building)
            .Include(sr => sr.Unit)
            .Include(sr => sr.Attachments);

        // Manager: filter to their buildings
        if (!isAdmin)
        {
            var buildingIds = await _db.BuildingManagers
                .Where(bm => bm.UserId == userId)
                .Select(bm => bm.BuildingId)
                .ToListAsync();
            query = query.Where(sr => buildingIds.Contains(sr.BuildingId));
        }

        if (buildingId.HasValue) query = query.Where(sr => sr.BuildingId == buildingId);
        if (unitId.HasValue) query = query.Where(sr => sr.UnitId == unitId);
        if (status.HasValue) query = query.Where(sr => sr.Status == status);

        var items = await query.OrderByDescending(sr => sr.CreatedAtUtc).ToListAsync();
        return Ok(items.Select(MapToDto).ToList());
    }

    [HttpGet("my")]
    [Authorize(Roles = AppRoles.Tenant)]
    public async Task<ActionResult<List<ServiceRequestDto>>> GetMy()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

        var items = await _db.ServiceRequests
            .Include(sr => sr.Building)
            .Include(sr => sr.Unit)
            .Include(sr => sr.Attachments)
            .Where(sr => sr.SubmittedByUserId == userId)
            .OrderByDescending(sr => sr.CreatedAtUtc)
            .ToListAsync();

        return Ok(items.Select(MapToDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ServiceRequestDto>> GetById(int id)
    {
        var sr = await _db.ServiceRequests
            .Include(sr => sr.Building)
            .Include(sr => sr.Unit)
            .Include(sr => sr.Attachments)
            .FirstOrDefaultAsync(sr => sr.Id == id);

        if (sr == null) return NotFound();

        // Tenants can only see their own
        if (User.IsInRole(AppRoles.Tenant))
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            if (sr.SubmittedByUserId != userId) return Forbid();
        }

        return Ok(MapToDto(sr));
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateServiceRequestStatusRequest request)
    {
        var sr = await _db.ServiceRequests.FindAsync(id);
        if (sr == null) return NotFound();

        sr.Status = request.Status;
        sr.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await _db.SaveChangesAsync();

        if (request.Status == ServiceRequestStatus.Resolved)
        {
            await _emailService.SendEmailAsync(
                sr.Email ?? "",
                $"Service Request #{sr.Id} Resolved",
                $"Your service request has been resolved.");
        }

        return NoContent();
    }

    [HttpPost("{id}/attachments")]
    [Authorize]
    [RequestSizeLimit(52_428_800)] // 50MB total
    public async Task<ActionResult<List<AttachmentDto>>> UploadAttachments(int id, [FromForm] List<IFormFile> files)
    {
        var sr = await _db.ServiceRequests.Include(s => s.Attachments).FirstOrDefaultAsync(s => s.Id == id);
        if (sr == null) return NotFound();

        if (sr.Attachments.Count + files.Count > 5)
            return BadRequest(new { message = "Maximum 5 attachments per service request." });

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        var result = new List<AttachmentDto>();

        foreach (var file in files)
        {
            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { message = $"File {file.FileName} exceeds 10MB limit." });

            if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
                return BadRequest(new { message = $"File type {file.ContentType} is not allowed. Use JPG, PNG, or WebP." });

            using var stream = file.OpenReadStream();
            var storedPath = await _fileStorage.SaveFileAsync("servicerequests", sr.Id, file.FileName, file.ContentType, stream);

            var attachment = new ServiceRequestAttachment
            {
                ServiceRequestId = sr.Id,
                FileName = file.FileName,
                ContentType = file.ContentType,
                StoredPath = storedPath
            };

            _db.ServiceRequestAttachments.Add(attachment);
            await _db.SaveChangesAsync();

            result.Add(new AttachmentDto
            {
                Id = attachment.Id,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                Url = $"/api/files/sr-{attachment.Id}",
                UploadedAtUtc = attachment.UploadedAtUtc
            });
        }

        return Ok(result);
    }

    private static ServiceRequestDto MapToDto(ServiceRequest sr)
    {
        return new ServiceRequestDto
        {
            Id = sr.Id,
            BuildingId = sr.BuildingId,
            BuildingName = sr.Building?.Name,
            UnitId = sr.UnitId,
            UnitNumber = sr.Unit?.UnitNumber,
            SubmittedByUserId = sr.SubmittedByUserId,
            SubmittedByName = sr.SubmittedByName,
            Phone = sr.Phone,
            Email = sr.Email,
            Area = sr.Area,
            Category = sr.Category,
            Priority = sr.Priority,
            IsEmergency = sr.IsEmergency,
            Description = sr.Description,
            Status = sr.Status,
            CreatedAtUtc = sr.CreatedAtUtc,
            UpdatedAtUtc = sr.UpdatedAtUtc,
            Attachments = sr.Attachments?.Select(a => new AttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                ContentType = a.ContentType,
                Url = $"/api/files/sr-{a.Id}",
                UploadedAtUtc = a.UploadedAtUtc
            }).ToList() ?? []
        };
    }
}
