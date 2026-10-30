using Defra.WasteObligations.Api.Services.Scheduling;
using Microsoft.Extensions.Logging;

namespace Defra.WasteObligations.Api.ScheduledJobs;

public sealed class ObligationCalculationJob(ILogger<ObligationCalculationJob> logger) : IScheduledJob
{
    public Task Run(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Obligation calculation placeholder executed; calculation is not yet implemented");

        return Task.CompletedTask;
    }
}
