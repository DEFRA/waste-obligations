using Defra.WasteObligations.Api.Services;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Data;

// The worker owns the correction lease. A failed correction must not advance completion history.
public class ApplicationDataCorrectionRunner(
    IMongoDatabase database,
    IEnumerable<IApplicationDataCorrection> corrections,
    TimeProvider timeProvider,
    ILogger<ApplicationDataCorrectionRunner> logger,
    IOptions<ApplicationDataCorrectionOptions> options
) : IApplicationDataCorrectionRunner
{
    public async Task Run(CancellationToken cancellationToken)
    {
        var ordered = corrections.OrderBy(x => x.Version).ToArray();
        if (
            ordered.Any(x => x.Version < 1)
            || ordered.Select(x => x.Version).Distinct().Count() != ordered.Length
            || ordered.Select(x => x.Id).Distinct().Count() != ordered.Length
        )
            throw new InvalidOperationException(
                "Application data correction IDs and positive versions must be unique."
            );

        var collection = database
            .GetCollection<ApplicationDataCorrectionCompletion>("_application_data_corrections")
            .WithReadPreference(ReadPreference.Primary);
        foreach (var correction in ordered)
        {
            var completed = await BackgroundOperationWatchdog.Run(
                token => collection.Find(x => x.Id == correction.Id).SingleOrDefaultAsync(token),
                TimeSpan.FromSeconds(options.Value.OperationTimeoutSeconds),
                () =>
                    logger.LogError(
                        "Completion read stalled for correction {CorrectionId}. Cancellation was requested; waiting for the read to stop.",
                        correction.Id
                    ),
                cancellationToken,
                timeProvider
            );
            if (completed is not null)
            {
                if (completed.Version != correction.Version)
                    throw new InvalidOperationException(
                        $"Completed correction '{correction.Id}' has a different version."
                    );

                continue;
            }

            logger.LogInformation(
                "Starting application data correction {CorrectionId} version {CorrectionVersion}.",
                correction.Id,
                correction.Version
            );
            await correction.Apply(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await BackgroundOperationWatchdog.Run(
                token =>
                    collection.InsertOneAsync(
                        new ApplicationDataCorrectionCompletion
                        {
                            Id = correction.Id,
                            Version = correction.Version,
                            CompletedAt = timeProvider.GetUtcNowWithoutMicroseconds(),
                        },
                        cancellationToken: token
                    ),
                TimeSpan.FromSeconds(options.Value.OperationTimeoutSeconds),
                () =>
                    logger.LogError(
                        "Completion write stalled for correction {CorrectionId}. Cancellation was requested; waiting for the write to stop.",
                        correction.Id
                    ),
                cancellationToken,
                timeProvider
            );
            logger.LogInformation("Application data correction {CorrectionId} completed.", correction.Id);
        }
    }
}
