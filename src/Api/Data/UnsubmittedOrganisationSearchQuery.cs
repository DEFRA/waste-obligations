using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Data;

public record UnsubmittedOrganisationSearchQuery
{
    public int? ObligationYear { get; init; }
    public IReadOnlyCollection<RegistrationType>? RegistrationTypes { get; init; }
    public string? BusinessCountry { get; init; }
    public string? Search { get; init; }
    public IReadOnlyCollection<UnsubmittedOrganisationSort>? Sort { get; init; }
}
