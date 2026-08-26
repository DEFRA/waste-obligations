using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedComplianceDeclarationsPaged
{
    [JsonPropertyName("unsubmittedComplianceDeclarations")]
    public IEnumerable<UnsubmittedComplianceDeclaration> UnsubmittedComplianceDeclarations { get; init; } = [];

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; }

    [JsonPropertyName("eligibilityAsOf")]
    public DateTimeOffset EligibilityAsOf { get; init; }
}

public record UnsubmittedComplianceDeclaration
{
    [JsonPropertyName("organisationId")]
    public Guid OrganisationId { get; init; }

    [JsonPropertyName("registrationType")]
    public RegistrationType RegistrationType { get; init; }

    [JsonPropertyName("organisationName")]
    public required string OrganisationName { get; init; }

    [JsonPropertyName("organisationReferenceNumber")]
    public required string OrganisationReferenceNumber { get; init; }

    [JsonPropertyName("recyclingObligationsMet")]
    public bool? RecyclingObligationsMet { get; init; }

    [JsonPropertyName("obligationCoveragePercentage")]
    public decimal ObligationCoveragePercentage { get; init; }

    [JsonPropertyName("obligationDataState")]
    public required string ObligationDataState { get; init; }

    [JsonPropertyName("obligationsAsOf")]
    public DateTimeOffset? ObligationsAsOf { get; init; }
}
