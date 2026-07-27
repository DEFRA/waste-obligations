using System.Text.Json.Nodes;
using Defra.WasteObligations.Api.Dtos;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Defra.WasteObligations.Api.Endpoints.Organisations.Prns;

public class SearchPrnsOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (operation.OperationId is not SearchPrns.OperationId)
            return Task.CompletedTask;

        ReplaceParameter<OrganisationPrnStatus>(operation, nameof(SearchOrganisationPrnsRequest.Status));
        ReplaceParameter<OrganisationPrnSort>(operation, nameof(SearchOrganisationPrnsRequest.Sort));

        return Task.CompletedTask;
    }

    private static void ReplaceParameter<T>(OpenApiOperation operation, string propertyName)
        where T : struct, Enum
    {
        var parameterName = ToCamelCase(propertyName);

        if (
            operation.Parameters?.FirstOrDefault(x => x.In == ParameterLocation.Query && x.Name == parameterName)
            is not OpenApiParameter parameter
        )
            return;

        parameter.Schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = Enum.GetNames<T>().Select(x => (JsonNode)JsonValue.Create(x)!).ToList(),
        };
    }

    private static string ToCamelCase(string input) => char.ToLowerInvariant(input[0]) + input[1..];
}
