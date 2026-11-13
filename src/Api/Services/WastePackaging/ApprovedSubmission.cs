using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services.WastePackaging;

public record ApprovedSubmission
{
    public required Guid SubmitterOrganisationId { get; init; }
    public required ObligationSubmitterType SubmitterType { get; init; }
    public required IReadOnlyList<ProducerPackaging> Producers { get; init; }
}
