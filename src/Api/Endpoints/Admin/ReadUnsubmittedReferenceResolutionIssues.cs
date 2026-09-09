using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class ReadUnsubmittedReferenceResolutionIssues
{
    public const string OperationId = "ReadUnsubmittedReferenceResolutionIssues";

    public static void MapUnsubmittedReferenceResolutionIssues(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/unsubmitted-compliance-declarations/reference-resolution-issues", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static async Task<IResult> Handle(
        [FromServices] IUnsubmittedReferenceResolutionIssuesService referenceResolutionIssuesService,
        CancellationToken cancellationToken
    ) => Results.Ok(await referenceResolutionIssuesService.Get(cancellationToken));
}
