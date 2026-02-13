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
[Route("api/workorders")]
[Authorize]
public class WorkOrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _fileStorage;
    private readonly IEmailService _emailService;

    public WorkOrdersController(AppDbContext db, IFileStorageService fileStorage, IEmailService emailService)
    {
        _db = db;
        _fileStorage = fileStorage;
        _emailService = emailService;
    }

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<WorkOrderDto>> Create([FromBody] CreateWorkOrderRequest request)
    {
        var wo = new WorkOrder
        {
            BuildingId = request.BuildingId,
            ServiceRequestId = request.ServiceRequestId,
            VendorId = request.VendorId,
            Title = request.Title,
            Description = request.Description,
            ScheduledFor = request.ScheduledFor,
            Status = request.VendorId.HasValue ? WorkOrderStatus.Assigned : WorkOrderStatus.Draft,
            CreatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        };

        _db.WorkOrders.Add(wo);

        // Update SR status if linked
        if (request.ServiceRequestId.HasValue)
        {
            var sr = await _db.ServiceRequests.FindAsync(request.ServiceRequestId.Value);
            if (sr != null)
            {
                sr.Status = ServiceRequestStatus.InProgress;
                sr.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }
        }

        await _db.SaveChangesAsync();

        if (request.VendorId.HasValue)
        {
            var vendor = await _db.Vendors.FindAsync(request.VendorId.Value);
            if (vendor?.Email != null)
            {
                await _emailService.SendEmailAsync(vendor.Email,
                    $"New Work Order #{wo.Id} Assigned",
                    $"A new work order has been assigned to you: {wo.Title}");
            }
        }

        return CreatedAtAction(nameof(GetById), new { id = wo.Id }, await GetWorkOrderDto(wo.Id));
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<List<WorkOrderDto>>> GetAll(
        [FromQuery] int? buildingId,
        [FromQuery] int? vendorId,
        [FromQuery] WorkOrderStatus? status)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAdmin = User.IsInRole(AppRoles.Admin);

        IQueryable<WorkOrder> query = _db.WorkOrders
            .Include(wo => wo.Building)
            .Include(wo => wo.Vendor)
            .Include(wo => wo.Notes).ThenInclude(n => n.CreatedByUser)
            .Include(wo => wo.Attachments);

        if (!isAdmin)
        {
            var buildingIds = await _db.BuildingManagers
                .Where(bm => bm.UserId == userId)
                .Select(bm => bm.BuildingId)
                .ToListAsync();
            query = query.Where(wo => buildingIds.Contains(wo.BuildingId));
        }

        if (buildingId.HasValue) query = query.Where(wo => wo.BuildingId == buildingId);
        if (vendorId.HasValue) query = query.Where(wo => wo.VendorId == vendorId);
        if (status.HasValue) query = query.Where(wo => wo.Status == status);

        var items = await query.OrderByDescending(wo => wo.CreatedAtUtc).ToListAsync();
        return Ok(items.Select(MapToDto).ToList());
    }

    [HttpGet("my")]
    [Authorize(Roles = AppRoles.Vendor)]
    public async Task<ActionResult<List<WorkOrderDto>>> GetMyWorkOrders()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var user = await _db.Users.FindAsync(userId);
        if (user?.VendorId == null) return Ok(new List<WorkOrderDto>());

        var items = await _db.WorkOrders
            .Include(wo => wo.Building)
            .Include(wo => wo.Vendor)
            .Include(wo => wo.Notes).ThenInclude(n => n.CreatedByUser)
            .Include(wo => wo.Attachments)
            .Where(wo => wo.VendorId == user.VendorId)
            .OrderByDescending(wo => wo.CreatedAtUtc)
            .ToListAsync();

        return Ok(items.Select(MapToDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkOrderDto>> GetById(int id)
    {
        var dto = await GetWorkOrderDto(id);
        if (dto == null) return NotFound();

        // Vendor can only see their own
        if (User.IsInRole(AppRoles.Vendor) && !User.IsInRole(AppRoles.Admin))
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            var user = await _db.Users.FindAsync(userId);
            if (user?.VendorId != dto.VendorId) return Forbid();
        }

        return Ok(dto);
    }

    [HttpPut("{id}/assign")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignWorkOrderRequest request)
    {
        var wo = await _db.WorkOrders.FindAsync(id);
        if (wo == null) return NotFound();

        wo.VendorId = request.VendorId;
        if (request.ScheduledFor.HasValue) wo.ScheduledFor = request.ScheduledFor;
        wo.Status = WorkOrderStatus.Assigned;
        wo.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        await _db.SaveChangesAsync();

        var vendor = await _db.Vendors.FindAsync(request.VendorId);
        if (vendor?.Email != null)
        {
            await _emailService.SendEmailAsync(vendor.Email,
                $"Work Order #{wo.Id} Assigned",
                $"Work order '{wo.Title}' has been assigned to you.");
        }

        return NoContent();
    }

    [HttpPut("{id}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateWorkOrderStatusRequest request)
    {
        var wo = await _db.WorkOrders.FindAsync(id);
        if (wo == null) return NotFound();

        wo.Status = request.Status;
        wo.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (request.Status == WorkOrderStatus.Completed)
            wo.CompletedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        if (request.Status == WorkOrderStatus.Completed && wo.ServiceRequestId.HasValue)
        {
            var sr = await _db.ServiceRequests.FindAsync(wo.ServiceRequestId.Value);
            if (sr != null)
            {
                sr.Status = ServiceRequestStatus.Resolved;
                sr.UpdatedBy = "System";
                await _db.SaveChangesAsync();

                await _emailService.SendEmailAsync(sr.Email ?? "",
                    $"Service Request #{sr.Id} Completed",
                    $"Your service request has been completed.");
            }
        }

        return NoContent();
    }

    [HttpPost("{id}/notes")]
    [Authorize]
    public async Task<ActionResult<WorkOrderNoteDto>> AddNote(int id, [FromBody] CreateWorkOrderNoteRequest request)
    {
        var wo = await _db.WorkOrders.FindAsync(id);
        if (wo == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var note = new WorkOrderNote
        {
            WorkOrderId = id,
            NoteText = request.NoteText,
            CreatedByUserId = userId
        };

        _db.WorkOrderNotes.Add(note);
        await _db.SaveChangesAsync();

        var user = await _db.Users.FindAsync(userId);

        return Ok(new WorkOrderNoteDto
        {
            Id = note.Id,
            NoteText = note.NoteText,
            CreatedAtUtc = note.CreatedAtUtc,
            CreatedByUserId = note.CreatedByUserId,
            CreatedByName = user?.FullName
        });
    }

    [HttpPost("{id}/attachments")]
    [Authorize]
    [RequestSizeLimit(52_428_800)]
    public async Task<ActionResult<List<AttachmentDto>>> UploadAttachments(int id, [FromForm] List<IFormFile> files)
    {
        var wo = await _db.WorkOrders.Include(w => w.Attachments).FirstOrDefaultAsync(w => w.Id == id);
        if (wo == null) return NotFound();

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        var result = new List<AttachmentDto>();

        foreach (var file in files)
        {
            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { message = $"File {file.FileName} exceeds 10MB limit." });

            if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
                return BadRequest(new { message = $"File type {file.ContentType} is not allowed." });

            using var stream = file.OpenReadStream();
            var storedPath = await _fileStorage.SaveFileAsync("workorders", wo.Id, file.FileName, file.ContentType, stream);

            var attachment = new WorkOrderAttachment
            {
                WorkOrderId = wo.Id,
                FileName = file.FileName,
                ContentType = file.ContentType,
                StoredPath = storedPath
            };

            _db.WorkOrderAttachments.Add(attachment);
            await _db.SaveChangesAsync();

            result.Add(new AttachmentDto
            {
                Id = attachment.Id,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                Url = $"/api/files/wo-{attachment.Id}",
                UploadedAtUtc = attachment.UploadedAtUtc
            });
        }

        return Ok(result);
    }

    private async Task<WorkOrderDto?> GetWorkOrderDto(int id)
    {
        var wo = await _db.WorkOrders
            .Include(wo => wo.Building)
            .Include(wo => wo.Vendor)
            .Include(wo => wo.Notes).ThenInclude(n => n.CreatedByUser)
            .Include(wo => wo.Attachments)
            .FirstOrDefaultAsync(wo => wo.Id == id);

        return wo == null ? null : MapToDto(wo);
    }

    private static WorkOrderDto MapToDto(WorkOrder wo) => new()
    {
        Id = wo.Id,
        BuildingId = wo.BuildingId,
        BuildingName = wo.Building?.Name,
        ServiceRequestId = wo.ServiceRequestId,
        VendorId = wo.VendorId,
        VendorName = wo.Vendor?.Name,
        Title = wo.Title,
        Description = wo.Description,
        ScheduledFor = wo.ScheduledFor,
        Status = wo.Status,
        CreatedAtUtc = wo.CreatedAtUtc,
        UpdatedAtUtc = wo.UpdatedAtUtc,
        CompletedAtUtc = wo.CompletedAtUtc,
        Notes = wo.Notes?.Select(n => new WorkOrderNoteDto
        {
            Id = n.Id,
            NoteText = n.NoteText,
            CreatedAtUtc = n.CreatedAtUtc,
            CreatedByUserId = n.CreatedByUserId,
            CreatedByName = n.CreatedByUser?.FullName
        }).ToList() ?? [],
        Attachments = wo.Attachments?.Select(a => new AttachmentDto
        {
            Id = a.Id,
            FileName = a.FileName,
            ContentType = a.ContentType,
            Url = $"/api/files/wo-{a.Id}",
            UploadedAtUtc = a.UploadedAtUtc
        }).ToList() ?? []
    };
}
