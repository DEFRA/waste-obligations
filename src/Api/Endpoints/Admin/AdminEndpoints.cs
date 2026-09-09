using Defra.WasteObligations.Api.Authentication;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var adminApp = app.MapGroup("/admin").RequireAuthorization(PolicyNames.Admin);

        adminApp.MapAdminAuditEventEndpoints();
        adminApp.MapAdminOrganisationEndpoints();
        adminApp.MapAdminUnsubmittedComplianceDeclarationEndpoints();
    }
}
