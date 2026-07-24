using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.WasteObligations.Api.Utils.ErrorHandling;

namespace Defra.WasteObligations.Api.Dtos;

[JsonConverter(typeof(UpdatePrnStatusJsonConverter))]
public enum UpdatePrnStatus
{
    Accepted,
    Rejected,
}

public sealed class UpdatePrnStatusJsonConverter : JsonConverter<UpdatePrnStatus>
{
    private const string ErrorMessage = "Status must be either Accepted or Rejected.";

    public override UpdatePrnStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new InvalidRequestBodyException(ErrorMessage);

        return reader.GetString() switch
        {
            { } value when value.Equals(nameof(UpdatePrnStatus.Accepted), StringComparison.OrdinalIgnoreCase) =>
                UpdatePrnStatus.Accepted,
            { } value when value.Equals(nameof(UpdatePrnStatus.Rejected), StringComparison.OrdinalIgnoreCase) =>
                UpdatePrnStatus.Rejected,
            _ => throw new InvalidRequestBodyException(ErrorMessage),
        };
    }

    public override void Write(Utf8JsonWriter writer, UpdatePrnStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(
            value switch
            {
                UpdatePrnStatus.Accepted => nameof(UpdatePrnStatus.Accepted),
                UpdatePrnStatus.Rejected => nameof(UpdatePrnStatus.Rejected),
                _ => throw new ArgumentOutOfRangeException(nameof(value)),
            }
        );
    }
}
