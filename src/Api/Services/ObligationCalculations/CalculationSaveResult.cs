using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public sealed record CalculationSaveResult(ObligationCalculation Calculation, CalculationSaveOutcome Outcome);
