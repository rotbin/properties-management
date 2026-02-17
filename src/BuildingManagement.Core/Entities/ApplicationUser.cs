using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace BuildingManagement.Core.Entities;

public class ApplicationUser : IdentityUser
{
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    /// <summary>Preferred UI language: "he" | "en"</summary>
    [MaxLength(5)]
    public string PreferredLanguage { get; set; } = "he";

    /// <summary>External accounting provider issuer profile ID (for managers issuing invoices).</summary>
    [MaxLength(200)]
    public string? IssuerProfileId { get; set; }

    // For vendor users
    public int? VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    // Buildings this user manages (Manager role)
    public ICollection<BuildingManager> ManagedBuildings { get; set; } = new List<BuildingManager>();

    // Units this user is tenant of
    public ICollection<Unit> TenantUnits { get; set; } = new List<Unit>();
}

/// <summary>
/// Join table: Manager -> Buildings they manage
/// </summary>
public class BuildingManager
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;
}
