namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedOrganisationRawObligation
{
    public Guid OrganisationId { get; init; }
    public required string MaterialName { get; init; }
    public int Tonnage { get; init; }
    public decimal MaterialTarget { get; init; }
    public int? ObligationToMeet { get; init; }
    public int TonnageAwaitingAcceptance { get; init; }
    public int TonnageAccepted { get; init; }
    public int? TonnageOutstanding { get; init; }
    public required string Status { get; init; }
}
