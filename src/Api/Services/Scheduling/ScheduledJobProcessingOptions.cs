using System.ComponentModel.DataAnnotations;

namespace Defra.WasteObligations.Api.Services.Scheduling;

public record ScheduledJobProcessingOptions
{
    public bool Enabled { get; init; }

    [Range(1, 3600)]
    public int PollIntervalSeconds { get; init; } = 10;
}
