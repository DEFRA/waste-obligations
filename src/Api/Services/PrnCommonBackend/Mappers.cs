namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public static class Mappers
{
    public static Dtos.Obligation ToDto(this Obligation obligation) =>
        new()
        {
            MaterialName = obligation.MaterialName,
            RecyclingTarget = obligation.MaterialTarget,
            Tonnage = obligation.Tonnage,
            ObligatedTonnage = obligation.ObligationToMeet,
            TonnageAwaitingAcceptance = obligation.TonnageAwaitingAcceptance,
            AcceptedTonnage = obligation.TonnageAccepted,
            OutstandingTonnage = obligation.TonnageOutstanding,
            Status = obligation.Status,
        };
}
