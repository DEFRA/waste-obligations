using Defra.WasteObligations.Api.Utils.Logging;
using Defra.WasteObligations.AuditEvents.Logging;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Utils.Http;

public class CorrelationIdHandler(IOptions<TraceHeader> options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var traceId = CorrelationContext.Current;
        var headerName = options.Value.Name;
        if (traceId is not null && !request.Headers.Contains(headerName))
            request.Headers.TryAddWithoutValidation(headerName, traceId);

        return base.SendAsync(request, cancellationToken);
    }
}
