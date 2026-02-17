using System.ComponentModel.DataAnnotations;

namespace BuildingManagement.Core.DTOs;

public record LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public record LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public int ExpiresIn { get; init; }
    public List<string> Roles { get; init; } = [];
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string? Phone { get; init; }
}

public record RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}

public record LogoutRequest
{
    public string? RefreshToken { get; init; }
}

public record RegisterManagerRequest
{
    [Required, MaxLength(200)]
    public string FullName { get; init; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; init; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; init; }
}

public record RegisterTenantRequest
{
    [Required, MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; init; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; init; }

    /// <summary>Marketing consent – happy to stay in touch.</summary>
    public bool MarketingConsent { get; init; }

    /// <summary>Must be true – accepted terms of use.</summary>
    [Required]
    public bool TermsAccepted { get; init; }

    // ── Address & unit info (screen 2) ──

    /// <summary>Building ID selected from autocomplete.</summary>
    [Required]
    public int BuildingId { get; init; }

    /// <summary>Floor number.</summary>
    public int? Floor { get; init; }

    /// <summary>Apartment / unit number.</summary>
    [Required, MaxLength(20)]
    public string ApartmentNumber { get; init; } = string.Empty;

    /// <summary>Owner / Landlord / Renter.</summary>
    public int PropertyRole { get; init; } // maps to PropertyRole enum

    /// <summary>Also a house committee member.</summary>
    public bool IsCommitteeMember { get; init; }
}

public record BuildingSearchResult
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? AddressLine { get; init; }
    public string? City { get; init; }
}
