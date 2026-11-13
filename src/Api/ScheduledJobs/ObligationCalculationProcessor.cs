using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Api.Services.Scheduling;
using Defra.WasteObligations.Api.Services.WastePackaging;
using Microsoft.Extensions.Logging;

namespace Defra.WasteObligations.Api.ScheduledJobs;

public class ObligationCalculationProcessor(
    IWastePackagingService wastePackagingService,
    IObligationCalculator calculator,
    IObligationCalculationService calculationService,
    ILogger<ObligationCalculationProcessor> logger
) : IScheduledJobProcessor<ObligationCalculationInstruction>
{
    public async Task Process(ObligationCalculationInstruction work, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = await wastePackagingService.Read(work.ObligationYear - 1, cancellationToken);
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
                await calculationService.Save(calculation, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
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
