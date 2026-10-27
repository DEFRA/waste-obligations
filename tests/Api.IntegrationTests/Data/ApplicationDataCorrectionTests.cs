using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Corrections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Driver;
using NSubstitute;

namespace Defra.WasteObligations.Api.IntegrationTests.Data;

public class ApplicationDataCorrectionTests : IntegrationTestBase
{
    private const string CheckpointsCollectionName = "_application_data_corrections";
    private static readonly DateTimeOffset s_correctedAt = new(2026, 10, 26, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Run_ShouldUseExplicitVersionsAndSkipCompletedCorrections()
    {
        var database = GetMongoApplicationDatabase();
        var history = GetMongoDatabase().GetCollection<ApplicationDataCorrectionCompletion>(CheckpointsCollectionName);
        const string firstId = "ordering-first";
        const string secondId = "ordering-second";
        await history.DeleteManyAsync(
            Builders<ApplicationDataCorrectionCompletion>.Filter.In(x => x.Id, new[] { firstId, secondId }),
            TestContext.Current.CancellationToken
        );
        var order = new List<int>();
        var first = Substitute.For<IApplicationDataCorrection>();
        first.Version.Returns(1);
        first.Id.Returns(firstId);
        first
            .Apply(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add(1);
                return Task.CompletedTask;
            });
        var second = Substitute.For<IApplicationDataCorrection>();
        second.Version.Returns(2);
        second.Id.Returns(secondId);
        second
            .Apply(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add(2);
                return Task.CompletedTask;
            });
        var runner = new ApplicationDataCorrectionRunner(
            database,
            [second, first],
            new FakeTimeProvider(s_correctedAt),
            Substitute.For<ILogger<ApplicationDataCorrectionRunner>>(),
            Options.Create(new ApplicationDataCorrectionOptions())
        );

        await runner.Run(TestContext.Current.CancellationToken);
        await runner.Run(TestContext.Current.CancellationToken);

        order.Should().Equal(1, 2);
    }

    [Fact]
    public async Task Lease_ShouldExcludeOtherOwnersAndRemainIndependentOfSchemaMigrations()
    {
        await GetMongoDatabase()
            .DropCollectionAsync("_application_data_correction_lease", TestContext.Current.CancellationToken);
        var first = new ApplicationDataCorrectionLeaseService(GetMongoApplicationDatabase(), TimeProvider.System);
        var second = new ApplicationDataCorrectionLeaseService(GetMongoApplicationDatabase(), TimeProvider.System);
        var migration = new MongoMigrationLeaseService(GetMongoDatabase(), TimeProvider.System);
        await GetMongoDatabase().DropCollectionAsync("_migrations_lease", TestContext.Current.CancellationToken);
        var duration = TimeSpan.FromMinutes(1);

        (await first.TryAcquire(duration, TestContext.Current.CancellationToken)).Should().BeTrue();
        (await second.TryAcquire(duration, TestContext.Current.CancellationToken)).Should().BeFalse();
        (await second.TryRenew(duration, TestContext.Current.CancellationToken)).Should().BeFalse();
        await second.Release(TestContext.Current.CancellationToken);
        (await first.TryRenew(duration, TestContext.Current.CancellationToken)).Should().BeTrue();
        (await migration.TryAcquire(duration, TestContext.Current.CancellationToken)).Should().BeTrue();
        await first.Release(TestContext.Current.CancellationToken);
        (await second.TryAcquire(duration, TestContext.Current.CancellationToken)).Should().BeTrue();
        await second.Release(TestContext.Current.CancellationToken);
        await migration.Release(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Run_ShouldUsePrimaryCollectionForCompletionCoordination()
    {
        const string correctionId = "primary-completion-test";
        var applicationDatabase = GetMongoApplicationDatabase();
        await GetMongoDatabase()
            .GetCollection<ApplicationDataCorrectionCompletion>(CheckpointsCollectionName)
            .DeleteOneAsync(x => x.Id == correctionId, TestContext.Current.CancellationToken);
        var collection = Substitute.For<IMongoCollection<ApplicationDataCorrectionCompletion>>();
        collection
            .WithReadPreference(ReadPreference.Primary)
            .Returns(
                applicationDatabase
                    .GetCollection<ApplicationDataCorrectionCompletion>(CheckpointsCollectionName)
                    .WithReadPreference(ReadPreference.Primary)
            );
        var database = Substitute.For<IMongoDatabase>();
        database.GetCollection<ApplicationDataCorrectionCompletion>(CheckpointsCollectionName).Returns(collection);
        var correction = Substitute.For<IApplicationDataCorrection>();
        correction.Id.Returns(correctionId);
        correction.Version.Returns(1);
        var runner = new ApplicationDataCorrectionRunner(
            database,
            [correction],
            TimeProvider.System,
            Substitute.For<ILogger<ApplicationDataCorrectionRunner>>(),
            Options.Create(new ApplicationDataCorrectionOptions { OperationTimeoutSeconds = 1 })
        );

        await runner.Run(TestContext.Current.CancellationToken);
        await runner.Run(TestContext.Current.CancellationToken);

        collection.Received(2).WithReadPreference(ReadPreference.Primary);
        await correction.Received(1).Apply(Arg.Any<CancellationToken>());
        var completion = await GetMongoDatabase()
            .GetCollection<ApplicationDataCorrectionCompletion>(CheckpointsCollectionName)
            .Find(x => x.Id == correctionId)
            .SingleAsync(TestContext.Current.CancellationToken);
        completion.Version.Should().Be(1);
    }
}
