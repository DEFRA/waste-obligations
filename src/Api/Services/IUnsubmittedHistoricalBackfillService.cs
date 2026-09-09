using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Services;

public interface IUnsubmittedHistoricalBackfillService
{
    Task<UnsubmittedHistoricalBackfillStart> Start(CancellationToken cancellationToken);
}
