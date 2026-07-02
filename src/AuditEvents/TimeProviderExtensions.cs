namespace Defra.WasteObligations.AuditEvents;

internal static class TimeProviderExtensions
{
    public static DateTime GetUtcNowWithoutMicroseconds(this TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        now = new DateTimeOffset(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);

        return now.UtcDateTime;
    }
}
