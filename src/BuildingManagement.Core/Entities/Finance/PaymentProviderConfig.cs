using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Entities.Finance;

/// <summary>
/// Payment provider configuration scoped per building (or global if BuildingId is null).
/// Secrets (API keys, terminals) should be stored in Azure Key Vault.
/// Fields here store Key Vault secret NAMES / references, NOT actual secrets.
/// </summary>
public class PaymentProviderConfig : BaseEntity
{
    /// <summary>Null = global default config</summary>
    public int? BuildingId { get; set; }
    public Building? Building { get; set; }

    public PaymentProviderType ProviderType { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Key Vault secret name for MerchantId / Terminal / API Key</summary>
    [MaxLength(200)]
    public string? MerchantIdRef { get; set; }

    /// <summary>Key Vault secret name for Terminal ID</summary>
    [MaxLength(200)]
    public string? TerminalIdRef { get; set; }

    /// <summary>Key Vault secret name for API User / Username</summary>
    [MaxLength(200)]
    public string? ApiUserRef { get; set; }

    /// <summary>Key Vault secret name for API Password / Secret</summary>
    [MaxLength(200)]
    public string? ApiPasswordRef { get; set; }

    /// <summary>Key Vault secret name for Webhook signing secret</summary>
    [MaxLength(200)]
    public string? WebhookSecretRef { get; set; }

    public ProviderFeatures SupportedFeatures { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "ILS";

    /// <summary>Provider-specific base URL override (for sandbox/production switching)</summary>
    [MaxLength(500)]
    public string? BaseUrl { get; set; }
}
