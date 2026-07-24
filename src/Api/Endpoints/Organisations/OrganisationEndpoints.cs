using Defra.WasteObligations.Api.Endpoints.Organisations.ComplianceDeclarations;
using Defra.WasteObligations.Api.Endpoints.Organisations.Obligations;
using Defra.WasteObligations.Api.Endpoints.Organisations.Prns;

namespace Defra.WasteObligations.Api.Endpoints.Organisations;

public static class OrganisationEndpoints
{
    public static void MapOrganisationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapObligationsRead();
        app.MapPrnRead();

        app.MapComplianceDeclarationsCreate();
        app.MapComplianceDeclarationsRead();
        app.MapComplianceDeclarationRead();
        app.MapComplianceDeclarationUpdate();
    }
}
