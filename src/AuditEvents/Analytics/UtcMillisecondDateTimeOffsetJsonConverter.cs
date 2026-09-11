using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Defra.WasteObligations.AuditEvents.Analytics;

public sealed class UtcMillisecondDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException($"{nameof(UtcMillisecondDateTimeOffsetJsonConverter)} only supports writing.");

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        var utcValue = value.ToUniversalTime();
        var milliseconds = utcValue.Ticks - utcValue.Ticks % TimeSpan.TicksPerMillisecond;
        var utcMilliseconds = new DateTimeOffset(milliseconds, TimeSpan.Zero);

        writer.WriteStringValue(utcMilliseconds.ToString(Format, CultureInfo.InvariantCulture));
    }
}
