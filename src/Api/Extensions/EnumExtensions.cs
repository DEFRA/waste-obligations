using System.Text.Json;
using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Extensions;

public static class EnumExtensions
{
    private static JsonSerializerOptions JsonOptions { get; } =
        new() { Converters = { new JsonStringEnumConverter() } };

    public static string ToJsonValue<TEnum>(this TEnum value)
        where TEnum : struct, Enum => JsonSerializer.Serialize(value, JsonOptions).Trim('"');

    public static TEnum FromJsonValue<TEnum>(this string value)
        where TEnum : struct, Enum => JsonSerializer.Deserialize<TEnum>($"\"{value}\"", JsonOptions);
}
