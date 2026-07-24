using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Defra.WasteObligations.Api.Endpoints.OpenApi;

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

        var configuration = context.ApplicationServices.GetRequiredService<IConfiguration>();
        var scheme = configuration.GetValue<string>("OpenApi:Scheme") ?? "https";
        var host = configuration.GetValue<string>("OpenApi:Host") ?? "localhost";

        document.Servers = new List<OpenApiServer> { new() { Url = $"{scheme}://{host}" } };

        return Task.CompletedTask;
    }
}
