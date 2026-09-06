namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapUnsubmittedPollingStatus();
    }
}
