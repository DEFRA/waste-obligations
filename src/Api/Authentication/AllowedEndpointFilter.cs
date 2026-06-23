using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Authentication;

public class AllowedEndpointFilter(IOptions<AclOptions> aclOptions) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var endpointName = context
            .HttpContext.GetEndpoint()
            ?.Metadata.GetMetadata<IEndpointNameMetadata>()
            ?.EndpointName;

        if (string.IsNullOrWhiteSpace(endpointName))
            return Results.NotFound();

        var clientId =
            context.HttpContext.User.FindFirstValue(Claims.ClientId) ?? context.HttpContext.User.Identity?.Name;

        if (
            clientId is null
            || !aclOptions.Value.Clients.TryGetValue(clientId, out var client)
            || !client.AllowedEndpoints.Contains(endpointName, StringComparer.Ordinal)
        )
            return Results.NotFound();

        return await next(context);
    }
}
