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

        var statusParameter = operation.Parameters?.FirstOrDefault(p =>
            p is { Name: "status", In: ParameterLocation.Query }
        );

        if (statusParameter != null)
        {
            operation.Parameters?.Remove(statusParameter);

            var newTypeParameter = new OpenApiParameter
            {
                Name = "status",
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

        return Task.CompletedTask;
    }
}
