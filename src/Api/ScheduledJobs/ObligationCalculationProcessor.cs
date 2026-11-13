using System.Text.Json;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Api.Services.Packaging;
using Defra.WasteObligations.Api.Services.Scheduling;
using Microsoft.Extensions.Logging;

namespace Defra.WasteObligations.Api.ScheduledJobs;

public class ObligationCalculationProcessor(
    IPackagingSource packagingSource,
    IObligationCalculator calculator,
    IObligationCalculationService calculationService,
    ILogger<ObligationCalculationProcessor> logger
) : IScheduledJobProcessor<ObligationCalculationInstruction>
{
    public async Task Process(ObligationCalculationInstruction work, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = await packagingSource.GetFullYear(work.ObligationYear - 1, cancellationToken);
        var matched = false;
        foreach (var item in response.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Guid? submitterId = null;
            try
            {
                submitterId = item.GetProperty("submitterOrganisationId").GetGuid();
                if (work.SubmitterId is not null && submitterId != work.SubmitterId)
                    continue;

                matched = true;
                var submission = MapSubmission(item, submitterId.Value);
                var calculation = calculator.Calculate(submission, work.ObligationYear);
                await calculationService.Save(calculation, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(
                    exception,
                    "Obligation calculation failed for instruction {InstructionId}, obligation year {ObligationYear} and submitter {SubmitterId}",
                    work.Id,
                    work.ObligationYear,
                    submitterId
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

    private static ApprovedSubmission MapSubmission(JsonElement item, Guid submitterId) =>
        new()
        {
            SubmitterOrganisationId = submitterId,
            SubmitterType = item.GetProperty("submitterType").GetString() switch
            {
                "DirectRegistrant" => ObligationSubmitterType.DirectRegistrant,
                "ComplianceScheme" => ObligationSubmitterType.ComplianceScheme,
                _ => throw new InvalidOperationException("Packaging submitter type is not supported"),
            },
            Producers = item.GetProperty("producers")
                .EnumerateArray()
                .Select(x => new ProducerPackaging
                {
                    ProducerOrganisationId = x.GetProperty("producerOrganisationId").GetGuid(),
                    Materials = x.GetProperty("materials")
                        .EnumerateArray()
                        .Select(x => new MaterialWeight
                        {
                            MaterialCode =
                                x.GetProperty("materialCode").GetString()
                                ?? throw new InvalidOperationException("Packaging material code is required"),
                            Kilograms = x.GetProperty("kilograms").GetInt64(),
                        })
                        .ToArray(),
                })
                .ToArray(),
        };
}
