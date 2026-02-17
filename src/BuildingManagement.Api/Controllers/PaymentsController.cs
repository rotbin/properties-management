using System.Security.Cryptography;
using System.Text;
using BuildingManagement.Core.DTOs;
using BuildingManagement.Core.Entities.Finance;
using BuildingManagement.Core.Enums;
using BuildingManagement.Core.Interfaces;
using BuildingManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BuildingManagement.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly IEmailService _emailService;
    private readonly IAccountingDocProvider _docProvider;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(AppDbContext db, IPaymentGatewayFactory gatewayFactory, IEmailService emailService, IAccountingDocProvider docProvider, ILogger<PaymentsController> logger)
    {
        _db = db;
        _gatewayFactory = gatewayFactory;
        _emailService = emailService;
        _docProvider = docProvider;
        _logger = logger;
    }

    // ─── Hosted Payment Session (Pay Now) ───────────────

    [HttpPost("session/{unitChargeId}")]
    [Authorize(Roles = $"{AppRoles.Tenant},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<CreatePaymentSessionResponse>> CreatePaymentSession(int unitChargeId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

        var charge = await _db.UnitCharges
            .Include(uc => uc.Unit)
            .Include(uc => uc.Allocations)
            .FirstOrDefaultAsync(uc => uc.Id == unitChargeId);
        if (charge == null) return NotFound();

        if (User.IsInRole(AppRoles.Tenant) && charge.Unit.TenantUserId != userId)
            return Forbid();

        var totalPaid = charge.Allocations.Sum(a => a.AllocatedAmount);
        var remaining = charge.AmountDue - totalPaid;
        if (remaining <= 0) return BadRequest(new { message = "Charge is already fully paid." });

        var gateway = await _gatewayFactory.GetGatewayAsync(charge.Unit.BuildingId);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var frontendBase = Request.Headers["Origin"].FirstOrDefault() ?? "http://localhost:5173";

        // Create pending Payment record first
        var payment = new Payment
        {
            UnitId = charge.UnitId,
            UserId = userId,
            Amount = remaining,
            Status = PaymentStatus.Pending
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        var result = await gateway.CreatePaymentSessionAsync(new CreatePaymentSessionRequest(
            BuildingId: charge.Unit.BuildingId,
            UnitChargeId: unitChargeId,
            UserId: userId,
            UserEmail: User.FindFirst(ClaimTypes.Email)?.Value ?? "",
            UserName: User.FindFirst(ClaimTypes.Name)?.Value ?? "",
            Amount: remaining,
            Currency: "ILS",
            Description: $"HOA Payment - Unit {charge.Unit.UnitNumber} - {charge.Period}",
            SuccessUrl: $"{frontendBase}/payment/success",
            CancelUrl: $"{frontendBase}/payment/cancel",
            WebhookUrl: $"{baseUrl}/api/payments/webhook/{gateway.ProviderType}",
            IdempotencyKey: $"pay-{unitChargeId}-{payment.Id}"));

        if (result.Success)
        {
            payment.ProviderReference = result.ProviderReference;

            // For Fake gateway: auto-confirm the payment immediately so the
            // full flow works in development without needing a real webhook callback.
            if (gateway.ProviderType == PaymentProviderType.Fake)
            {
                payment.Status = PaymentStatus.Succeeded;
                // Reload charge with allocations to get accurate totals
                charge = await _db.UnitCharges
                    .Include(uc => uc.Unit).ThenInclude(u => u.TenantUser)
                    .Include(uc => uc.Allocations)
                    .FirstAsync(uc => uc.Id == unitChargeId);
                await AllocatePaymentAsync(payment, charge, remaining);
                _logger.LogInformation("FAKE: Auto-confirmed payment {PaymentId} for charge {ChargeId}, amount {Amount} ILS",
                    payment.Id, unitChargeId, remaining);

                // Issue receipt in background (best-effort, non-blocking)
                _ = Task.Run(async () => { try { await IssueReceiptSafe(payment.Id); } catch { /* logged inside */ } });
            }

            await _db.SaveChangesAsync();
        }

        return Ok(new CreatePaymentSessionResponse
        {
            PaymentUrl = result.PaymentUrl,
            SessionId = result.SessionId,
            PaymentId = payment.Id,
            Error = result.Error
        });
    }

    // ─── Tokenization (Add Card — Hosted Flow) ─────────

    [HttpPost("tokenize")]
    [Authorize(Roles = $"{AppRoles.Tenant},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<StartTokenizationResponse>> StartTokenization([FromBody] StartTokenizationRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var gateway = await _gatewayFactory.GetGatewayAsync(request.BuildingId);
        var frontendBase = Request.Headers["Origin"].FirstOrDefault() ?? "http://localhost:5173";
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var result = await gateway.TokenizePaymentMethodAsync(new TokenizeRequest(
            BuildingId: request.BuildingId,
            UserId: userId,
            UserEmail: User.FindFirst(ClaimTypes.Email)?.Value ?? "",
            UserName: User.FindFirst(ClaimTypes.Name)?.Value ?? "",
            SuccessUrl: $"{frontendBase}/payment/success?type=tokenize",
            CancelUrl: $"{frontendBase}/payment/cancel?type=tokenize",
            WebhookUrl: $"{baseUrl}/api/payments/webhook/{gateway.ProviderType}"));

        if (result.Success && result.Token != null)
        {
            // Gateway returned a token directly (e.g. Fake gateway) — save card now,
            // no redirect needed.
            if (request.IsDefault)
            {
                var existing = await _db.PaymentMethods.Where(pm => pm.UserId == userId && pm.IsDefault).ToListAsync();
                foreach (var pm in existing) pm.IsDefault = false;
            }

            _db.PaymentMethods.Add(new PaymentMethod
            {
                UserId = userId,
                MethodType = PaymentMethodType.CreditCard,
                Provider = gateway.ProviderName,
                Token = result.Token,
                Last4Digits = result.Last4,
                Expiry = result.Expiry,
                CardBrand = result.CardBrand,
                ProviderCustomerId = result.ProviderCustomerId,
                IsDefault = request.IsDefault,
                IsActive = true
            });
            await _db.SaveChangesAsync();

            // Card saved directly — return success with no redirect
            return Ok(new StartTokenizationResponse
            {
                RedirectUrl = null,
                Error = null
            });
        }

        return Ok(new StartTokenizationResponse
        {
            RedirectUrl = result.RedirectUrl,
            Error = result.Error
        });
    }

    // ─── Legacy setup-method (Fake gateway only — direct) ───

    [HttpPost("setup-method")]
    [Authorize(Roles = $"{AppRoles.Tenant},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<PaymentMethodDto>> SetupMethod([FromBody] SetupPaymentMethodRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

        // Only Fake gateway supports direct server-side tokenization
        var gateway = await _gatewayFactory.GetGatewayAsync();
        if (gateway.ProviderType != PaymentProviderType.Fake && string.IsNullOrEmpty(request.CardNumber))
            return BadRequest(new { message = "For real providers, use the hosted tokenization flow (POST /api/payments/tokenize)." });

        var tokenResult = await gateway.TokenizePaymentMethodAsync(new TokenizeRequest(
            BuildingId: 0, UserId: userId,
            UserEmail: User.FindFirst(ClaimTypes.Email)?.Value ?? "",
            UserName: User.FindFirst(ClaimTypes.Name)?.Value ?? "",
            SuccessUrl: "", CancelUrl: "", WebhookUrl: ""));

        if (!tokenResult.Success || tokenResult.Token == null)
            return BadRequest(new { message = $"Failed to tokenize: {tokenResult.Error}" });

        if (request.IsDefault)
        {
            var existing = await _db.PaymentMethods.Where(pm => pm.UserId == userId && pm.IsDefault).ToListAsync();
            foreach (var pm in existing) pm.IsDefault = false;
        }

        var method = new PaymentMethod
        {
            UserId = userId,
            MethodType = request.MethodType,
            Provider = request.Provider ?? gateway.ProviderName,
            Token = tokenResult.Token,
            Last4Digits = tokenResult.Last4,
            Expiry = tokenResult.Expiry,
            CardBrand = tokenResult.CardBrand,
            ProviderCustomerId = tokenResult.ProviderCustomerId,
            IsDefault = request.IsDefault,
            IsActive = true
        };

        _db.PaymentMethods.Add(method);
        await _db.SaveChangesAsync();

        return Ok(MapMethodDto(method));
    }

    // ─── Payment Methods ────────────────────────────────

    [HttpGet("methods")]
    [Authorize(Roles = $"{AppRoles.Tenant},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<List<PaymentMethodDto>>> GetMethods()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var methods = await _db.PaymentMethods.Where(pm => pm.UserId == userId && pm.IsActive).ToListAsync();
        return Ok(methods.Select(MapMethodDto).ToList());
    }

    [HttpPut("methods/{id}/default")]
    [Authorize(Roles = $"{AppRoles.Tenant},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> SetDefaultMethod(int id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var methods = await _db.PaymentMethods.Where(pm => pm.UserId == userId && pm.IsActive).ToListAsync();
        foreach (var m in methods) m.IsDefault = m.Id == id;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("methods/{id}")]
    [Authorize(Roles = $"{AppRoles.Tenant},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> DeleteMethod(int id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var method = await _db.PaymentMethods.FirstOrDefaultAsync(pm => pm.Id == id && pm.UserId == userId);
        if (method == null) return NotFound();
        method.IsActive = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── Pay via saved token (direct charge) ────────────

    [HttpPost("pay/{unitChargeId}")]
    [Authorize(Roles = $"{AppRoles.Tenant},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<PaymentDto>> PayCharge(int unitChargeId, [FromBody] PayChargeRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

        var charge = await _db.UnitCharges
            .Include(uc => uc.Unit)
            .Include(uc => uc.Allocations)
            .FirstOrDefaultAsync(uc => uc.Id == unitChargeId);
        if (charge == null) return NotFound();

        if (User.IsInRole(AppRoles.Tenant) && charge.Unit.TenantUserId != userId) return Forbid();

        var totalPaid = charge.Allocations.Sum(a => a.AllocatedAmount);
        var remaining = charge.AmountDue - totalPaid;
        if (remaining <= 0) return BadRequest(new { message = "This charge is already fully paid." });

        var payAmount = request.Amount.HasValue && request.Amount.Value < remaining ? request.Amount.Value : remaining;

        PaymentMethod? method = null;
        if (request.PaymentMethodId.HasValue)
            method = await _db.PaymentMethods.FirstOrDefaultAsync(pm => pm.Id == request.PaymentMethodId && pm.UserId == userId && pm.IsActive);
        else
            method = await _db.PaymentMethods.FirstOrDefaultAsync(pm => pm.UserId == userId && pm.IsDefault && pm.IsActive);

        if (method == null)
            return BadRequest(new { message = "No payment method found. Add a payment method first." });

        var gateway = await _gatewayFactory.GetGatewayAsync(charge.Unit.BuildingId);
        var chargeResult = await gateway.ChargeTokenAsync(new ChargeTokenRequest(
            BuildingId: charge.Unit.BuildingId,
            Token: method.Token,
            Amount: payAmount,
            Currency: "ILS",
            Description: $"HOA - Unit {charge.Unit.UnitNumber} - {charge.Period}",
            IdempotencyKey: $"charge-{unitChargeId}-{DateTime.UtcNow:yyyyMMdd}"));

        var payment = new Payment
        {
            UnitId = charge.UnitId,
            UserId = userId,
            Amount = payAmount,
            PaymentMethodId = method.Id,
            ProviderReference = chargeResult.ProviderReference,
            Status = chargeResult.Success ? PaymentStatus.Succeeded : PaymentStatus.Failed
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        if (chargeResult.Success)
            await AllocatePaymentAsync(payment, charge, payAmount);

        return Ok(MapPaymentDto(payment, charge.Unit.UnitNumber, method.Last4Digits));
    }

    // ─── Payment History ────────────────────────────────

    [HttpGet("unit/{unitId}")]
    public async Task<ActionResult<List<PaymentDto>>> GetPaymentsForUnit(int unitId)
    {
        if (User.IsInRole(AppRoles.Tenant))
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            var unit = await _db.Units.FindAsync(unitId);
            if (unit?.TenantUserId != userId) return Forbid();
        }

        var payments = await _db.Payments.Include(p => p.Unit).Include(p => p.User).Include(p => p.PaymentMethod)
            .Where(p => p.UnitId == unitId).OrderByDescending(p => p.PaymentDateUtc)
            .Select(p => new PaymentDto
            {
                Id = p.Id, UnitId = p.UnitId, UnitNumber = p.Unit.UnitNumber, UserId = p.UserId,
                UserName = p.User.FullName, Amount = p.Amount, PaymentDateUtc = p.PaymentDateUtc,
                PaymentMethodId = p.PaymentMethodId, Last4 = p.PaymentMethod != null ? p.PaymentMethod.Last4Digits : null,
                ProviderReference = p.ProviderReference, Status = p.Status, CreatedAtUtc = p.CreatedAtUtc
            }).ToListAsync();
        return Ok(payments);
    }

    [HttpGet("my")]
    [Authorize(Roles = AppRoles.Tenant)]
    public async Task<ActionResult<List<PaymentDto>>> GetMyPayments()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var payments = await _db.Payments.Include(p => p.Unit).Include(p => p.PaymentMethod)
            .Where(p => p.UserId == userId).OrderByDescending(p => p.PaymentDateUtc)
            .Select(p => new PaymentDto
            {
                Id = p.Id, UnitId = p.UnitId, UnitNumber = p.Unit.UnitNumber, UserId = p.UserId,
                Amount = p.Amount, PaymentDateUtc = p.PaymentDateUtc, PaymentMethodId = p.PaymentMethodId,
                Last4 = p.PaymentMethod != null ? p.PaymentMethod.Last4Digits : null,
                ProviderReference = p.ProviderReference, Status = p.Status, CreatedAtUtc = p.CreatedAtUtc
            }).ToListAsync();
        return Ok(payments);
    }

    // ─── Webhook (per-provider routing) ─────────────────

    [HttpPost("webhook/{providerType}")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(string providerType)
    {
        if (!Enum.TryParse<PaymentProviderType>(providerType, true, out var pt))
            return BadRequest(new { error = "Unknown provider type" });

        var gateway = _gatewayFactory.GetGateway(pt);
        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());

        // Rate limiting note: in production add middleware rate limiting to this endpoint
        // Read body once into a MemoryStream so we can pass it to parse and also compute hash
        Request.EnableBuffering();
        using var memStream = new MemoryStream();
        await Request.Body.CopyToAsync(memStream);
        var bodyBytes = memStream.ToArray();
        var bodyText = Encoding.UTF8.GetString(bodyBytes);

        // Compute payload hash for dedup
        var payloadHash = Convert.ToHexString(SHA256.HashData(bodyBytes)).ToLower();

        // Parse the webhook
        var parseStream = new MemoryStream(bodyBytes);
        var parsed = await gateway.ParseWebhookAsync(parseStream, headers);

        if (!parsed.Parsed)
        {
            _logger.LogWarning("Failed to parse webhook from {Provider}: {Error}", providerType, parsed.Error);
            return BadRequest(new { error = "Failed to parse webhook" });
        }

        // Idempotency: check WebhookEventLog
        var eventId = parsed.EventId ?? $"unknown_{Guid.NewGuid():N}";
        if (await _db.Set<WebhookEventLog>().AnyAsync(w => w.ProviderType == pt && w.EventId == eventId))
        {
            _logger.LogInformation("Duplicate webhook event {EventId} from {Provider}, ignoring", eventId, providerType);
            return Ok(new { received = true, duplicate = true });
        }

        // Verify signature (fail-closed for real providers in production)
        var signatureValid = await gateway.VerifyWebhookSignatureAsync(parsed, headers);
        if (!signatureValid && pt != PaymentProviderType.Fake)
        {
            _logger.LogWarning("Webhook signature verification FAILED for {Provider} event {EventId}", providerType, eventId);
            // Log it but reject
            _db.Set<WebhookEventLog>().Add(new WebhookEventLog
            {
                ProviderType = pt, EventId = eventId, PayloadHash = payloadHash,
                ProcessedAtUtc = DateTime.UtcNow, Result = "SignatureInvalid"
            });
            await _db.SaveChangesAsync();
            return Unauthorized(new { error = "Signature verification failed" });
        }

        // Process payment update
        if (parsed.ProviderReference != null && parsed.Status.HasValue)
        {
            var payment = await _db.Payments
                .Include(p => p.Allocations)
                .FirstOrDefaultAsync(p => p.ProviderReference == parsed.ProviderReference);

            if (payment != null && payment.Status == PaymentStatus.Pending)
            {
                payment.Status = parsed.Status.Value;
                if (parsed.Status == PaymentStatus.Succeeded)
                {
                    var charge = await _db.UnitCharges
                        .Include(uc => uc.Unit).Include(uc => uc.Allocations)
                        .FirstOrDefaultAsync(uc => uc.Id == payment.UnitId); // Look up by matching
                    // Allocate if not already done
                    if (charge != null && !payment.Allocations.Any())
                        await AllocatePaymentAsync(payment, charge, payment.Amount);
                }
                await _db.SaveChangesAsync();

                // Issue receipt for successful payment (idempotent, best-effort)
                if (parsed.Status == PaymentStatus.Succeeded)
                    _ = Task.Run(async () => { try { await IssueReceiptSafe(payment.Id); } catch { /* logged inside */ } });
            }
        }

        // Process tokenization callback
        if (parsed.Token != null)
        {
            _logger.LogInformation("Webhook tokenization callback: token received for customer {Cust}", parsed.ProviderCustomerId);
            // Token saved via the redirect flow; webhook is confirmation
        }

        _db.Set<WebhookEventLog>().Add(new WebhookEventLog
        {
            ProviderType = pt, EventId = eventId, PayloadHash = payloadHash,
            ProcessedAtUtc = DateTime.UtcNow, Result = "Processed"
        });
        await _db.SaveChangesAsync();

        return Ok(new { received = true });
    }

    // ─── Legacy webhook (backward compat) ───────────────

    [HttpPost("webhook")]
    [AllowAnonymous]
    public Task<IActionResult> WebhookLegacy() => Webhook("Fake");

    // ─── Standing Orders (PayPal Subscriptions) ─────────

    [HttpPost("standing-orders")]
    [Authorize(Roles = $"{AppRoles.Tenant},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<CreateStandingOrderResponse>> CreateStandingOrder([FromBody] CreateStandingOrderRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

        var unit = await _db.Units.Include(u => u.Building)
            .FirstOrDefaultAsync(u => u.Id == request.UnitId);
        if (unit == null) return NotFound(new { message = "Unit not found" });

        if (User.IsInRole(AppRoles.Tenant) && unit.TenantUserId != userId)
            return Forbid();

        // Check for existing active standing order
        var existing = await _db.StandingOrders
            .AnyAsync(so => so.UserId == userId && so.UnitId == request.UnitId
                && so.Status == StandingOrderStatus.Active);
        if (existing)
            return BadRequest(new { message = "An active standing order already exists for this unit." });

        var gateway = await _gatewayFactory.GetGatewayAsync(request.BuildingId);
        var frontendBase = Request.Headers["Origin"].FirstOrDefault() ?? "http://localhost:5173";

        var standingOrder = new StandingOrder
        {
            UserId = userId,
            UnitId = request.UnitId,
            BuildingId = request.BuildingId,
            ProviderType = gateway.ProviderType,
            Amount = request.Amount,
            Currency = request.Currency,
            Frequency = request.Frequency,
            Status = StandingOrderStatus.Active,
            StartDate = DateTime.UtcNow,
            NextChargeDate = DateTime.UtcNow.AddMonths(1)
        };

        // For PayPal: create a subscription via the PayPal API
        if (gateway is BuildingManagement.Infrastructure.Services.Gateways.PayPalGateway paypalGateway)
        {
            // In production, credentials would come from Key Vault via PaymentProviderConfig
            var config = await _db.PaymentProviderConfigs
                .FirstOrDefaultAsync(c => c.ProviderType == PaymentProviderType.PayPal
                    && (c.BuildingId == request.BuildingId || c.BuildingId == null)
                    && c.IsActive && !c.IsDeleted);

            if (config == null)
                return BadRequest(new { message = "PayPal not configured for this building." });

            // Create a billing plan
            var planResult = await paypalGateway.CreateBillingPlanAsync(
                productId: "PROD-HOA-PAYMENT",
                amount: request.Amount,
                currency: request.Currency,
                description: $"HOA - {unit.Building.Name} Unit {unit.UnitNumber}",
                clientId: config.MerchantIdRef,
                clientSecret: config.ApiPasswordRef,
                baseUrl: config.BaseUrl);

            if (!planResult.Success)
                return BadRequest(new { message = planResult.Error });

            standingOrder.ProviderPlanId = planResult.PlanId;

            // Create subscription
            var subResult = await paypalGateway.CreateSubscriptionAsync(
                planId: planResult.PlanId!,
                returnUrl: $"{frontendBase}/payment/success?type=standing-order",
                cancelUrl: $"{frontendBase}/payment/cancel?type=standing-order",
                clientId: config.MerchantIdRef,
                clientSecret: config.ApiPasswordRef,
                baseUrl: config.BaseUrl);

            if (!subResult.Success)
                return BadRequest(new { message = subResult.Error });

            standingOrder.ProviderSubscriptionId = subResult.SubscriptionId;
            standingOrder.ApprovalUrl = subResult.ApprovalUrl;
        }
        else if (gateway.ProviderType == PaymentProviderType.Fake)
        {
            // For Fake gateway: auto-activate standing order
            standingOrder.ProviderSubscriptionId = $"fake_sub_{Guid.NewGuid():N}";
            standingOrder.Status = StandingOrderStatus.Active;
            _logger.LogInformation("FAKE: Created standing order for user {UserId}, unit {UnitId}, amount {Amount}",
                userId, request.UnitId, request.Amount);
        }
        else
        {
            return BadRequest(new { message = $"Standing orders not yet supported for {gateway.ProviderName}." });
        }

        _db.StandingOrders.Add(standingOrder);
        await _db.SaveChangesAsync();

        return Ok(new CreateStandingOrderResponse
        {
            StandingOrderId = standingOrder.Id,
            ApprovalUrl = standingOrder.ApprovalUrl
        });
    }

    [HttpGet("standing-orders")]
    [Authorize(Roles = $"{AppRoles.Tenant},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<List<StandingOrderDto>>> GetStandingOrders()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var isTenant = User.IsInRole(AppRoles.Tenant);

        var query = _db.StandingOrders
            .Include(so => so.Unit).ThenInclude(u => u.Building)
            .AsQueryable();

        if (isTenant)
            query = query.Where(so => so.UserId == userId);

        var orders = await query
            .OrderByDescending(so => so.CreatedAtUtc)
            .Select(so => new StandingOrderDto
            {
                Id = so.Id,
                UserId = so.UserId,
                UnitId = so.UnitId,
                UnitNumber = so.Unit.UnitNumber,
                BuildingId = so.BuildingId,
                BuildingName = so.Unit.Building.Name,
                ProviderType = so.ProviderType.ToString(),
                ProviderSubscriptionId = so.ProviderSubscriptionId,
                Amount = so.Amount,
                Currency = so.Currency,
                Frequency = so.Frequency.ToString(),
                Status = so.Status,
                StartDate = so.StartDate,
                EndDate = so.EndDate,
                NextChargeDate = so.NextChargeDate,
                LastChargedAtUtc = so.LastChargedAtUtc,
                ApprovalUrl = so.ApprovalUrl,
                SuccessfulCharges = so.SuccessfulCharges,
                FailedCharges = so.FailedCharges,
                CreatedAtUtc = so.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(orders);
    }

    [HttpPost("standing-orders/{id}/cancel")]
    [Authorize(Roles = $"{AppRoles.Tenant},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> CancelStandingOrder(int id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

        var order = await _db.StandingOrders.FindAsync(id);
        if (order == null) return NotFound();

        if (User.IsInRole(AppRoles.Tenant) && order.UserId != userId)
            return Forbid();

        if (order.Status == StandingOrderStatus.Cancelled)
            return BadRequest(new { message = "Standing order is already cancelled." });

        // Cancel on the provider side if PayPal
        if (order.ProviderType == PaymentProviderType.PayPal && order.ProviderSubscriptionId != null)
        {
            var gateway = _gatewayFactory.GetGateway(PaymentProviderType.PayPal);
            if (gateway is BuildingManagement.Infrastructure.Services.Gateways.PayPalGateway paypalGateway)
            {
                var config = await _db.PaymentProviderConfigs
                    .FirstOrDefaultAsync(c => c.ProviderType == PaymentProviderType.PayPal
                        && (c.BuildingId == order.BuildingId || c.BuildingId == null)
                        && c.IsActive && !c.IsDeleted);

                if (config != null)
                {
                    await paypalGateway.CancelSubscriptionAsync(
                        order.ProviderSubscriptionId,
                        "Cancelled by user",
                        config.MerchantIdRef,
                        config.ApiPasswordRef,
                        config.BaseUrl);
                }
            }
        }

        order.Status = StandingOrderStatus.Cancelled;
        order.EndDate = DateTime.UtcNow;
        order.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Standing order {Id} cancelled by user {UserId}", id, userId);
        return NoContent();
    }

    [HttpPost("standing-orders/{id}/pause")]
    [Authorize(Roles = $"{AppRoles.Tenant},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> PauseStandingOrder(int id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var order = await _db.StandingOrders.FindAsync(id);
        if (order == null) return NotFound();

        if (User.IsInRole(AppRoles.Tenant) && order.UserId != userId) return Forbid();
        if (order.Status != StandingOrderStatus.Active)
            return BadRequest(new { message = "Only active standing orders can be paused." });

        order.Status = StandingOrderStatus.Paused;
        order.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("standing-orders/{id}/resume")]
    [Authorize(Roles = $"{AppRoles.Tenant},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> ResumeStandingOrder(int id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var order = await _db.StandingOrders.FindAsync(id);
        if (order == null) return NotFound();

        if (User.IsInRole(AppRoles.Tenant) && order.UserId != userId) return Forbid();
        if (order.Status != StandingOrderStatus.Paused)
            return BadRequest(new { message = "Only paused standing orders can be resumed." });

        order.Status = StandingOrderStatus.Active;
        order.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ─── Helpers ────────────────────────────────────────

    private async Task AllocatePaymentAsync(Payment payment, UnitCharge charge, decimal amount)
    {
        _db.PaymentAllocations.Add(new PaymentAllocation
        {
            PaymentId = payment.Id,
            UnitChargeId = charge.Id,
            AllocatedAmount = amount
        });

        var newTotal = charge.Allocations.Sum(a => a.AllocatedAmount) + amount;
        charge.Status = newTotal >= charge.AmountDue ? UnitChargeStatus.Paid : UnitChargeStatus.PartiallyPaid;

        var lastBalance = await _db.LedgerEntries
            .Where(le => le.UnitId == charge.UnitId)
            .OrderByDescending(le => le.Id)
            .Select(le => (decimal?)le.BalanceAfter)
            .FirstOrDefaultAsync() ?? 0m;

        _db.LedgerEntries.Add(new LedgerEntry
        {
            BuildingId = charge.Unit.BuildingId,
            UnitId = charge.UnitId,
            EntryType = LedgerEntryType.Payment,
            Category = "HOAMonthlyFees",
            Description = $"Payment for charge #{charge.Id}",
            ReferenceId = payment.Id,
            Debit = 0,
            Credit = amount,
            BalanceAfter = lastBalance - amount
        });

        await _db.SaveChangesAsync();

        var tenantEmail = charge.Unit.TenantUser?.Email;
        if (tenantEmail != null)
        {
            await _emailService.SendEmailAsync(tenantEmail,
                $"Payment Confirmation - {charge.Period}",
                $"Your payment of {amount:N2} ILS for {charge.Period} was successful. Reference: {payment.ProviderReference}");
        }
    }

    private static PaymentMethodDto MapMethodDto(PaymentMethod pm) => new()
    {
        Id = pm.Id, MethodType = pm.MethodType, Provider = pm.Provider,
        Last4Digits = pm.Last4Digits, Expiry = pm.Expiry, CardBrand = pm.CardBrand,
        IsDefault = pm.IsDefault, IsActive = pm.IsActive
    };

    private static PaymentDto MapPaymentDto(Payment p, string? unitNumber, string? last4) => new()
    {
        Id = p.Id, UnitId = p.UnitId, UnitNumber = unitNumber, UserId = p.UserId,
        Amount = p.Amount, PaymentDateUtc = p.PaymentDateUtc, PaymentMethodId = p.PaymentMethodId,
        Last4 = last4, ProviderReference = p.ProviderReference, Status = p.Status, CreatedAtUtc = p.CreatedAtUtc
    };

    /// <summary>Issue receipt for a payment (idempotent, logs errors).</summary>
    private async Task IssueReceiptSafe(int paymentId)
    {
        try
        {
            var payment = await _db.Payments
                .Include(p => p.Unit).ThenInclude(u => u.Building)
                .Include(p => p.User)
                .Include(p => p.Allocations).ThenInclude(a => a.UnitCharge)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null || payment.Status != PaymentStatus.Succeeded || payment.ReceiptDocId != null)
                return;

            var building = payment.Unit?.Building;
            if (building == null || string.IsNullOrEmpty(building.IssuerProfileId))
            {
                _logger.LogInformation("Skipping receipt for payment {PaymentId}: no issuer profile on building", paymentId);
                return;
            }

            var period = payment.Allocations.FirstOrDefault()?.UnitCharge?.Period ?? "N/A";
            var result = await _docProvider.CreateReceiptAsync(new CreateDocRequest
            {
                IssuerProfileId = building.IssuerProfileId,
                Customer = new DocCustomer { Name = payment.User?.FullName ?? "Tenant", Email = payment.User?.Email },
                Amount = payment.Amount,
                Currency = "ILS",
                Date = payment.PaymentDateUtc,
                Description = $"HOA payment – {building.Name} – {period}",
                ExternalRef = $"payment:{payment.Id}"
            });

            if (!result.Success) { _logger.LogWarning("Receipt failed for payment {PaymentId}: {Error}", paymentId, result.Error); return; }

            await _db.Payments
                .Where(p => p.Id == paymentId && p.ReceiptDocId == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.ReceiptDocId, result.DocId)
                    .SetProperty(p => p.ReceiptDocNumber, result.DocNumber)
                    .SetProperty(p => p.ReceiptPdfUrl, result.PdfUrl)
                    .SetProperty(p => p.ReceiptIssuedAtUtc, DateTime.UtcNow));

            _logger.LogInformation("Receipt issued for payment {PaymentId}: {DocNumber}", paymentId, result.DocNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IssueReceiptSafe failed for payment {PaymentId}", paymentId);
        }
    }
}
