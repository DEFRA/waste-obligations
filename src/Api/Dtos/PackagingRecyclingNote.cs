using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Defra.WasteObligations.Api.Dtos.Attributes;

namespace Defra.WasteObligations.Api.Dtos;

// Prn.cs is an unsafe filename on Windows; therefore, filename does not match type.
public record Prn
{
    [Required]
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [Required]
    [JsonPropertyName("number")]
    public required string Number { get; init; }

    [Required]
    [PossibleValue(PrnType.Prn)]
    [PossibleValue(PrnType.Pern)]
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [Required]
    [PossibleValue(PrnStatus.AwaitingAcceptance)]
    [PossibleValue(PrnStatus.Accepted)]
    [PossibleValue(PrnStatus.Rejected)]
    [PossibleValue(PrnStatus.AwaitingCancellation)]
    [PossibleValue(PrnStatus.Cancelled)]
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [Description(
        "Time the PRN or PERN was issued, returned at UTC offset zero. For RREPW and epr-backend records this is also the authorisation time."
    )]
    [Required]
    [JsonPropertyName("issuedAt")]
    public required DateTimeOffset IssuedAt { get; init; }

    [Required]
    [JsonPropertyName("obligationYear")]
    public required int ObligationYear { get; init; }

    [Required]
    [JsonPropertyName("accreditationYear")]
    public required int AccreditationYear { get; init; }

    [Required]
    [JsonPropertyName("decemberWaste")]
    public required bool DecemberWaste { get; init; }

    [Required]
    [PossibleValue(Dtos.Material.Plastic)]
    [PossibleValue(Dtos.Material.Glass)]
    [PossibleValue(Dtos.Material.Aluminium)]
    [PossibleValue(Dtos.Material.Steel)]
    [PossibleValue(Dtos.Material.Wood)]
    [PossibleValue(Dtos.Material.GlassRemelt)]
    [PossibleValue(Dtos.Material.Paper)]
    [PossibleValue(Dtos.Material.Fibre)]
    [JsonPropertyName("material")]
    public required string Material { get; init; }

    [Required]
    [JsonPropertyName("recyclingProcess")]
    public required string RecyclingProcess { get; init; }

    [Required]
    [JsonPropertyName("tonnage")]
    public required int Tonnage { get; init; }

    [Required]
    [JsonPropertyName("issuer")]
    public required PrnIssuer Issuer { get; init; }

    [Required]
    [JsonPropertyName("recipient")]
    public required PrnRecipient Recipient { get; init; }

    [Required]
    [JsonPropertyName("authorisedBy")]
    public required PrnAuthorisedBy AuthorisedBy { get; init; }

    [Required]
    [JsonPropertyName("accreditationNumber")]
    public required string AccreditationNumber { get; init; }

    [JsonPropertyName("reprocessingSite")]
    public string? ReprocessingSite { get; init; }

    [Required]
    [JsonPropertyName("reprocessorExporterAgency")]
    public required string ReprocessorExporterAgency { get; init; }

    [JsonPropertyName("additionalNotes")]
    public string? AdditionalNotes { get; init; }

    [Required]
    [JsonPropertyName("audit")]
    public required PrnAudit Audit { get; init; }
}
