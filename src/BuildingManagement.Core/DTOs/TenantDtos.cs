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
