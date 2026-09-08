using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class StartUnsubmittedHistoricalBackfill
{
    public const string OperationId = "StartUnsubmittedHistoricalBackfill";

    public static void MapUnsubmittedHistoricalBackfill(this IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/unsubmitted-compliance-declarations/historical-backfill", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static async Task<IResult> Handle(
        [FromServices] IUnsubmittedHistoricalBackfillService historicalBackfillService,
        CancellationToken cancellationToken
    ) => Results.Ok(await historicalBackfillService.Start(cancellationToken));
}
