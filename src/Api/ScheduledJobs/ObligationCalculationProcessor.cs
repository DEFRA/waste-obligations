using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.Scheduling;
using Microsoft.Extensions.Logging;

namespace Defra.WasteObligations.Api.ScheduledJobs;

public class ObligationCalculationProcessor(ILogger<ObligationCalculationProcessor> logger)
    : IScheduledJobProcessor<ObligationCalculationInstruction>
{
    public Task Process(ObligationCalculationInstruction work, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation(
            "Obligation calculation placeholder handled instruction {InstructionId} for obligation year {ObligationYear} and submitter {SubmitterId}; no calculation performed",
            work.Id,
            work.ObligationYear,
            work.SubmitterId
        );

        return Task.CompletedTask;
    }
}
