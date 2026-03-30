using Defra.WasteObligations.Api.Endpoints.Example;

namespace Defra.WasteObligations.Api.Endpoints;

public static class Endpoints
{
    public static void MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapExampleGet();
        app.MapExamplePut();
    }
}
