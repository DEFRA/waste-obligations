using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.WasteObligations.Api.Serialization;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class AdminJsonSerializerOptions
{
    public static JsonSerializerOptions Value { get; } =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter(),
                new ObjectIdJsonConverter(),
                new BsonDocumentJsonConverter(),
            },
        };
}
