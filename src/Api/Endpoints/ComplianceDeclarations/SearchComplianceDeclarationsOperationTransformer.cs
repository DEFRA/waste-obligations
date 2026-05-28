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

        var statusName = ToCamelCase(nameof(SearchComplianceDeclarationsRequest.Status));
        var statusParameter = operation.Parameters?.FirstOrDefault(p =>
            p.In == ParameterLocation.Query && p.Name == statusName
        );

        if (statusParameter != null)
        {
            operation.Parameters?.Remove(statusParameter);

            var newTypeParameter = new OpenApiParameter
            {
                Name = statusName,
                In = ParameterLocation.Query,
                Description = statusParameter.Description,
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchemaReference(nameof(ComplianceDeclarationStatus))
                    {
                        Reference = new JsonSchemaReference
                        {
                            Type = ReferenceType.Schema,
                            Id = nameof(ComplianceDeclarationStatus),
                        },
                    },
                },
            };

            operation.Parameters?.Insert(0, newTypeParameter);
        }

        var pageName = ToCamelCase(nameof(SearchComplianceDeclarationsRequest.Page));
        var pageParameter = operation.Parameters?.FirstOrDefault(p =>
            p.In == ParameterLocation.Query && p.Name == pageName
        );

        if (pageParameter != null)
        {
            operation.Parameters?.Remove(pageParameter);

            var newPageParameter = new OpenApiParameter
            {
                Name = pageName,
                In = ParameterLocation.Query,
                Description = pageParameter.Description,
                Schema = new OpenApiSchema
                {
                    Type = pageParameter.Schema!.Type,
                    Minimum = pageParameter.Schema!.Minimum,
                    Maximum = null,
                    Pattern = pageParameter.Schema!.Pattern,
                    Format = pageParameter.Schema!.Format,
                },
            };

            operation.Parameters?.Insert(3, newPageParameter);
        }

        return Task.CompletedTask;
    }

    private static string ToCamelCase(string input) => char.ToLowerInvariant(input[0]) + input[1..];
}
