namespace Defra.WasteObligations.Testing.Extensions;

public static class StringExtensions
{
    private static readonly Random s_random = new();

    public static string Random(this string[] values) => values[s_random.Next(0, values.Length)];
}
