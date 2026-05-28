using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.ComplianceDeclarations;

public static class SearchComplianceDeclarations
{
    public const string OperationId = "SearchComplianceDeclarations";

    public static void MapComplianceDeclarationsSearch(this IEndpointRouteBuilder app)
    {
        app.MapGet("/compliance-declarations", Handle)
            .WithName(OperationId)
            .WithTags("Search")
            .WithSummary("Search compliance declarations")
            .WithDescription("Returns a paged list of compliance declarations filtered by optional parameters")
            .Produces<ComplianceDeclarationsPaged>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Read);
    }

    private static async Task<IResult> Handle(
        [AsParameters] SearchComplianceDeclarationsRequest request,
        [FromServices] IComplianceDeclarationService complianceDeclarationService,
        CancellationToken cancellationToken
    )
    {
        var page = request.EffectivePage;
        var pageSize = request.EffectivePageSize;
        var result = await complianceDeclarationService.Search(
            request.ObligationYear,
            [.. request.ParsedStatus().Select(x => x.ToEntity())],
            request.OrganisationName,
            page,
            pageSize,
            cancellationToken
        );

        return Results.Ok(result.ToDto(page, pageSize));
    }
}
