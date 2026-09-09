using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Services.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class ReadAuditEventDispatchLease
{
    public const string OperationId = "ReadAdminAuditEventDispatchLease";

    public static void MapAuditEventDispatchLeaseRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/audit-events/dispatch-leases/{processName}", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static async Task<IResult> Handle(
        [FromRoute] string processName,
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    )
    {
        var lease = await adminDataService.ReadAuditEventDispatchLease(processName, cancellationToken);

        return lease is null ? Results.NotFound() : Results.Ok(lease);
    }
}
