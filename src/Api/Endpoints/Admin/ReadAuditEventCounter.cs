using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Services.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class ReadAuditEventCounter
{
    public const string OperationId = "ReadAdminAuditEventCounter";

    public static void MapAuditEventCounterRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/audit-events/counter", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static async Task<IResult> Handle(
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    )
    {
        var counter = await adminDataService.ReadAuditEventCounter(cancellationToken);

        return counter is null ? Results.NotFound() : Results.Ok(counter);
    }
}
