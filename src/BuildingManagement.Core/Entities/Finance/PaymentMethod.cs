using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Entities.Finance;

public class PaymentMethod
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public PaymentMethodType MethodType { get; set; }

    [MaxLength(50)]
    public string? Provider { get; set; }

    /// <summary>Tokenized reference from payment gateway. NEVER store raw card data.</summary>
    [Required, MaxLength(500)]
    public string Token { get; set; } = string.Empty;

    [MaxLength(4)]
    public string? Last4Digits { get; set; }

    [MaxLength(7)]
    public string? Expiry { get; set; }

    [MaxLength(20)]
    public string? CardBrand { get; set; }

    /// <summary>Provider customer ID (for Meshulam/Pelecard customer references)</summary>
    [MaxLength(200)]
    public string? ProviderCustomerId { get; set; }

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
