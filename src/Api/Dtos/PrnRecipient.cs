using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record PrnRecipient
{
    [Required]
    [JsonPropertyName("organisationId")]
    public required Guid OrganisationId { get; init; }

    [Required]
    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [Description(
        "Recipient legal name when supplied by a richer source. PRN common backend and legacy PRN records return null because they retain only a selected display name."
    )]
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [Description(
        "Recipient trading name when supplied by a richer source. PRN common backend and legacy PRN records return null because they retain only a selected display name."
    )]
    [JsonPropertyName("tradingName")]
    public string? TradingName { get; init; }

    [Description(
        "Recipient registration type when supplied by epr-backend. PRN common backend and legacy PRN records return null because they do not retain this value."
    )]
    [JsonPropertyName("registrationType")]
    public RegistrationType? RegistrationType { get; init; }
}
