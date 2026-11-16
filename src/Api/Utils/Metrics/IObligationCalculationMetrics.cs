using Defra.WasteObligations.Api.Services.ObligationCalculations;

namespace Defra.WasteObligations.Api.Utils.Metrics;

public interface IObligationCalculationMetrics
{
    void Submitter(CalculationSaveOutcome outcome);
    void SubmitterFailed();
    void Instruction(CalculationInstructionOutcome outcome);
    void FetchDuration(TimeSpan duration);
}
