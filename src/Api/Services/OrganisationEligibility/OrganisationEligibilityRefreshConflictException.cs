namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public class OrganisationEligibilityRefreshConflictException(Exception? innerException = null)
    : Exception("The active organisation eligibility generation changed during refresh", innerException);
