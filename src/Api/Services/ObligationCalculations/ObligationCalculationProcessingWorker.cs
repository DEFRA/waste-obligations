using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public class ObligationCalculationProcessingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ObligationCalculationProcessingOptions> options,
    ILogger<ObligationCalculationProcessingWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Obligation calculation instruction processing is off");

            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IObligationCalculationInstructionProcessor>();
                if (await processor.ProcessNext(stoppingToken))
                    continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Obligation calculation instruction processing failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), stoppingToken);
        }
    }
}
