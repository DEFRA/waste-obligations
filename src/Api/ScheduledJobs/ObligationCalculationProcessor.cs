using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Api.Services.Scheduling;
using Defra.WasteObligations.Api.Services.WastePackaging;
using Defra.WasteObligations.Api.Utils.Metrics;
using Microsoft.Extensions.Logging;

namespace Defra.WasteObligations.Api.ScheduledJobs;

public class ObligationCalculationProcessor(
    IWastePackagingService wastePackagingService,
    IObligationCalculator calculator,
    IObligationCalculationService calculationService,
    IObligationCalculationMetrics metrics,
    TimeProvider timeProvider,
    ILogger<ObligationCalculationProcessor> logger
) : IScheduledJobProcessor<ObligationCalculationInstruction>
{
    public async Task Process(ObligationCalculationInstruction work, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PackagingResponse response;
        var start = timeProvider.GetTimestamp();
        try
        {
            response = await wastePackagingService.Read(work.ObligationYear - 1, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            metrics.Instruction(CalculationInstructionOutcome.PackagingFailure);
            throw;
        }
        finally
        {
            metrics.FetchDuration(timeProvider.GetElapsedTime(start));
        }
        var hasErrors = false;
        var matched = false;
        foreach (var item in response.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (work.SubmitterId is not null && item.SubmitterOrganisationId != work.SubmitterId)
                    continue;

                matched = true;
                var calculation = calculator.Calculate(item, work.ObligationYear);
                var result = await calculationService.SaveWithOutcome(calculation, cancellationToken);
                metrics.Submitter(result.Outcome);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                hasErrors = true;
                metrics.SubmitterFailed();
                logger.LogError(
                    exception,
                    "Obligation calculation failed for instruction {InstructionId}, obligation year {ObligationYear} and submitter {SubmitterId}",
                    work.Id,
                    work.ObligationYear,
                    item.SubmitterOrganisationId
                );
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        metrics.Instruction(
            hasErrors ? CalculationInstructionOutcome.SubmitterErrors : CalculationInstructionOutcome.Success
        );
        if (response.Items.Count == 0)
            logger.LogInformation(
                "No packaging data for instruction {InstructionId} and obligation year {ObligationYear}",
                work.Id,
                work.ObligationYear
            );
        else if (work.SubmitterId is not null && !matched)
            logger.LogInformation(
                "No packaging data matched instruction {InstructionId}, obligation year {ObligationYear} and submitter {SubmitterId}",
                work.Id,
                work.ObligationYear,
                work.SubmitterId
            );
    }
}
