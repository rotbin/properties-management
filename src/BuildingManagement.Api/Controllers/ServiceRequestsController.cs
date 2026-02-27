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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ServiceRequestsController> _logger;

    public ServiceRequestsController(AppDbContext db, IFileStorageService fileStorage, IEmailService emailService, IServiceScopeFactory scopeFactory, ILogger<ServiceRequestsController> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _emailService = emailService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Tenant},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<ServiceRequestDto>> Create([FromBody] CreateServiceRequestRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        // Resolve phone: request > tenant profile > user
        var phone = request.Phone;
        if (string.IsNullOrWhiteSpace(phone))
        {
            var tenantProfile = await _db.TenantProfiles
                .Where(tp => tp.UserId == userId && tp.IsActive && !tp.IsDeleted)
                .FirstOrDefaultAsync();
            phone = tenantProfile?.Phone ?? user.Phone;
        }

        if (string.IsNullOrWhiteSpace(phone))
            return BadRequest(new { message = "Callback phone is required." });

        // Tenant: restrict to their building(s)
        if (User.IsInRole(AppRoles.Tenant) && !User.IsInRole(AppRoles.Admin))
        {
            var tenantBuildingIds = await _db.TenantProfiles
                .Where(tp => tp.UserId == userId && tp.IsActive && !tp.IsDeleted)
                .Select(tp => tp.Unit.BuildingId)
                .Distinct()
                .ToListAsync();
            if (tenantBuildingIds.Count > 0 && !tenantBuildingIds.Contains(request.BuildingId))
                return BadRequest(new { message = "You can only create tickets for your building." });
        }

        var building = await _db.Buildings.FindAsync(request.BuildingId);

        var sr = new ServiceRequest
        {
            BuildingId = request.BuildingId,
            UnitId = request.UnitId,
            SubmittedByUserId = userId,
            SubmittedByName = user.FullName,
            Phone = phone,
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

        // Load building for MapToDto
        sr.Building = building!;

        await _emailService.SendEmailAsync(
            "manager@example.com",
            $"New Service Request #{sr.Id}",
            $"A new service request has been submitted by {user.FullName}: {request.Description}");

        // Trigger AI agent analysis (fire-and-forget via singleton scope factory)
        var srId = sr.Id;
        var scopeFactory = _scopeFactory;
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var msgController = scope.ServiceProvider.GetRequiredService<TicketMessagesController>();
                await msgController.TriggerAgentOnNewTicketAsync(srId);
            }
            catch (Exception ex) { Console.WriteLine($"AI agent trigger failed for ticket #{srId}: {ex}"); }
        });

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
            .Include(sr => sr.Attachments)
            .Include(sr => sr.WorkOrders).ThenInclude(wo => wo.Vendor)
            .Include(sr => sr.IncidentGroup).ThenInclude(ig => ig!.ServiceRequests)
            .Include(sr => sr.Messages);

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
        var readReceipts = await GetReadReceipts(userId!, items.Select(s => s.Id).ToList());
        return Ok(items.Select(sr => MapToDto(sr, readReceipts)).ToList());
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
            .Include(sr => sr.WorkOrders).ThenInclude(wo => wo.Vendor)
            .Include(sr => sr.IncidentGroup).ThenInclude(ig => ig!.ServiceRequests)
            .Include(sr => sr.Messages)
            .Where(sr => sr.SubmittedByUserId == userId)
            .OrderByDescending(sr => sr.CreatedAtUtc)
            .ToListAsync();

        var readReceipts = await GetReadReceipts(userId, items.Select(s => s.Id).ToList());
        return Ok(items.Select(sr => MapToDto(sr, readReceipts)).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ServiceRequestDto>> GetById(int id)
    {
        var sr = await _db.ServiceRequests
            .Include(sr => sr.Building)
            .Include(sr => sr.Unit)
            .Include(sr => sr.Attachments)
            .Include(sr => sr.WorkOrders).ThenInclude(wo => wo.Vendor)
            .Include(sr => sr.IncidentGroup).ThenInclude(ig => ig!.ServiceRequests)
            .Include(sr => sr.Messages)
            .FirstOrDefaultAsync(sr => sr.Id == id);

        if (sr == null) return NotFound();

        // Tenants can only see their own
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        if (User.IsInRole(AppRoles.Tenant))
        {
            if (sr.SubmittedByUserId != currentUserId) return Forbid();
        }

        var readReceipts = await GetReadReceipts(currentUserId, new List<int> { sr.Id });
        return Ok(MapToDto(sr, readReceipts));
    }

    /// <summary>Assign a vendor to a service request by creating/updating a linked work order.</summary>
    [HttpPut("{id}/assign-vendor")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<object>> AssignVendor(int id, [FromBody] AssignVendorToSrRequest request)
    {
        var sr = await _db.ServiceRequests
            .Include(s => s.Building)
            .Include(s => s.WorkOrders)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sr == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAdmin = User.IsInRole(AppRoles.Admin);

        // Manager building access check
        if (!isAdmin)
        {
            var hasAccess = await _db.BuildingManagers.AnyAsync(bm => bm.UserId == userId && bm.BuildingId == sr.BuildingId);
            if (!hasAccess) return Forbid();
        }

        var vendor = await _db.Vendors.FindAsync(request.VendorId);
        if (vendor == null) return BadRequest(new { message = "Vendor not found." });

        // Find existing work order for this SR, or create new (idempotent)
        var wo = sr.WorkOrders.FirstOrDefault();
        var defaultTitle = request.Title ?? $"{sr.Building?.Name} – {sr.Area} – {sr.Category}";
        if (wo == null)
        {
            wo = new WorkOrder
            {
                BuildingId = sr.BuildingId,
                ServiceRequestId = sr.Id,
                Title = defaultTitle,
                Description = request.Notes ?? sr.Description,
                VendorId = request.VendorId,
                ScheduledFor = request.ScheduledFor ?? DateTime.UtcNow,
                Status = WorkOrderStatus.Assigned,
                CreatedBy = userId
            };
            _db.WorkOrders.Add(wo);
        }
        else
        {
            wo.VendorId = request.VendorId;
            wo.ScheduledFor = request.ScheduledFor ?? wo.ScheduledFor;
            if (!string.IsNullOrEmpty(request.Title)) wo.Title = request.Title;
            if (!string.IsNullOrEmpty(request.Notes)) wo.Description = request.Notes;
            wo.Status = WorkOrderStatus.Assigned;
            wo.UpdatedBy = userId;
        }

        // Update SR status
        sr.Status = ServiceRequestStatus.Assigned;
        sr.UpdatedBy = userId;

        await _db.SaveChangesAsync();

        // Send notification to vendor
        if (vendor.Email != null)
        {
            await _emailService.SendEmailAsync(vendor.Email,
                $"New assignment: Ticket #{sr.Id}",
                $"You have been assigned to ticket #{sr.Id}: {sr.Description}");
        }

        return Ok(new
        {
            serviceRequestId = sr.Id,
            serviceRequestStatus = sr.Status.ToString(),
            workOrderId = wo.Id,
            workOrderStatus = wo.Status.ToString(),
            vendorName = vendor.Name
        });
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

            // Trigger AI agent satisfaction follow-up via singleton scope factory
            var resolvedSrId = sr.Id;
            var resolvedScopeFactory = _scopeFactory;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = resolvedScopeFactory.CreateScope();
                    var msgController = scope.ServiceProvider.GetRequiredService<TicketMessagesController>();
                    await msgController.TriggerAgentOnResolutionAsync(resolvedSrId);
                }
                catch (Exception ex) { Console.WriteLine($"AI agent resolution follow-up failed for ticket #{resolvedSrId}: {ex}"); }
            });
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

    private async Task<Dictionary<int, int>> GetReadReceipts(string userId, List<int> srIds)
    {
        return await _db.TicketReadReceipts
            .Where(r => r.UserId == userId && srIds.Contains(r.ServiceRequestId))
            .ToDictionaryAsync(r => r.ServiceRequestId, r => r.LastReadMessageId);
    }

    private static ServiceRequestDto MapToDto(ServiceRequest sr, Dictionary<int, int>? readReceipts = null)
    {
        var linkedWo = sr.WorkOrders?.FirstOrDefault();
        var maxMsgId = sr.Messages?.Any() == true ? sr.Messages.Max(m => m.Id) : 0;
        var lastRead = readReceipts != null && readReceipts.TryGetValue(sr.Id, out var lr) ? lr : 0;
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
            }).ToList() ?? [],
            AssignedVendorId = linkedWo?.VendorId,
            AssignedVendorName = linkedWo?.Vendor?.Name,
            LinkedWorkOrderId = linkedWo?.Id,
            LinkedWorkOrderStatus = linkedWo?.Status.ToString(),
            IncidentGroupId = sr.IncidentGroupId,
            IncidentGroupTitle = sr.IncidentGroup?.Title,
            IncidentTicketCount = sr.IncidentGroup?.ServiceRequests?.Count ?? 0,
            MessageCount = sr.Messages?.Count ?? 0,
            HasUnreadMessages = maxMsgId > lastRead
        };
    }
}
