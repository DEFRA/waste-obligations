using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.ComplianceDeclarations;

public static class DeleteComplianceDeclaration
{
    public const string OperationId = "DeleteComplianceDeclaration";

    public static void MapComplianceDeclarationDelete(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/compliance-declarations/{id}", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Write)
            .AddEndpointFilter<AllowedEndpointFilter>();
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
