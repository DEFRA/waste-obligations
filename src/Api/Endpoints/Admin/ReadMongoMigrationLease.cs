using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Services.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class ReadMongoMigrationLease
{
    public const string OperationId = "ReadAdminMongoMigrationLease";

    public static void MapMongoMigrationLeaseRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/mongo-migrations/lease", Handle)
            .WithName(OperationId)
            .ExcludeFromDescription()
            .RequireAuthorization(PolicyNames.Admin);
    }

    private static async Task<IResult> Handle(
        [FromServices] IAdminDataService adminDataService,
        CancellationToken cancellationToken
    )
    {
        var lease = await adminDataService.ReadMongoMigrationLease(cancellationToken);

        return lease is null ? Results.NotFound() : Results.Ok(lease);
    }
}
