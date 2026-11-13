using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public interface IObligationCalculationInstructionService
{
    Task<ObligationCalculationInstruction> Create(
        int obligationYear,
        Guid? submitterId,
        CancellationToken cancellationToken,
        string? traceId = null
    );
    Task<ObligationCalculationInstruction> Create(
        IClientSessionHandle session,
        int obligationYear,
        Guid? submitterId,
        CancellationToken cancellationToken,
        string? traceId = null
    );
}
