using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.ComplianceDeclarations;

public static class DeleteComplianceDeclaration
{
    public static void MapComplianceDeclarationDelete(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/compliance-declarations/{id}", Handle)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Write);
    }

    private static async Task<IResult> Handle(
        [FromRoute] string id,
        [FromServices] IComplianceDeclarationService complianceDeclarationService,
        CancellationToken cancellationToken
    )
    {
        var deleted = await complianceDeclarationService.Delete(id, cancellationToken);

        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
