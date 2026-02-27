using System.ComponentModel.DataAnnotations;

namespace BuildingManagement.Core.Entities;

public class IncidentGroup
{
    public int Id { get; set; }

    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();
}
