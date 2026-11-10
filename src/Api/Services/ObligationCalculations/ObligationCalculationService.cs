using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Data.Migrations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public class ObligationCalculationService(IDbContext dbContext) : IObligationCalculationService
{
    private const string VersionField = "version";

    public async Task<ObligationCalculation> Save(
        ObligationCalculation calculation,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(calculation);
        var currentCollection = dbContext.ObligationCalculations.Database.GetCollection<BsonDocument>(
            dbContext.ObligationCalculations.CollectionNamespace.CollectionName,
            dbContext.ObligationCalculations.Settings
        );
        var identity =
            Builders<ObligationCalculation>.Filter.Eq(
                x => x.SubmitterOrganisationId,
                calculation.SubmitterOrganisationId
            ) & Builders<ObligationCalculation>.Filter.Eq(x => x.ObligationYear, calculation.ObligationYear);
        var renderedIdentity = identity.Render(
            new RenderArgs<ObligationCalculation>(
                BsonSerializer.LookupSerializer<ObligationCalculation>(),
                BsonSerializer.SerializerRegistry
            )
        );
        // Capture the payload and expected version once, outside the transaction retry boundary.
        var previous = await currentCollection.Find(renderedIdentity).SingleOrDefaultAsync(cancellationToken);
        if (
            previous is not null
            && Equivalent(BsonSerializer.Deserialize<ObligationCalculation>(previous), calculation)
        )
            return BsonSerializer.Deserialize<ObligationCalculation>(previous);

        var replacement = new ObligationCalculation
        {
            Id = previous is null ? ObjectId.GenerateNewId() : previous["_id"].AsObjectId,
            Version = previous is null ? 1 : checked(previous[VersionField].AsInt32 + 1),
            SubmitterOrganisationId = calculation.SubmitterOrganisationId,
            SubmitterType = calculation.SubmitterType,
            ObligationYear = calculation.ObligationYear,
            CalculatedOn = calculation.CalculatedOn,
            Producers = calculation.Producers,
        };
        try
        {
            return await dbContext.ExecuteTransaction(
                async (session, token) =>
                {
                    if (previous is null)
                    {
                        await dbContext.ObligationCalculations.InsertOneAsync(
                            session,
                            replacement,
                            cancellationToken: token
                        );
                    }
                    else
                    {
                        var expected = new BsonDocument
                        {
                            { "_id", previous["_id"] },
                            { VersionField, previous[VersionField] },
                        };
                        var stillCurrent = await currentCollection.Find(session, expected).AnyAsync(token);
                        if (!stillCurrent)
                            throw new ConcurrencyException(
                                "The obligation calculation has changed. Reload and try again."
                            );

                        var history = dbContext.ObligationCalculationHistory;
                        var rawHistory = history.Database.GetCollection<BsonDocument>(
                            history.CollectionNamespace.CollectionName,
                            history.Settings
                        );
                        await rawHistory.InsertOneAsync(
                            session,
                            new BsonDocument { { "_id", ObjectId.GenerateNewId() }, { "calculation", previous } },
                            cancellationToken: token
                        );
                        var result = await currentCollection.ReplaceOneAsync(
                            session,
                            expected,
                            replacement.ToBsonDocument(),
                            cancellationToken: token
                        );
                        if (result.MatchedCount != 1)
                            throw new ConcurrencyException(
                                "The obligation calculation has changed. Reload and try again."
                            );
                    }

                    return replacement;
                },
                "Save obligation calculation",
                cancellationToken
            );
        }
        catch (MongoWriteException exception) when (previous is null && IsIdentityCollision(exception.WriteError))
        {
            throw new ConcurrencyException(
                "An obligation calculation already exists for this organisation and year. Reload and try again."
            );
        }
    }

    private static bool IsIdentityCollision(WriteError error) =>
        error.Category == ServerErrorCategory.DuplicateKey
        && (
            (
                error.Details?.GetValue("keyPattern", BsonNull.Value) is BsonDocument pattern
                && pattern.Equals(new BsonDocument { { "submitterOrganisationId", 1 }, { "obligationYear", 1 } })
            )
            || error.Message.Contains(
                $" index: {ObligationCalculationIndexes.IdentityIndexName} dup key:",
                StringComparison.Ordinal
            )
        );

    private static bool Equivalent(ObligationCalculation current, ObligationCalculation incoming) =>
        current.SubmitterType == incoming.SubmitterType
        && current.Producers.Count == incoming.Producers.Count
        && current
            .Producers.OrderBy(x => x.ProducerOrganisationId)
            .Zip(incoming.Producers.OrderBy(x => x.ProducerOrganisationId))
            .All(pair =>
                pair.First.ProducerOrganisationId == pair.Second.ProducerOrganisationId
                && pair.First.Materials.Count == pair.Second.Materials.Count
                && pair.First.Materials.OrderBy(x => x.MaterialCode, StringComparer.Ordinal)
                    .SequenceEqual(pair.Second.Materials.OrderBy(x => x.MaterialCode, StringComparer.Ordinal))
            );
}
