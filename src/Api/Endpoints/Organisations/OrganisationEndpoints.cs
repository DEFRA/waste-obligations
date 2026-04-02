using Defra.WasteObligations.Api.Endpoints.Organisations.Obligations;

namespace Defra.WasteObligations.Api.Endpoints.Organisations;

public static class OrganisationEndpoints
{
    public static void MapOrganisationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapObligationsRead();
    }
}
