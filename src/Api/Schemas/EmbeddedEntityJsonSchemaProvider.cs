using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Defra.WasteObligations.AuditEvents.Analytics;

namespace Defra.WasteObligations.Api.Schemas;

public sealed class EmbeddedEntityJsonSchemaProvider : IEntityJsonSchemaProvider
{
    private const string SchemaFileSuffix = ".schema.json";

    private readonly ConcurrentDictionary<string, JsonSchemaDocument> _schemas = [];
    private readonly Assembly _assembly = typeof(EmbeddedEntityJsonSchemaProvider).Assembly;

    public JsonSchemaDocument Get(string entity, string schemaVersion)
    {
        var schemaFileName = GetSchemaFileName(entity, schemaVersion);

        return _schemas.GetOrAdd(schemaFileName, LoadSchema);
    }

    private JsonSchemaDocument LoadSchema(string schemaFileName)
    {
        var resourceName = FindResourceName(schemaFileName);
        using var stream = _assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            throw new FileNotFoundException($"Could not load embedded entity schema resource '{resourceName}'.");
        }

        using var document = JsonDocument.Parse(stream);
        return new JsonSchemaDocument(document.RootElement.Clone());
    }

    private string FindResourceName(string schemaFileName)
    {
        var resourceName = _assembly
            .GetManifestResourceNames()
            .SingleOrDefault(x => x.EndsWith($".{schemaFileName}", StringComparison.Ordinal));

        if (resourceName is not null)
        {
            return resourceName;
        }

        throw new FileNotFoundException($"Could not find embedded entity schema '{schemaFileName}'.");
    }

    private static string GetSchemaFileName(string entity, string schemaVersionValue)
    {
        var schemaVersionPrefix = $"{entity}.";
        var schemaVersion = schemaVersionValue.StartsWith(schemaVersionPrefix, StringComparison.Ordinal)
            ? schemaVersionValue[schemaVersionPrefix.Length..]
            : schemaVersionValue;

        return $"{ToKebabCase(entity)}.{schemaVersion}{SchemaFileSuffix}";
    }

    private static string ToKebabCase(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(character is '_' ? '-' : char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
