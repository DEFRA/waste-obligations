namespace Defra.WasteObligations.Api.Services;

public class CurrentComplianceYearProvider(TimeProvider timeProvider) : ICurrentComplianceYearProvider
{
    private static readonly TimeZoneInfo UnitedKingdomTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public int GetCurrentComplianceYear()
    {
        var utcNow = timeProvider.GetUtcNow();
        var localNow = TimeZoneInfo.ConvertTime(utcNow, UnitedKingdomTimeZone);

        return GetCurrentComplianceYear(localNow);
    }

    public ComplianceYearHandover GetHandover(TimeSpan outgoingYearGracePeriod)
    {
        var utcNow = timeProvider.GetUtcNow();
        var localNow = TimeZoneInfo.ConvertTime(utcNow, UnitedKingdomTimeZone);
        var currentComplianceYear = GetCurrentComplianceYear(localNow);

        if (localNow.Month is 1)
            return new ComplianceYearHandover(currentComplianceYear, IncomingComplianceYear: currentComplianceYear + 1);

        var cutover = new DateTimeOffset(localNow.Year, 2, 1, 0, 0, 0, localNow.Offset);
        if (localNow >= cutover && localNow < cutover.Add(outgoingYearGracePeriod))
        {
            return new ComplianceYearHandover(
                currentComplianceYear,
                OutgoingComplianceYear: currentComplianceYear - 1,
                OutgoingYearCutoverAt: cutover.UtcDateTime
            );
        }

        return new ComplianceYearHandover(currentComplianceYear);
    }

    private static int GetCurrentComplianceYear(DateTimeOffset localNow) =>
        localNow.Month is 1 ? localNow.Year - 1 : localNow.Year;
}
