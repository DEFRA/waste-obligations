using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record PrnAudit
{
    [Description(
        "Time the PRN record was created in the source store, returned at UTC offset zero. Source-store timestamps are not directly comparable across PRN pools."
    )]
    [Required]
    [JsonPropertyName("createdAt")]
    public required DateTimeOffset CreatedAt { get; init; }

    [Description(
        "Time the PRN record was last updated in the source store, returned at UTC offset zero. Source-store timestamps are not directly comparable across PRN pools."
    )]
    [Required]
    [JsonPropertyName("updatedAt")]
    public required DateTimeOffset UpdatedAt { get; init; }

    [Description(
        "Time the PRN or PERN was accepted, returned at UTC offset zero. Null when no acceptance event exists or when the source cannot supply lifecycle-event timestamps."
    )]
    [JsonPropertyName("acceptedAt")]
    public DateTimeOffset? AcceptedAt { get; init; }

    [Description(
        "Time the PRN or PERN was rejected, returned at UTC offset zero. Null when no rejection event exists or when the source cannot supply lifecycle-event timestamps."
    )]
    [JsonPropertyName("rejectedAt")]
    public DateTimeOffset? RejectedAt { get; init; }

    [Description(
        "Time the PRN or PERN was cancelled, returned at UTC offset zero. Null when no cancellation event exists or when the source cannot supply lifecycle-event timestamps."
    )]
    [JsonPropertyName("cancelledAt")]
    public DateTimeOffset? CancelledAt { get; init; }
}
