using System.ComponentModel.DataAnnotations;

namespace BuildingManagement.Core.DTOs;

public record BuildingDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public string? PostalCode { get; init; }
    public string? Notes { get; init; }
    public int UnitCount { get; init; }
}

public record CreateBuildingRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? AddressLine { get; init; }

    [MaxLength(100)]
    public string? City { get; init; }

    [MaxLength(20)]
    public string? PostalCode { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public record UpdateBuildingRequest : CreateBuildingRequest;

public record UnitDto
{
    public int Id { get; init; }
    public int BuildingId { get; init; }
    public string UnitNumber { get; init; } = string.Empty;
    public int? Floor { get; init; }
    public decimal? SizeSqm { get; init; }
    public string? OwnerName { get; init; }
    public string? TenantUserId { get; init; }
    public string? TenantName { get; init; }
}

public record CreateUnitRequest
{
    [Required]
    public int BuildingId { get; init; }

    [Required, MaxLength(20)]
    public string UnitNumber { get; init; } = string.Empty;

    public int? Floor { get; init; }
    public decimal? SizeSqm { get; init; }

    [MaxLength(200)]
    public string? OwnerName { get; init; }

    public string? TenantUserId { get; init; }
}
