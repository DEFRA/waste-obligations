using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Defra.WasteObligations.Api.Endpoints;

public class OpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        document.Info = new OpenApiInfo
        {
            Title = "Waste Obligations REST API",
            Version = "0.0.1",
            Description = "Manage obligation data in relation to EPR",
        };

        return Task.CompletedTask;
    }
}
