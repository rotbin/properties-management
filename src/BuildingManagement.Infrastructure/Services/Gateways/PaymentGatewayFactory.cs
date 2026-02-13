using BuildingManagement.Core.Entities.Finance;
using BuildingManagement.Core.Enums;
using BuildingManagement.Core.Interfaces;
using BuildingManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Services.Gateways;

public class PaymentGatewayFactory : IPaymentGatewayFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PaymentGatewayFactory> _logger;

    public PaymentGatewayFactory(IServiceProvider serviceProvider, ILogger<PaymentGatewayFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<IPaymentGateway> GetGatewayAsync(int? buildingId = null, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        PaymentProviderConfig? config = null;

        // 1. Try building-specific config
        if (buildingId.HasValue)
        {
            config = await db.Set<PaymentProviderConfig>()
                .Where(c => c.BuildingId == buildingId && c.IsActive && !c.IsDeleted)
                .FirstOrDefaultAsync(ct);
        }

        // 2. Fall back to global config (BuildingId = null)
        config ??= await db.Set<PaymentProviderConfig>()
            .Where(c => c.BuildingId == null && c.IsActive && !c.IsDeleted)
            .FirstOrDefaultAsync(ct);

        var providerType = config?.ProviderType ?? PaymentProviderType.Fake;

        _logger.LogDebug("Resolved payment provider {Provider} for building {BuildingId}", providerType, buildingId);
        return GetGateway(providerType);
    }

    public IPaymentGateway GetGateway(PaymentProviderType providerType)
    {
        return providerType switch
        {
            PaymentProviderType.Fake => _serviceProvider.GetRequiredService<FakePaymentGateway>(),
            PaymentProviderType.Meshulam => _serviceProvider.GetRequiredService<MeshulamGateway>(),
            PaymentProviderType.Pelecard => _serviceProvider.GetRequiredService<PelecardGateway>(),
            PaymentProviderType.Tranzila => _serviceProvider.GetRequiredService<TranzilaGateway>(),
            _ => throw new ArgumentException($"Unknown payment provider: {providerType}")
        };
    }
}
