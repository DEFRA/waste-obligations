using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public record PrnDetails
{
    [JsonPropertyName("externalId")]
    public required Guid ExternalId { get; init; }

    [JsonPropertyName("prnNumber")]
    public required string? PrnNumber { get; init; }

    [JsonPropertyName("organisationId")]
    public required Guid OrganisationId { get; init; }

    [JsonPropertyName("organisationName")]
    public required string? OrganisationName { get; init; }

    [JsonPropertyName("reprocessorExporterAgency")]
    public required string? ReprocessorExporterAgency { get; init; }

    [JsonPropertyName("prnStatus")]
    public required string? PrnStatus { get; init; }

    [JsonPropertyName("tonnageValue")]
    public required int TonnageValue { get; init; }

    [JsonPropertyName("materialName")]
    public required string? MaterialName { get; init; }

    [JsonPropertyName("issuerNotes")]
    public string? IssuerNotes { get; init; }

    [JsonPropertyName("prnSignatory")]
    public string? PrnSignatory { get; init; }

    [JsonPropertyName("prnSignatoryPosition")]
    public string? PrnSignatoryPosition { get; init; }

    [JsonPropertyName("issueDate")]
    public required DateTime IssueDate { get; init; }

    [JsonPropertyName("processToBeUsed")]
    public required string? ProcessToBeUsed { get; init; }

    [JsonPropertyName("decemberWaste")]
    public required bool DecemberWaste { get; init; }

    [JsonPropertyName("issuedByOrg")]
    public required string? IssuedByOrg { get; init; }

    [JsonPropertyName("accreditationNumber")]
    public required string? AccreditationNumber { get; init; }

    [JsonPropertyName("reprocessingSite")]
    public string? ReprocessingSite { get; init; }

    [JsonPropertyName("accreditationYear")]
    public required string? AccreditationYear { get; init; }

    [JsonPropertyName("obligationYear")]
    public required string? ObligationYear { get; init; }

    [JsonPropertyName("createdOn")]
    public required DateTime CreatedOn { get; init; }

    [JsonPropertyName("lastUpdatedDate")]
    public required DateTime LastUpdatedDate { get; init; }

    [JsonPropertyName("isExport")]
    public required bool IsExport { get; init; }
}
