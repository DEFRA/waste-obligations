using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Api.Services.Scheduling;
using Microsoft.Extensions.Logging;

namespace Defra.WasteObligations.Api.ScheduledJobs;

public sealed class ObligationCalculationJob(
    ICurrentObligationYearProvider obligationYearProvider,
    IObligationCalculationInstructionService instructions,
    ILogger<ObligationCalculationJob> logger
) : IScheduledJob
{
    public async Task Run(CancellationToken cancellationToken)
    {
        var instruction = await instructions.Create(
            obligationYearProvider.GetCurrentObligationYear(),
            null,
            cancellationToken
        );
        logger.LogInformation(
            "Obligation calculation instruction {InstructionId} is {InstructionStatus} for obligation year {ObligationYear}",
            instruction.Id,
            instruction.Status,
            instruction.ObligationYear
        );
    }
}
