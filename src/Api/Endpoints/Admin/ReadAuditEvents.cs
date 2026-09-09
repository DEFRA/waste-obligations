using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Services.Admin;
using Defra.WasteObligations.AuditEvents.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class ReadAuditEvents
{
    public const string OperationId = "ReadAdminAuditEvents";

    public static void MapAuditEventsRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/audit-events", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static IResult Handle(
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
}
