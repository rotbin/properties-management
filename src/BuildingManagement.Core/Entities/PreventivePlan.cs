using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Entities;

public class PreventivePlan : BaseEntity
{
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public FrequencyType FrequencyType { get; set; }
    public int Interval { get; set; } = 1;

    public DateTime NextDueDate { get; set; }

    [MaxLength(2000)]
    public string? ChecklistText { get; set; }
}
