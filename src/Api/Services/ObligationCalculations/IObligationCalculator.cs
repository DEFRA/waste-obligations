using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.WastePackaging;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public interface IObligationCalculator
{
    ObligationCalculation Calculate(ApprovedSubmission submission, int obligationYear);
}
