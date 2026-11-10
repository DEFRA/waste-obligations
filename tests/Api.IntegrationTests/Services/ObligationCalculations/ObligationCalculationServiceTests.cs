using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;

namespace Defra.WasteObligations.Api.IntegrationTests.Services.ObligationCalculations;

public class ObligationCalculationServiceTests : IntegrationTestBase
{
    [Fact]
    public async Task Save_ShouldPersistWholeCalculationAndKeepEquivalentDataUnchanged()
    {
        var input = Calculation();
        var subject = Subject();

        var saved = await subject.Save(input, TestContext.Current.CancellationToken);
        var reordered = input with
        {
            CalculatedOn = input.CalculatedOn.AddDays(1),
            Producers = input
                .Producers.Reverse()
                .Select(x => x with { Materials = x.Materials.Reverse().ToArray() })
                .ToArray(),
        };
        var equivalent = await subject.Save(reordered, TestContext.Current.CancellationToken);
        var stored = await Current.Find(x => x.Id == saved.Id).SingleAsync(TestContext.Current.CancellationToken);

        saved.Id.Should().NotBe(ObjectId.Empty);
        stored.Should().BeEquivalentTo(input, options => options.Excluding(x => x.Id));
        stored.Version.Should().Be(1);
        stored.SchemaVersion.Should().Be("v1.0");
        equivalent.Should().BeEquivalentTo(saved);
        (
            await History.CountDocumentsAsync(
                FilterDefinition<ObligationCalculationHistory>.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(0);
        var bson = await RawCurrent
            .Find(new BsonDocument("_id", saved.Id))
            .SingleAsync(TestContext.Current.CancellationToken);
        bson["submitterOrganisationId"].AsBsonBinaryData.SubType.Should().Be(BsonBinarySubType.UuidStandard);
        bson["submitterType"].AsString.Should().Be("DirectRegistrant");
        bson["producers"]
            [0]["materials"]
            .AsBsonArray.Should()
            .AllSatisfy(x =>
            {
                x["inputKilograms"].BsonType.Should().Be(BsonType.Int64);
                x["inputTonnes"].BsonType.Should().Be(BsonType.Int64);
                x["obligationTonnes"].BsonType.Should().Be(BsonType.Int64);
            });
    }

    [Fact]
    public async Task Save_ShouldArchiveExactPreviousPayloadAndOnlySupersededVersions()
    {
        var subject = Subject();
        var first = await subject.Save(Calculation(), TestContext.Current.CancellationToken);
        await RawCurrent.UpdateOneAsync(
            new BsonDocument("_id", first.Id),
            new BsonDocument(
                "$set",
                new BsonDocument { { "futureField", new BsonDocument("nested", 42) }, { "schemaVersion", "v0.9" } }
            ),
            cancellationToken: TestContext.Current.CancellationToken
        );
        var original = await RawCurrent
            .Find(new BsonDocument("_id", first.Id))
            .SingleAsync(TestContext.Current.CancellationToken);
        var changed = ChangeKilograms(first);

        var second = await subject.Save(changed, TestContext.Current.CancellationToken);
        var third = await subject.Save(ChangeKilograms(second), TestContext.Current.CancellationToken);
        await subject.Save(
            third with
            {
                CalculatedOn = third.CalculatedOn.AddDays(1),
            },
            TestContext.Current.CancellationToken
        );
        var history = await History
            .Find(x => x.CalculationId == first.Id)
            .SortBy(x => x.Version)
            .ToListAsync(TestContext.Current.CancellationToken);

        third.Id.Should().Be(first.Id);
        third.Version.Should().Be(3);
        history.Select(x => x.Version).Should().Equal(1, 2);
        history[0].Calculation.Equals(original).Should().BeTrue();
        history[1].Calculation.Equals(second.ToBsonDocument()).Should().BeTrue();
        second.Producers[0].Materials[0].InputTonnes.Should().Be(first.Producers[0].Materials[0].InputTonnes);
        second.Producers[0].Materials[0].ObligationTonnes.Should().Be(first.Producers[0].Materials[0].ObligationTonnes);
    }

    [Theory]
    [InlineData("submitterType")]
    [InlineData("producerId")]
    [InlineData("producerMembership")]
    [InlineData("materialCode")]
    [InlineData("materialMembership")]
    [InlineData("inputTonnes")]
    [InlineData("obligationTonnes")]
    public async Task Save_ShouldVersionEachComparedDataChange(string change)
    {
        var subject = Subject();
        var first = await subject.Save(Calculation(), TestContext.Current.CancellationToken);
        var producer = first.Producers[0];
        var material = producer.Materials[0];
        var changed = change switch
        {
            "submitterType" => first with { SubmitterType = ObligationSubmitterType.ComplianceScheme },
            "producerId" => first with
            {
                Producers = [producer with { ProducerOrganisationId = Guid.NewGuid() }, first.Producers[1]],
            },
            "producerMembership" => first with { Producers = [producer] },
            "materialCode" => first with
            {
                Producers =
                [
                    producer with
                    {
                        Materials = [material with { MaterialCode = "PC" }, .. producer.Materials.Skip(1)],
                    },
                    first.Producers[1],
                ],
            },
            "materialMembership" => first with
            {
                Producers = [producer with { Materials = [material] }, first.Producers[1]],
            },
            "inputTonnes" => first with
            {
                Producers =
                [
                    producer with
                    {
                        Materials = [material with { InputTonnes = 51 }, .. producer.Materials.Skip(1)],
                    },
                    first.Producers[1],
                ],
            },
            _ => first with
            {
                Producers =
                [
                    producer with
                    {
                        Materials = [material with { ObligationTonnes = 29 }, .. producer.Materials.Skip(1)],
                    },
                    first.Producers[1],
                ],
            },
        };

        var saved = await subject.Save(changed, TestContext.Current.CancellationToken);

        saved.Version.Should().Be(2);
        (await History.Find(x => x.CalculationId == first.Id).SingleAsync(TestContext.Current.CancellationToken))
            .Calculation.Equals(first.ToBsonDocument())
            .Should()
            .BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_WhenCompetingCallsReadSameVersion_ShouldSurfaceConflict(bool firstInsert)
    {
        var input = Calculation();
        if (!firstInsert)
            await Subject().Save(input, TestContext.Current.CancellationToken);
        var competing = firstInsert ? input : ChangeKilograms(input);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pausedContext = Context();
        var wrapper = Wrapper(pausedContext);
        wrapper
            .ExecuteTransaction(
                Arg.Any<Func<IClientSessionHandle, CancellationToken, Task<ObligationCalculation>>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(call =>
            {
                var attempts = 0;

                return pausedContext.ExecuteTransaction(
                    async (session, token) =>
                    {
                        if (attempts++ == 0)
                        {
                            reached.SetResult();
                            await release.Task.WaitAsync(token);
                            var transient = new MongoException("Injected transient transaction failure");
                            transient.AddErrorLabel("TransientTransactionError");
                            throw transient;
                        }

                        return await call.ArgAt<
                            Func<IClientSessionHandle, CancellationToken, Task<ObligationCalculation>>
                        >(0)(session, token);
                    },
                    call.ArgAt<string>(1),
                    call.ArgAt<CancellationToken>(2)
                );
            });
        var losing = new ObligationCalculationService(wrapper).Save(competing, TestContext.Current.CancellationToken);
        await reached.Task.WaitAsync(TestContext.Current.CancellationToken);
        var winner = await Subject().Save(competing, TestContext.Current.CancellationToken);
        release.SetResult();
        Func<Task> finish = async () => await losing;

        await finish.Should().ThrowAsync<ConcurrencyException>();
        var retried = await Subject().Save(competing, TestContext.Current.CancellationToken);
        retried.Should().BeEquivalentTo(winner);
        (
            await Current.CountDocumentsAsync(
                FilterDefinition<ObligationCalculation>.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(1);
        (
            await History.CountDocumentsAsync(
                FilterDefinition<ObligationCalculationHistory>.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(firstInsert ? 0 : 1);
    }

    [Fact]
    public async Task Save_WhenReplacementFailsAfterArchive_ShouldRollbackArchive()
    {
        var collectionName = $"calculation_rollback_{Guid.NewGuid():N}";
        await GetMongoDatabase()
            .CreateCollectionAsync(
                collectionName,
                new CreateCollectionOptions<ObligationCalculation>
                {
                    Validator = Builders<ObligationCalculation>.Filter.Eq(x => x.Version, 1),
                },
                TestContext.Current.CancellationToken
            );
        var collection = GetMongoApplicationDatabase().GetCollection<ObligationCalculation>(collectionName);
        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<ObligationCalculation>(
                Builders<ObligationCalculation>
                    .IndexKeys.Ascending(x => x.SubmitterOrganisationId)
                    .Ascending(x => x.ObligationYear),
                new CreateIndexOptions { Unique = true }
            ),
            cancellationToken: TestContext.Current.CancellationToken
        );
        try
        {
            var context = Context();
            var wrapper = Wrapper(context);
            wrapper.ObligationCalculations.Returns(collection);
            wrapper
                .ExecuteTransaction(
                    Arg.Any<Func<IClientSessionHandle, CancellationToken, Task<ObligationCalculation>>>(),
                    Arg.Any<string>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(call =>
                    context.ExecuteTransaction(
                        call.ArgAt<Func<IClientSessionHandle, CancellationToken, Task<ObligationCalculation>>>(0),
                        call.ArgAt<string>(1),
                        call.ArgAt<CancellationToken>(2)
                    )
                );
            var subject = new ObligationCalculationService(wrapper);
            var first = await subject.Save(Calculation(), TestContext.Current.CancellationToken);
            Func<Task> save = () => subject.Save(ChangeKilograms(first), TestContext.Current.CancellationToken);

            await save.Should().ThrowAsync<MongoWriteException>();
            (
                await GetMongoDatabase()
                    .GetCollection<ObligationCalculation>(collectionName)
                    .Find(x => x.Id == first.Id)
                    .SingleAsync(TestContext.Current.CancellationToken)
            )
                .Should()
                .BeEquivalentTo(first);
            (
                await History.CountDocumentsAsync(
                    FilterDefinition<ObligationCalculationHistory>.Empty,
                    cancellationToken: TestContext.Current.CancellationToken
                )
            )
                .Should()
                .Be(0);
        }
        finally
        {
            await GetMongoDatabase().DropCollectionAsync(collectionName, TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_WhenTransactionFailsOrIsCancelledBeforeCommit_ShouldRollback(bool cancelled)
    {
        var first = await Subject().Save(Calculation(), TestContext.Current.CancellationToken);
        var context = Context();
        var wrapper = Wrapper(context);
        wrapper
            .ExecuteTransaction(
                Arg.Any<Func<IClientSessionHandle, CancellationToken, Task<ObligationCalculation>>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(call =>
                context.ExecuteTransaction<ObligationCalculation>(
                    async (session, token) =>
                    {
                        await call.ArgAt<Func<IClientSessionHandle, CancellationToken, Task<ObligationCalculation>>>(0)(
                            session,
                            token
                        );
                        if (cancelled)
                            throw new OperationCanceledException(new CancellationToken(true));
                        throw new InvalidOperationException("Injected failure before commit");
                    },
                    call.ArgAt<string>(1),
                    call.ArgAt<CancellationToken>(2)
                )
            );
        Func<Task> save = () =>
            new ObligationCalculationService(wrapper).Save(
                ChangeKilograms(first),
                TestContext.Current.CancellationToken
            );

        if (cancelled)
            await save.Should().ThrowAsync<OperationCanceledException>();
        else
            await save.Should().ThrowAsync<InvalidOperationException>();
        (await Current.Find(x => x.Id == first.Id).SingleAsync(TestContext.Current.CancellationToken))
            .Should()
            .BeEquivalentTo(first);
        (
            await History.CountDocumentsAsync(
                FilterDefinition<ObligationCalculationHistory>.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(0);
    }

    [Fact]
    public async Task Save_WhenUnrelatedHistoryUniquenessFails_ShouldPreserveMongoFailureAndCurrent()
    {
        var first = await Subject().Save(Calculation(), TestContext.Current.CancellationToken);
        await History.InsertOneAsync(
            new ObligationCalculationHistory
            {
                CalculationId = first.Id,
                Version = first.Version,
                Calculation = first.ToBsonDocument(),
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        Func<Task> save = () => Subject().Save(ChangeKilograms(first), TestContext.Current.CancellationToken);

        await save.Should().ThrowAsync<MongoWriteException>();
        (await Current.Find(x => x.Id == first.Id).SingleAsync(TestContext.Current.CancellationToken))
            .Should()
            .BeEquivalentTo(first);
    }

    [Fact]
    public async Task Indexes_ShouldSupportCurrentAndHistoryReadShapes()
    {
        var first = await Subject().Save(Calculation(), TestContext.Current.CancellationToken);
        await Subject().Save(ChangeKilograms(first), TestContext.Current.CancellationToken);
        var application = Context();
        var current = application.ObligationCalculations;
        (
            await current
                .Find(x => x.SubmitterOrganisationId == first.SubmitterOrganisationId)
                .ToListAsync(TestContext.Current.CancellationToken)
        )
            .Should()
            .ContainSingle();
        var filter =
            Builders<ObligationCalculation>.Filter.In(
                x => x.SubmitterOrganisationId,
                new[] { first.SubmitterOrganisationId, Guid.NewGuid() }
            ) & Builders<ObligationCalculation>.Filter.Eq(x => x.ObligationYear, first.ObligationYear);
        (await current.Find(filter).ToListAsync(TestContext.Current.CancellationToken)).Should().ContainSingle();
        (
            await current
                .Find(
                    Builders<ObligationCalculation>.Filter.ElemMatch(
                        x => x.Producers,
                        x => x.ProducerOrganisationId == first.Producers[0].ProducerOrganisationId
                    )
                )
                .ToListAsync(TestContext.Current.CancellationToken)
        )
            .Should()
            .ContainSingle();
        (await current.Find(x => x.Id == first.Id).SingleAsync(TestContext.Current.CancellationToken))
            .Version.Should()
            .Be(2);
        (
            await application
                .ObligationCalculationHistory.Find(x => x.CalculationId == first.Id && x.Version == 1)
                .SingleAsync(TestContext.Current.CancellationToken)
        )
            .Calculation.Equals(first.ToBsonDocument())
            .Should()
            .BeTrue();
        (
            await application
                .ObligationCalculationHistory.Find(x => x.CalculationId == first.Id)
                .SortBy(x => x.Version)
                .ToListAsync(TestContext.Current.CancellationToken)
        )
            .Should()
            .ContainSingle();
    }

    private static IMongoCollection<ObligationCalculation> Current =>
        GetMongoDatabase().GetCollection<ObligationCalculation>(nameof(ObligationCalculation));
    private static IMongoCollection<BsonDocument> RawCurrent =>
        GetMongoDatabase().GetCollection<BsonDocument>(nameof(ObligationCalculation));
    private static IMongoCollection<ObligationCalculationHistory> History =>
        GetMongoDatabase().GetCollection<ObligationCalculationHistory>(nameof(ObligationCalculationHistory));

    private static ObligationCalculationService Subject() => new(Context());

    private static MongoDbContext Context() =>
        new(
            GetMongoApplicationDatabase(),
            Options.Create(new MongoDbOptions()),
            Substitute.For<ILogger<MongoDbContext>>()
        );

    private static IDbContext Wrapper(MongoDbContext context)
    {
        var wrapper = Substitute.For<IDbContext>();
        wrapper.ObligationCalculations.Returns(context.ObligationCalculations);
        wrapper.ObligationCalculationHistory.Returns(context.ObligationCalculationHistory);

        return wrapper;
    }

    private static ObligationCalculation ChangeKilograms(ObligationCalculation value) =>
        value with
        {
            CalculatedOn = value.CalculatedOn.AddMinutes(1),
            Producers =
            [
                value.Producers[0] with
                {
                    Materials =
                    [
                        value.Producers[0].Materials[0] with
                        {
                            InputKilograms = value.Producers[0].Materials[0].InputKilograms + 100,
                        },
                        .. value.Producers[0].Materials.Skip(1),
                    ],
                },
                value.Producers[1],
            ],
        };

    private static ObligationCalculation Calculation() =>
        new()
        {
            SubmitterOrganisationId = Guid.NewGuid(),
            SubmitterType = ObligationSubmitterType.DirectRegistrant,
            ObligationYear = 2025,
            CalculatedOn = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            Producers =
            [
                new()
                {
                    ProducerOrganisationId = Guid.NewGuid(),
                    Materials =
                    [
                        new()
                        {
                            MaterialCode = "PL",
                            InputKilograms = 50_100,
                            InputTonnes = 50,
                            ObligationTonnes = 28,
                        },
                        new()
                        {
                            MaterialCode = "GL",
                            InputKilograms = 3_000,
                            InputTonnes = 3,
                            ObligationTonnes = 1,
                        },
                        new()
                        {
                            MaterialCode = "GR",
                            InputKilograms = 3_000,
                            InputTonnes = 3,
                            ObligationTonnes = 2,
                        },
                    ],
                },
                new()
                {
                    ProducerOrganisationId = Guid.NewGuid(),
                    Materials =
                    [
                        new()
                        {
                            MaterialCode = "PL",
                            InputKilograms = 50_100,
                            InputTonnes = 50,
                            ObligationTonnes = 28,
                        },
                    ],
                },
            ],
        };
}
