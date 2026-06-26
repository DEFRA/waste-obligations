using System.ComponentModel.DataAnnotations;

namespace Defra.WasteObligations.AuditEvents.Analytics;

public record AnalyticsAuditEventProcessorOptions
{
    public const string SectionName = "AnalyticsAuditEventProcessor";

    [Required]
    public required string ProcessName { get; init; }

    [Range(1, 500)]
    public int BatchSize { get; init; } = 25;

    [Range(1, 300)]
    public int PollIntervalSeconds { get; init; } = 15;

    [Range(0, 300)]
    public int PollJitterSeconds { get; init; } = 5;

    [Range(5, 3600)]
    public int LeaseDurationSeconds { get; init; } = 60;
}
