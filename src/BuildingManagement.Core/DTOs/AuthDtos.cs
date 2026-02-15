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
