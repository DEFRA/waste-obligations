using Defra.WasteObligations.Api.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Example;

public static class Put
{
    public static void MapExamplePut(this IEndpointRouteBuilder app)
    {
        app.MapPut("/example/{id:guid}", Handle).ExcludeFromDescription().RequireAuthorization(PolicyNames.Write);
    }

    [HttpPut]
    private static async Task<IResult> Handle([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await Task.Yield();

        return Results.Ok(id);
    }
}
