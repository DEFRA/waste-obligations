namespace Defra.WasteObligations.Api.Data.Entities;

public record ObligationTonnages
{
    public int Material { get; init; }
    public int AwaitingAcceptance { get; init; }
    public int Accepted { get; init; }
    public int Outstanding { get; init; }
    public int Obligated { get; init; }
}
