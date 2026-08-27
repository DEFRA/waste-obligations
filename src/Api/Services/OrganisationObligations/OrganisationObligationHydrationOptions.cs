using System.ComponentModel.DataAnnotations;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public record OrganisationObligationHydrationOptions
{
    public const string SectionName = "OrganisationObligationHydration";

    [Range(1, 100)]
    public int BatchSize { get; init; } = 10;

    [Range(1, 20)]
    public int MaxConcurrentRequests { get; init; } = 5;

    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromMinutes(30);

    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromMinutes(1);

    public TimeSpan MaximumRetryDelay { get; init; } = TimeSpan.FromMinutes(30);
}
