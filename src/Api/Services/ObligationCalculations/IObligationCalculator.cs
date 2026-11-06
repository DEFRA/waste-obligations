using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public interface IObligationCalculator
{
    ObligationCalculation Calculate(ApprovedSubmission submission, int obligationYear);
}
