namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public interface IObligationCalculationInstructionProcessor
{
    Task<bool> ProcessNext(CancellationToken cancellationToken);
}
