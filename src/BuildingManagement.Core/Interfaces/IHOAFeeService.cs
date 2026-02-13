namespace BuildingManagement.Core.Interfaces;

public record GenerateChargesResult(bool AlreadyRan, string Period, int ChargesCreated, string Message);

public interface IHOAFeeService
{
    /// <summary>Generate monthly charges for all units in a building. Idempotent per building+period.</summary>
    Task<GenerateChargesResult> GenerateMonthlyChargesAsync(int buildingId, string period, CancellationToken ct = default);
}
