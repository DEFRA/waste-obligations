using System.Diagnostics.Metrics;
using Amazon.CloudWatch.EMF.Model;
using Defra.WasteObligations.Api.Services.ObligationCalculations;

namespace Defra.WasteObligations.Api.Utils.Metrics;

public sealed class ObligationCalculationMetrics : IObligationCalculationMetrics
{
    private readonly Counter<long> _submitter;
    private readonly Counter<long> _instruction;
    private readonly Histogram<double> _fetchDuration;

    public ObligationCalculationMetrics(IMeterFactory factory)
    {
        var meter = factory.Create(Metrics.MeterName);
        _submitter = meter.CreateCounter<long>(Metrics.Names.ObligationCalculationSubmitterOutcome, nameof(Unit.COUNT));
        _instruction = meter.CreateCounter<long>(
            Metrics.Names.ObligationCalculationInstructionOutcome,
            nameof(Unit.COUNT)
        );
        _fetchDuration = meter.CreateHistogram<double>(
            Metrics.Names.ObligationCalculationPackagingFetchDuration,
            nameof(Unit.SECONDS),
            "Logical full-year fetch including retries and response deserialisation"
        );
    }

    public void Submitter(CalculationSaveOutcome outcome) =>
        RecordSubmitter(
            outcome switch
            {
                CalculationSaveOutcome.Created => "created",
                CalculationSaveOutcome.Updated => "updated",
                CalculationSaveOutcome.Unchanged => "unchanged",
                _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
            }
        );

    public void SubmitterFailed() => RecordSubmitter("failed");

    private void RecordSubmitter(string outcome) =>
        _submitter.Add(1, new KeyValuePair<string, object?>(Metrics.Tags.Outcome, outcome));

    public void Instruction(CalculationInstructionOutcome outcome) =>
        _instruction.Add(
            1,
            new KeyValuePair<string, object?>(
                Metrics.Tags.Outcome,
                outcome switch
                {
                    CalculationInstructionOutcome.Success => "success",
                    CalculationInstructionOutcome.SubmitterErrors => "submitter_errors",
                    CalculationInstructionOutcome.PackagingFailure => "packaging_failure",
                    _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
                }
            )
        );

    public void FetchDuration(TimeSpan duration) => _fetchDuration.Record(duration.TotalSeconds);
}
