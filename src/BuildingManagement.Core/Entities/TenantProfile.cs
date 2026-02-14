using System.ComponentModel.DataAnnotations;

namespace BuildingManagement.Core.Entities;

public class TenantProfile : BaseEntity
{
    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    /// <summary>Link to Identity user if tenant can login (nullable).</summary>
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    public DateTime? MoveInDate { get; set; }

    public DateTime? MoveOutDate { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsArchived { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
