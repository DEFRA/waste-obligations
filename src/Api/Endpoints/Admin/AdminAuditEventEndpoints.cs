using Defra.WasteObligations.Api.Services.Admin;
using Defra.WasteObligations.AuditEvents.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class AdminAuditEventEndpoints
{
    public static void MapAdminAuditEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("audit-events", HandleAuditEvents).ExcludeFromDescription();
        app.MapGet("audit-events/counter", HandleAuditEventCounter).ExcludeFromDescription();
    }

    private static EntityStreamResult<AuditEvent> HandleAuditEvents(
        [AsParameters] ReadAuditEventsRequest request,
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    ) =>
        new EntityStreamResult<AuditEvent>(
            adminDataService.ReadAuditEvents(
                request.AfterSequence,
                request.EffectiveLimit,
                request.Entity,
                request.EntityId,
                cancellationToken
            )
        );

    private static async Task<IResult> HandleAuditEventCounter(
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    )
    {
        var counter = await adminDataService.ReadAuditEventCounter(cancellationToken);

        return counter is null ? Results.NotFound() : new EntityResult<AuditEventCounter>(counter);
    }
}
