using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public static class Mappers
{
    public static Dtos.Obligation ToDto(this Obligation obligation) =>
        new()
        {
            Material = obligation.MaterialName,
            RecyclingTarget = obligation.MaterialTarget,
            Tonnages = new ObligationTonnages
            {
                Material = obligation.Tonnage,
                AwaitingAcceptance = obligation.TonnageAwaitingAcceptance,
                Accepted = obligation.TonnageAccepted,
                Outstanding = obligation.TonnageOutstanding.GetValueOrDefault(),
                Obligated = obligation.ObligationToMeet.GetValueOrDefault(),
            },
            Status = obligation.Status,
        };
}
