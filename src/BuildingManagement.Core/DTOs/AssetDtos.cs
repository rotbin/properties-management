using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.DTOs;

public record AssetDto
{
    public int Id { get; init; }
    public int BuildingId { get; init; }
    public string? BuildingName { get; init; }
    public string Name { get; init; } = string.Empty;
    public AssetType AssetType { get; init; }
    public string? LocationDescription { get; init; }
    public string? SerialNumber { get; init; }
    public DateTime? InstallDate { get; init; }
    public DateTime? WarrantyUntil { get; init; }
    public int? VendorId { get; init; }
    public string? VendorName { get; init; }
    public string? Notes { get; init; }
}

public record CreateAssetRequest
{
    [Required]
    public int BuildingId { get; init; }

    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    public AssetType AssetType { get; init; }

    [MaxLength(500)]
    public string? LocationDescription { get; init; }

    [MaxLength(100)]
    public string? SerialNumber { get; init; }

    public DateTime? InstallDate { get; init; }
    public DateTime? WarrantyUntil { get; init; }
    public int? VendorId { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public record PreventivePlanDto
{
    public int Id { get; init; }
    public int AssetId { get; init; }
    public string? AssetName { get; init; }
    public string Title { get; init; } = string.Empty;
    public FrequencyType FrequencyType { get; init; }
    public int Interval { get; init; }
    public DateTime NextDueDate { get; init; }
    public string? ChecklistText { get; init; }
}

public record CreatePreventivePlanRequest
{
    [Required]
    public int AssetId { get; init; }

    [Required, MaxLength(300)]
    public string Title { get; init; } = string.Empty;

    public FrequencyType FrequencyType { get; init; }
    public int Interval { get; init; } = 1;
    public DateTime NextDueDate { get; init; }

    [MaxLength(2000)]
    public string? ChecklistText { get; init; }
}
