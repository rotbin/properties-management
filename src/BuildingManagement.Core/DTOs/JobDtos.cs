namespace BuildingManagement.Core.DTOs;

public record JobRunLogDto
{
    public int Id { get; init; }
    public string JobName { get; init; } = string.Empty;
    public string PeriodKey { get; init; } = string.Empty;
    public DateTime RanAtUtc { get; init; }
}

public record GenerateJobResponse
{
    public bool AlreadyRan { get; init; }
    public string PeriodKey { get; init; } = string.Empty;
    public int WorkOrdersCreated { get; init; }
    public string Message { get; init; } = string.Empty;
}
