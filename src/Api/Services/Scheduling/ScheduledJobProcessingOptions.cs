using System.ComponentModel.DataAnnotations;

namespace Defra.WasteObligations.Api.Services.Scheduling;

public record ScheduledJobProcessingOptions
{
    public bool Enabled { get; init; }

    [Range(1, int.MaxValue)]
    public int MaximumAttempts { get; init; } = 3;

    [Range(1, 3600)]
    public int PollIntervalSeconds { get; init; } = 10;
}
