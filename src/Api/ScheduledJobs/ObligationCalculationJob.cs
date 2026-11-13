using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Api.Services.Scheduling;
using Defra.WasteObligations.Api.Utils.Logging;
using Defra.WasteObligations.AuditEvents.Logging;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.ScheduledJobs;

public sealed class ObligationCalculationJob(
    ICurrentObligationYearProvider obligationYearProvider,
    IObligationCalculationInstructionService instructions,
    ILogger<ObligationCalculationJob> logger
) : IScheduledJob
{
    public async Task Run(IClientSessionHandle session, CancellationToken cancellationToken)
    {
        var instruction = await instructions.Create(
            session,
            obligationYearProvider.GetCurrentObligationYear(),
            null,
            cancellationToken
        );
        using var scope = CorrelationContext.Begin(logger, instruction.CorrelationId);
        logger.LogInformation(
            "Preparing obligation calculation instruction {InstructionId} for obligation year {ObligationYear} in scheduled handoff transaction",
            instruction.Id,
            instruction.ObligationYear
        );
    }
}
