using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public interface IObligationCalculationService
{
    Task<ObligationCalculation> Save(ObligationCalculation calculation, CancellationToken cancellationToken);
}
