namespace Defra.WasteObligations.Api.Data.Entities;

public record OrganisationObligationRequestPacingRead
{
    public required double DurationMilliseconds { get; init; }

    public required bool Succeeded { get; init; }
}
