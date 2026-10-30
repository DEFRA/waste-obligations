using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record UpdatePrnRequest
{
    [Required]
    [JsonPropertyName("status")]
    public UpdatePrnStatus? Status { get; init; }

    [Required]
    [JsonPropertyName("user")]
    public required User User { get; init; }

    /// <summary>
    /// The obligation year to accept the PRN into. Only applies when accepting, and only
    /// needed for December waste PRNs where the user chooses between two years.
    /// </summary>
    [JsonPropertyName("obligationYear")]
    [Range(Dtos.ObligationYear.Minimum, Dtos.ObligationYear.Maximum)]
    public int? ObligationYear { get; init; }
}
