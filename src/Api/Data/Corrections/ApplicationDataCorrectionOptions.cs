using System.ComponentModel.DataAnnotations;

namespace Defra.WasteObligations.Api.Data.Corrections;

public record ApplicationDataCorrectionOptions
{
    public const string SectionName = "ApplicationDataCorrections";

    [Range(10, 3600)]
    public int LeaseDurationSeconds { get; init; } = 60;

    [Range(1, 1800)]
    public int LeaseRenewalIntervalSeconds { get; init; } = 15;

    [Range(1, 60)]
    public int LeaseOperationTimeoutSeconds { get; init; } = 10;

    [Range(1, 3600)]
    public int OperationTimeoutSeconds { get; init; } = 30;

    [Range(1, 3600)]
    public int RetryDelaySeconds { get; init; } = 30;

    [Range(1, 1000)]
    public int BatchSize { get; init; } = 100;
}
