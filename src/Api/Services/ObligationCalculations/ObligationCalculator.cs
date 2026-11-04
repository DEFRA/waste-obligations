using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public class ObligationCalculator(IOptions<ObligationCalculationOptions> options, TimeProvider timeProvider)
    : IObligationCalculator
{
    public ObligationCalculation Calculate(ApprovedSubmission submission, int obligationYear)
    {
        ArgumentNullException.ThrowIfNull(submission);
        Validate(submission);
        if (!options.Value.Targets.TryGetValue(obligationYear, out var targets))
        {
            throw new ArgumentOutOfRangeException(
                nameof(obligationYear),
                "No targets are configured for this obligation year"
            );
        }

        var producers = submission
            .Producers.Select(producer => new ProducerObligationCalculation
            {
                ProducerOrganisationId = producer.ProducerOrganisationId,
                Materials = producer.Materials.SelectMany(material => CalculateMaterial(material, targets)).ToArray(),
            })
            .ToArray();

        return new ObligationCalculation
        {
            SubmitterOrganisationId = submission.SubmitterOrganisationId,
            SubmitterType = submission.SubmitterType,
            ObligationYear = obligationYear,
            CalculatedOn = timeProvider.GetUtcNowWithoutMicroseconds(),
            Producers = producers,
        };
    }

    private static IEnumerable<MaterialObligationCalculation> CalculateMaterial(
        MaterialWeight material,
        Dictionary<string, double> targets
    )
    {
        var tonnes = (long)decimal.Round(material.Kilograms / 1000m, 0, MidpointRounding.AwayFromZero);
        var initialTarget = (decimal)targets[material.MaterialCode] * tonnes;
        var total = (long)decimal.Ceiling(initialTarget);
        if (material.MaterialCode == ObligationMaterials.Glass)
        {
            var remelt = (long)decimal.Ceiling(initialTarget * (decimal)targets[ObligationMaterials.GlassRemelt]);

            return
            [
                new()
                {
                    MaterialCode = ObligationMaterials.Glass,
                    InputTonnage = tonnes,
                    Obligation = total - remelt,
                },
                new()
                {
                    MaterialCode = ObligationMaterials.GlassRemelt,
                    InputTonnage = tonnes,
                    Obligation = remelt,
                },
            ];
        }

        return
        [
            new()
            {
                MaterialCode = material.MaterialCode,
                InputTonnage = tonnes,
                Obligation = total,
            },
        ];
    }

    private static void Validate(ApprovedSubmission submission)
    {
        if (
            submission.SubmitterOrganisationId == Guid.Empty
            || !Enum.IsDefined(submission.SubmitterType)
            || submission.Producers is null
            || submission.Producers.Count == 0
        )
        {
            throw new ArgumentException("A submitter identity, type and producers are required", nameof(submission));
        }

        var producerIds = new HashSet<Guid>();
        foreach (var producer in submission.Producers)
        {
            if (
                producer is null
                || producer.ProducerOrganisationId == Guid.Empty
                || !producerIds.Add(producer.ProducerOrganisationId)
                || (
                    submission.SubmitterType == ObligationSubmitterType.DirectRegistrant
                    && producer.ProducerOrganisationId != submission.SubmitterOrganisationId
                )
                || producer.Materials is null
                || producer.Materials.Count == 0
            )
            {
                throw new ArgumentException(
                    "Producer identities must be valid and unique, with material totals",
                    nameof(submission)
                );
            }

            var materialCodes = new HashSet<string>();
            foreach (var material in producer.Materials)
            {
                if (
                    material is null
                    || !ObligationMaterials.Inputs.Contains(material.MaterialCode)
                    || !materialCodes.Add(material.MaterialCode)
                    || material.Kilograms < 0
                )
                {
                    throw new ArgumentException(
                        "Material totals must have unique supported input codes and non-negative kilograms",
                        nameof(submission)
                    );
                }
            }
        }
    }
}
