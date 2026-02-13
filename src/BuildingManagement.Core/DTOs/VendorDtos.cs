using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.DTOs;

public record VendorDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public VendorServiceType ServiceType { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? ContactName { get; init; }
    public string? Notes { get; init; }
}

public record CreateVendorRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    public VendorServiceType ServiceType { get; init; }

    [MaxLength(20)]
    public string? Phone { get; init; }

    [MaxLength(200), EmailAddress]
    public string? Email { get; init; }

    [MaxLength(200)]
    public string? ContactName { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public record UpdateVendorRequest : CreateVendorRequest;
