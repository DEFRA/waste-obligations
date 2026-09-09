using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public class EntityStreamResult<T>(IAsyncEnumerable<T> entities) : IResult
{
    public const string ContentType = "application/x-ndjson";

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = ContentType;
        var jsonOptions = httpContext
            .RequestServices.GetRequiredService<IOptions<JsonOptions>>()
            .Value.SerializerOptions;

        await foreach (var entity in entities.WithCancellation(httpContext.RequestAborted))
        {
            await JsonSerializer.SerializeAsync(
                httpContext.Response.Body,
                entity,
                jsonOptions,
                httpContext.RequestAborted
            );
            await httpContext.Response.WriteAsync("\n", httpContext.RequestAborted);
            await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);
        }
    }
}
