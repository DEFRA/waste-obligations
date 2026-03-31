using Defra.WasteObligations.Api.Endpoints.Example;
using Defra.WasteObligations.Api.Endpoints.Organisations;

namespace Defra.WasteObligations.Api.Endpoints;

public static class Endpoints
{
    public static void MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapExampleGet();
        app.MapExamplePut();

        app.MapOrganisationEndpoints();
    }
}
