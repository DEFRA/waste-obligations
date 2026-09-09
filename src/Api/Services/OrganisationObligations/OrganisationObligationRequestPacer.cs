using Defra.WasteObligations.Api.Data;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public class OrganisationObligationRequestPacer(
    IOrganisationObligationRequestPacingStateStore pacingStateStore,
    IOptions<OrganisationObligationHydrationOptions> options,
    TimeProvider timeProvider
) : IOrganisationObligationRequestPacer
{
    public async Task ObserveWorkload(int activeSummaryCount, CancellationToken cancellationToken)
    {
        if (activeSummaryCount == 0)
        {
            var state = await pacingStateStore.Get(cancellationToken);
            if (state is null || (state.DesiredRequestsPerMinute == 0 && state.EffectiveRequestsPerMinute == 0))
            {
                return;
            }
        }

        await pacingStateStore.Update(
            state =>
                OrganisationObligationRequestPacingController.ObserveWorkload(state, activeSummaryCount, options.Value),
            cancellationToken
        );
    }

    public async Task ObserveRead(TimeSpan duration, bool succeeded, CancellationToken cancellationToken)
    {
        await pacingStateStore.Update(
            state =>
                OrganisationObligationRequestPacingController.ObserveRead(state, duration, succeeded, options.Value),
            cancellationToken
        );
    }

    public async Task<OrganisationObligationRequestPacingStatus> GetStatus(CancellationToken cancellationToken)
    {
        var state = await pacingStateStore.Get(cancellationToken);

        return OrganisationObligationRequestPacingController.Status(state);
    }

    public async Task Wait(CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
        var requestAt = utcNow;
        await pacingStateStore.Update(
            state =>
            {
                var reservation = OrganisationObligationRequestPacingController.ReserveNextRequest(state, utcNow);
                requestAt = reservation.RequestAt;

                return reservation.State;
            },
            cancellationToken
        );

        var delay = requestAt - timeProvider.GetUtcNowWithoutMicroseconds();
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, timeProvider, cancellationToken);
        }
    }
}
