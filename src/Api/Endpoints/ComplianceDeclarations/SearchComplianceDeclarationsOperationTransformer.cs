using Defra.WasteObligations.Api.Dtos;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Defra.WasteObligations.Api.Endpoints.ComplianceDeclarations;

public class SearchComplianceDeclarationsOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (operation.OperationId is not SearchComplianceDeclarations.OperationId)
            return Task.CompletedTask;

        ReplaceParameter(
            operation,
            nameof(SearchComplianceDeclarationsRequest.Status),
            index: 1,
            source => CreateEnumArrayParameter(source, nameof(ComplianceDeclarationStatus))
        );

        ReplaceParameter(
            operation,
            nameof(SearchComplianceDeclarationsRequest.RegistrationType),
            index: 2,
            source => CreateEnumArrayParameter(source, nameof(RegistrationType))
        );

        ReplaceParameter(
            operation,
            nameof(SearchComplianceDeclarationsRequest.Page),
            index: 4,
            source => new OpenApiParameter
            {
                Name = source.Name,
                In = ParameterLocation.Query,
                Description = source.Description,
                Schema = new OpenApiSchema
                {
                    Type = source.Schema!.Type,
                    Minimum = source.Schema.Minimum,
                    Maximum = null,
                    Pattern = source.Schema.Pattern,
                    Format = source.Schema.Format,
                },
            }
        );

        return Task.CompletedTask;
    }

    private static void ReplaceParameter(
        OpenApiOperation operation,
        string propertyName,
        int index,
        Func<OpenApiParameter, OpenApiParameter> transform
    )
    {
        var parameterName = ToCamelCase(propertyName);

        if (
            operation.Parameters?.FirstOrDefault(p => p.In == ParameterLocation.Query && p.Name == parameterName)
                is not OpenApiParameter parameter
            || operation.Parameters is null
        )
            return;

        operation.Parameters.Remove(parameter);
        operation.Parameters.Insert(index, transform(parameter));
    }

    private static OpenApiParameter CreateEnumArrayParameter(OpenApiParameter source, string schemaName)
    {
        return new OpenApiParameter
        {
            Name = source.Name,
            In = ParameterLocation.Query,
            Description = source.Description,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = new OpenApiSchemaReference(schemaName)
                {
                    Reference = new JsonSchemaReference { Type = ReferenceType.Schema, Id = schemaName },
                },
            },
        };
    }

    private static string ToCamelCase(string input) => char.ToLowerInvariant(input[0]) + input[1..];
}
