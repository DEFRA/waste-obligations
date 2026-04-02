using System.Reflection;
using System.Text.Json.Nodes;
using Defra.WasteObligations.Api.Dtos.Attributes;
using Defra.WasteObligations.Api.Extensions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Defra.WasteObligations.Api.Endpoints.OpenApi;

public class PossibleValueSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var memberInfo = context.JsonPropertyInfo?.AttributeProvider as MemberInfo;

        if (memberInfo == null)
            return Task.CompletedTask;

        var possibleValues = memberInfo
            .CustomAttributes.Where(x => x.AttributeType == typeof(PossibleValueAttribute))
            .ToList();

        if (possibleValues.Count == 0)
            return Task.CompletedTask;

        foreach (
            var possibleValue in possibleValues
                .Select(x => x.ConstructorArguments.FirstOrDefault().Value?.ToString())
                .NotNull()
        )
        {
            schema.Enum ??= [];
            schema.Enum.Add(JsonValue.Create(possibleValue));
        }

        return Task.CompletedTask;
    }
}
