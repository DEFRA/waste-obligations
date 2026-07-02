using System.Text.Json;

namespace Defra.WasteObligations.AuditEvents.Analytics;

public sealed class JsonSchemaDocument(JsonElement rootElement)
{
    private const string DefinitionReferencePrefix = "#/$defs/";

    public JsonElement RootElement { get; } = rootElement;

    public JsonElement ResolveLocalReference(JsonElement schemaElement)
    {
        if (
            schemaElement.ValueKind is JsonValueKind.Object
            && schemaElement.TryGetProperty("$ref", out var reference)
            && reference.GetString() is { } referenceValue
        )
        {
            if (!referenceValue.StartsWith(DefinitionReferencePrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unsupported schema reference '{referenceValue}'.");
            }

            var definitionName = referenceValue[DefinitionReferencePrefix.Length..];

            return RootElement.GetProperty("$defs").GetProperty(definitionName);
        }

        return schemaElement;
    }
}
