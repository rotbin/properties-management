using BuildingManagement.Core.DTOs;
using BuildingManagement.Core.Entities.Finance;
using BuildingManagement.Core.Enums;
using BuildingManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BuildingManagement.Api.Controllers;

[ApiController]
[Route("api/payment-config")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
public class PaymentProviderConfigController : ControllerBase
{
    private readonly AppDbContext _db;

    public PaymentProviderConfigController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<PaymentProviderConfigDto>>> GetAll([FromQuery] int? buildingId)
    {
        IQueryable<PaymentProviderConfig> q = _db.Set<PaymentProviderConfig>().Include(c => c.Building);
        if (buildingId.HasValue)
            q = q.Where(c => c.BuildingId == buildingId || c.BuildingId == null);

        var configs = await q.OrderBy(c => c.BuildingId).ToListAsync();
        return Ok(configs.Select(MapDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PaymentProviderConfigDto>> GetById(int id)
    {
        var config = await _db.Set<PaymentProviderConfig>().Include(c => c.Building).FirstOrDefaultAsync(c => c.Id == id);
        if (config == null) return NotFound();
        return Ok(MapDto(config));
    }

    [HttpPost]
    public async Task<ActionResult<PaymentProviderConfigDto>> Create([FromBody] CreatePaymentProviderConfigRequest req)
    {
        if (!Enum.TryParse<PaymentProviderType>(req.ProviderType, true, out var pt))
            return BadRequest(new { message = $"Invalid provider type: {req.ProviderType}" });

        var config = new PaymentProviderConfig
        {
            BuildingId = req.BuildingId,
            ProviderType = pt,
            IsActive = true,
            MerchantIdRef = req.MerchantIdRef,
            TerminalIdRef = req.TerminalIdRef,
            ApiUserRef = req.ApiUserRef,
            ApiPasswordRef = req.ApiPasswordRef,
            WebhookSecretRef = req.WebhookSecretRef,
            SupportedFeatures = (ProviderFeatures)req.SupportedFeatures,
            Currency = req.Currency,
            BaseUrl = req.BaseUrl,
            CreatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        };

        _db.Set<PaymentProviderConfig>().Add(config);
        await _db.SaveChangesAsync();

        return Ok(MapDto(config));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreatePaymentProviderConfigRequest req)
    {
        var config = await _db.Set<PaymentProviderConfig>().FindAsync(id);
        if (config == null) return NotFound();

        if (Enum.TryParse<PaymentProviderType>(req.ProviderType, true, out var pt))
            config.ProviderType = pt;

        config.BuildingId = req.BuildingId;
        config.MerchantIdRef = req.MerchantIdRef;
        config.TerminalIdRef = req.TerminalIdRef;
        config.ApiUserRef = req.ApiUserRef;
        config.ApiPasswordRef = req.ApiPasswordRef;
        config.WebhookSecretRef = req.WebhookSecretRef;
        config.SupportedFeatures = (ProviderFeatures)req.SupportedFeatures;
        config.Currency = req.Currency;
        config.BaseUrl = req.BaseUrl;
        config.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var config = await _db.Set<PaymentProviderConfig>().FindAsync(id);
        if (config == null) return NotFound();
        config.IsDeleted = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("providers")]
    public ActionResult<string[]> GetProviderTypes()
    {
        return Ok(Enum.GetNames<PaymentProviderType>());
    }

    private static PaymentProviderConfigDto MapDto(PaymentProviderConfig c) => new()
    {
        Id = c.Id,
        BuildingId = c.BuildingId,
        BuildingName = c.Building?.Name,
        ProviderType = c.ProviderType.ToString(),
        IsActive = c.IsActive,
        MerchantIdRef = c.MerchantIdRef,
        TerminalIdRef = c.TerminalIdRef,
        ApiUserRef = c.ApiUserRef,
        ApiPasswordRef = c.ApiPasswordRef,
        WebhookSecretRef = c.WebhookSecretRef,
        SupportedFeatures = (int)c.SupportedFeatures,
        Currency = c.Currency,
        BaseUrl = c.BaseUrl
    };
}
