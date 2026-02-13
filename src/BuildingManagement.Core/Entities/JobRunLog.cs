using System.ComponentModel.DataAnnotations;

namespace BuildingManagement.Core.Entities;

public class JobRunLog
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string JobName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string PeriodKey { get; set; } = string.Empty;

    public DateTime RanAtUtc { get; set; } = DateTime.UtcNow;
}
