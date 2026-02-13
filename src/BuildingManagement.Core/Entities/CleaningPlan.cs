using System.ComponentModel.DataAnnotations;

namespace BuildingManagement.Core.Entities;

public class CleaningPlan : BaseEntity
{
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public int CleaningVendorId { get; set; }
    public Vendor CleaningVendor { get; set; } = null!;

    public int StairwellsPerWeek { get; set; }
    public int ParkingPerWeek { get; set; }
    public int CorridorLobbyPerWeek { get; set; }
    public int GarbageRoomPerWeek { get; set; }

    public DateTime EffectiveFrom { get; set; }
}
