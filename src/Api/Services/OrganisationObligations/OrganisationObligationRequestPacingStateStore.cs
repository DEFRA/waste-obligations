using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public class OrganisationObligationRequestPacingStateStore(IMongoDatabase database, TimeProvider timeProvider)
    : IOrganisationObligationRequestPacingStateStore
{
    private readonly IMongoCollection<OrganisationObligationRequestPacingState> _states =
        database.GetCollection<OrganisationObligationRequestPacingState>(
            OrganisationObligationRequestPacingState.CollectionName
        );

    public async Task<OrganisationObligationRequestPacingState?> Get(CancellationToken cancellationToken)
    {
        var state = await _states
            .Find(x => x.Id == OrganisationObligationRequestPacingState.StateId)
            .SingleOrDefaultAsync(cancellationToken);

        return state;
    }

    public async Task<OrganisationObligationRequestPacingState> Update(
        Func<OrganisationObligationRequestPacingState, OrganisationObligationRequestPacingState> update,
        CancellationToken cancellationToken
    )
    {
        while (true)
        {
            var existing = await Get(cancellationToken);
            var state = update(existing ?? EmptyState());
            var utcNow = timeProvider.GetUtcNowWithoutMicroseconds();
            state = state with
            {
                Id = OrganisationObligationRequestPacingState.StateId,
                UpdatedAt = utcNow,
                Version = (existing?.Version ?? 0) + 1,
            };

            if (existing is null)
            {
                try
                {
                    await _states.InsertOneAsync(state, cancellationToken: cancellationToken);

                    return state;
                }
                catch (MongoWriteException exception)
                    when (exception.WriteError is { Category: ServerErrorCategory.DuplicateKey })
                {
                    continue;
                }
            }

            var versionFilter = Builders<OrganisationObligationRequestPacingState>.Filter.Eq(
                x => x.Version,
                existing.Version
            );
            if (existing.Version == 0)
            {
                versionFilter |= Builders<OrganisationObligationRequestPacingState>.Filter.Exists(
                    x => x.Version,
                    false
                );
            }

            var result = await _states.ReplaceOneAsync(
                Builders<OrganisationObligationRequestPacingState>.Filter.And(
                    Builders<OrganisationObligationRequestPacingState>.Filter.Eq(
                        x => x.Id,
                        OrganisationObligationRequestPacingState.StateId
                    ),
                    versionFilter
                ),
                state,
                cancellationToken: cancellationToken
            );
            if (result.ModifiedCount == 1)
            {
                return state;
            }
        }
    }

    private static OrganisationObligationRequestPacingState EmptyState() =>
        new()
        {
            Id = OrganisationObligationRequestPacingState.StateId,
            DesiredRequestsPerMinute = 0,
            EffectiveRequestsPerMinute = 0,
            RateAdjustment = 1,
            IsUnderPressure = false,
            RecentDownstreamFailurePercentage = 0,
            Version = 0,
            UpdatedAt = DateTime.UnixEpoch,
        };
}
