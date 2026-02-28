using System.ComponentModel.DataAnnotations;

namespace BuildingManagement.Core.DTOs;

public record TenantProfileDto
{
    public int Id { get; init; }
    public int UnitId { get; init; }
    public string? UnitNumber { get; init; }
    public int? Floor { get; init; }
    public int BuildingId { get; init; }
    public string? BuildingName { get; init; }
    public string? UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public DateTime? MoveInDate { get; init; }
    public DateTime? MoveOutDate { get; init; }
    public bool IsActive { get; init; }
    public bool IsArchived { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public record CreateTenantRequest
{
    [Required]
    public int UnitId { get; init; }

    [Required, MaxLength(200)]
    public string FullName { get; init; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; init; }

    [MaxLength(200)]
    public string? Email { get; init; }

    public DateTime? MoveInDate { get; init; }

    public bool IsActive { get; init; } = true;

    [MaxLength(1000)]
    public string? Notes { get; init; }

    public string? UserId { get; init; }
}

public record UpdateTenantRequest
{
    [Required, MaxLength(200)]
    public string FullName { get; init; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; init; }

    [MaxLength(200)]
    public string? Email { get; init; }

    public DateTime? MoveInDate { get; init; }

    public DateTime? MoveOutDate { get; init; }

    public bool IsActive { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public record EndTenancyRequest
{
    [Required]
    public DateTime MoveOutDate { get; init; }
}

// ─── Tenant Messages ───────────────────────────────────

public record TenantMessageDto
{
    public int Id { get; init; }
    public int TenantProfileId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public string? SentByUserId { get; init; }
    public string? SentByName { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string MessageType { get; init; } = string.Empty;
    public string? PayerCategory { get; init; }
    public bool IsRead { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ReadAtUtc { get; init; }
    public int? ParentMessageId { get; init; }
    public int ReplyCount { get; init; }
    public DateTime? LastReplyAtUtc { get; init; }
    public List<TenantMessageDto>? Replies { get; init; }
}

public record SendTenantMessageRequest
{
    [Required, MaxLength(100)]
    public string Subject { get; init; } = string.Empty;

    [Required]
    public string Body { get; init; } = string.Empty;

    public int? ParentMessageId { get; init; }
}

public record SendPaymentRemindersRequest
{
    [Required]
    public int BuildingId { get; init; }
}

public record PaymentAnalysisDto
{
    public int TenantProfileId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public string? UnitNumber { get; init; }
    public string? BuildingName { get; init; }
    public string PayerCategory { get; init; } = string.Empty;
    public decimal TotalDue { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal Outstanding { get; init; }
    public int OverdueCount { get; init; }
    public int TotalCharges { get; init; }
    public int LatePayments { get; init; }
    public double OnTimeRate { get; init; }
    public bool HasStandingOrder { get; init; }
}
