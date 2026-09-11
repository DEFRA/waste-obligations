namespace Defra.WasteObligations.Api.Utils;

internal static class SonarCoverageProbe
{
    public static bool IsEnabled(DateTimeOffset timestamp)
    {
        return timestamp > DateTimeOffset.MinValue;
    }
}
