using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Dtos;

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
        CancellationToken cancellationToken
    )
    {
        request.ParsedStatus();

        await Task.Delay(1, cancellationToken);

        return Results.Ok(new ComplianceDeclarationsPaged());
    }
}
