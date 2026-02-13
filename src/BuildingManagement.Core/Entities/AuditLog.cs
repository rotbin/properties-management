using System.ComponentModel.DataAnnotations;

namespace BuildingManagement.Core.Entities;

public class AuditLog
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? EntityId { get; set; }

    [Required, MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? PerformedBy { get; set; }

    public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(4000)]
    public string? Details { get; set; }
}
