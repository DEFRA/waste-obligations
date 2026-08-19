namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public sealed class ReadObligationsLatency(long startingTimestamp)
{
    public static object HttpContextItemKey { get; } = new();

    public long StartingTimestamp { get; } = startingTimestamp;

    public double WasteOrganisationsDurationMilliseconds { get; set; }

    public double PrnTokenDurationMilliseconds { get; set; }

    public double PrnObligationCalculationDurationMilliseconds { get; set; }

    public double PrnCommonBackendDurationMilliseconds { get; set; }

    public double ParallelDownstreamDurationMilliseconds { get; set; }

    public double ResponseMappingDurationMilliseconds { get; set; }
}
