using Defra.WasteObligations.Api.Services.ObligationCalculations;

namespace Defra.WasteObligations.Api.Services.WastePackaging;

public record PackagingResponse
{
    public required IReadOnlyList<ApprovedSubmission> Items { get; init; }
}
