namespace Defra.WasteObligations.Api.Services;

public class CurrentObligationYearProvider(TimeProvider timeProvider) : ICurrentObligationYearProvider
{
    private static readonly TimeZoneInfo UnitedKingdomTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public int GetCurrentObligationYear()
    {
        var utcNow = timeProvider.GetUtcNow();
        var localNow = TimeZoneInfo.ConvertTime(utcNow, UnitedKingdomTimeZone);

        return GetCurrentObligationYear(localNow);
    }

    private static int GetCurrentObligationYear(DateTimeOffset localNow) =>
        localNow.Month is 1 ? localNow.Year - 1 : localNow.Year;
}
