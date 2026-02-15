using BuildingManagement.Core.DTOs;
using BuildingManagement.Core.Entities;
using BuildingManagement.Core.Enums;
using BuildingManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BuildingManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<AuthController> _logger;

    // Simple in-memory refresh token store (for production, use DB)
    private static readonly Dictionary<string, string> _refreshTokens = new();

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JwtTokenService jwtTokenService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return Unauthorized(new { message = "Invalid email or password." });

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid email or password." });

        var (accessToken, refreshToken, expiresIn) = await _jwtTokenService.GenerateTokensAsync(user);
        var roles = await _userManager.GetRolesAsync(user);

        _refreshTokens[refreshToken] = user.Id;

        _logger.LogInformation("User {Email} logged in successfully", request.Email);

        return Ok(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiresIn,
            Roles = roles.ToList(),
            FullName = user.FullName,
            Email = user.Email ?? "",
            UserId = user.Id
        });
    }

    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterManagerRequest request)
    {
        // Check if email already exists
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
            return BadRequest(new { message = "Email is already registered." });

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            FullName = request.FullName,
            Phone = request.Phone
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return BadRequest(new { message = errors });
        }

        // Assign Manager role
        await _userManager.AddToRoleAsync(user, AppRoles.Manager);

        // Auto-login after registration
        var (accessToken, refreshToken, expiresIn) = await _jwtTokenService.GenerateTokensAsync(user);
        var roles = await _userManager.GetRolesAsync(user);
        _refreshTokens[refreshToken] = user.Id;

        _logger.LogInformation("New manager registered: {Email}", request.Email);

        return Ok(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiresIn,
            Roles = roles.ToList(),
            FullName = user.FullName,
            Email = user.Email ?? "",
            UserId = user.Id
        });
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (!_refreshTokens.TryGetValue(request.RefreshToken, out var userId))
            return Unauthorized(new { message = "Invalid refresh token." });

        _refreshTokens.Remove(request.RefreshToken);

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Unauthorized(new { message = "User not found." });

        var (accessToken, refreshToken, expiresIn) = await _jwtTokenService.GenerateTokensAsync(user);
        var roles = await _userManager.GetRolesAsync(user);

        _refreshTokens[refreshToken] = user.Id;

        return Ok(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiresIn,
            Roles = roles.ToList(),
            FullName = user.FullName,
            Email = user.Email ?? "",
            UserId = user.Id
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout([FromBody] LogoutRequest? request)
    {
        if (request?.RefreshToken != null)
            _refreshTokens.Remove(request.RefreshToken);

        return Ok(new { message = "Logged out." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FullName,
            user.Phone,
            user.VendorId,
            user.PreferredLanguage,
            Roles = roles.ToList()
        });
    }

    [HttpPut("me/language")]
    [Authorize]
    public async Task<IActionResult> SetLanguage([FromBody] SetLanguageRequest request)
    {
        if (request.Language != "he" && request.Language != "en")
            return BadRequest(new { message = "Supported languages: he, en" });

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        user.PreferredLanguage = request.Language;
        await _userManager.UpdateAsync(user);

        return Ok(new { language = user.PreferredLanguage });
    }
}

public record SetLanguageRequest(string Language);
