using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public interface IObligationCalculationInstructionService
{
    Task<ObligationCalculationInstruction> Create(
        int obligationYear,
        Guid? submitterId,
        CancellationToken cancellationToken
    );
}
