using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services;

public record OrganisationEligibilityPollingSummary
{
    public required int VisibleRowCount { get; init; }
    public required int HydrationEligibleOrganisationCount { get; init; }
    public required IReadOnlyDictionary<int, int> MaterialisedObligationYearRowCounts { get; init; }
    public required IReadOnlyDictionary<
        OrganisationReferenceNumberResolutionState,
        int
    > ReferenceResolutionStateCounts { get; init; }
}
