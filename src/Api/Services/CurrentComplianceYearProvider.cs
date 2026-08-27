namespace Defra.WasteObligations.Api.Services;

public class CurrentComplianceYearProvider(TimeProvider timeProvider) : ICurrentComplianceYearProvider
{
    private static readonly TimeZoneInfo UnitedKingdomTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public int GetCurrentComplianceYear()
    {
        var localDate = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), UnitedKingdomTimeZone).Date;

        return localDate.Month is 1 ? localDate.Year - 1 : localDate.Year;
    }
}
