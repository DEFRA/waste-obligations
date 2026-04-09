using System.ComponentModel.DataAnnotations;

namespace Defra.WasteObligations.Api.Utils.OAuth2;

public class OAuth2Options
{
    [Required]
    public required string TokenEndpoint { get; init; }

    [Required]
    public required string ClientId { get; init; }

    [Required]
    public required string ClientSecret { get; init; }

    public string? Scope { get; init; }
}
