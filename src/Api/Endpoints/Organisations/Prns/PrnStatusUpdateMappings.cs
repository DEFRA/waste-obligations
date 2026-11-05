using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;

namespace Defra.WasteObligations.Api.Endpoints.Organisations.Prns;

public static class PrnStatusUpdateMappings
{
    public static string ToPrnCommonBackendStatus(this UpdatePrnStatus? status) =>
        status switch
        {
            UpdatePrnStatus.Accepted => PrnCommonBackendStatus.Accepted,
            UpdatePrnStatus.Rejected => PrnCommonBackendStatus.Rejected,
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

    public static IResult ToResult(this PrnStatusUpdateResult result) =>
        result switch
        {
            PrnStatusUpdateResult.Updated => Results.Ok(),
            PrnStatusUpdateResult.NotFound => Results.NotFound(),
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
}
