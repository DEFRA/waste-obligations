namespace Defra.WasteObligations.Api.Services;

public interface ICurrentComplianceYearProvider
{
    int GetCurrentComplianceYear();
    ComplianceYearHandover GetHandover(TimeSpan outgoingYearGracePeriod);
}
