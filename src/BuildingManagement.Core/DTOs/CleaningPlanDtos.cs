using System.ComponentModel.DataAnnotations;

namespace BuildingManagement.Core.DTOs;

public record CleaningPlanDto
{
    public int Id { get; init; }
    public int BuildingId { get; init; }
    public int CleaningVendorId { get; init; }
    public string? CleaningVendorName { get; init; }
    public int StairwellsPerWeek { get; init; }
    public int ParkingPerWeek { get; init; }
    public int CorridorLobbyPerWeek { get; init; }
    public int GarbageRoomPerWeek { get; init; }
    public DateTime EffectiveFrom { get; init; }
}

public record CreateCleaningPlanRequest
{
    [Required]
    public int CleaningVendorId { get; init; }

    [Range(0, 7)]
    public int StairwellsPerWeek { get; init; }

    [Range(0, 7)]
    public int ParkingPerWeek { get; init; }

    [Range(0, 7)]
    public int CorridorLobbyPerWeek { get; init; }

    [Range(0, 7)]
    public int GarbageRoomPerWeek { get; init; }

    public DateTime EffectiveFrom { get; init; }
}
