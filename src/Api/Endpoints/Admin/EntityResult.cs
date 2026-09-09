using System.Text.Json;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public class EntityResult<T>(T entity) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            entity,
            AdminJsonSerializerOptions.Value,
            httpContext.RequestAborted
        );
    }
}
