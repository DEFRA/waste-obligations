using System.Diagnostics.CodeAnalysis;

namespace Defra.WasteObligations.Api.Data;

[ExcludeFromCodeCoverage(Justification = "See integration tests")]
public class MongoIndexService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
