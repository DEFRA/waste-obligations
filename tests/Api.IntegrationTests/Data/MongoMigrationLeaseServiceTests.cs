using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.IntegrationTests.Data;

public class MongoMigrationLeaseServiceTests : IntegrationTestBase
{
    private const string LeaseCollectionName = "_migrations_lease";
    private const string LeaseId = "mongo-migrations";

    [Fact]
    public async Task TryAcquire_WhenLeaseIsUnexpired_ShouldOnlyAllowOneOwner()
    {
        await ResetLease();
        var timeProvider = CreateTimeProvider();
        var firstInstance = CreateSubject(timeProvider);
        var secondInstance = CreateSubject(timeProvider);

        var firstResult = await firstInstance.TryAcquire(
            TimeSpan.FromSeconds(60),
            TestContext.Current.CancellationToken
        );
        var secondResult = await secondInstance.TryAcquire(
            TimeSpan.FromSeconds(60),
            TestContext.Current.CancellationToken
        );

        firstResult.Should().BeTrue();
        secondResult.Should().BeFalse();
        var lease = await GetLease();
        lease.Owner.Should().NotBeNullOrWhiteSpace();
        lease.ExpiresAt.Should().Be(timeProvider.GetUtcNow().AddSeconds(60).UtcDateTime);
    }

    [Fact]
    public async Task TryRenew_ShouldOnlyExtendLeaseForOwner()
    {
        await ResetLease();
        var timeProvider = CreateTimeProvider();
        var owner = CreateSubject(timeProvider);
        var nonOwner = CreateSubject(timeProvider);
        await owner.TryAcquire(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromSeconds(10));

        var nonOwnerResult = await nonOwner.TryRenew(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        var ownerResult = await owner.TryRenew(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

        nonOwnerResult.Should().BeFalse();
        ownerResult.Should().BeTrue();
        var lease = await GetLease();
        lease.ExpiresAt.Should().Be(timeProvider.GetUtcNow().AddSeconds(60).UtcDateTime);
    }

    [Fact]
    public async Task Release_ShouldAllowAnotherInstanceToAcquire()
    {
        await ResetLease();
        var timeProvider = CreateTimeProvider();
        var owner = CreateSubject(timeProvider);
        var nextOwner = CreateSubject(timeProvider);
        await owner.TryAcquire(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

        await owner.Release(TestContext.Current.CancellationToken);
        var result = await nextOwner.TryAcquire(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

        result.Should().BeTrue();
    }

    private static FakeTimeProvider CreateTimeProvider() =>
        new(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));

    private static MongoMigrationLeaseService CreateSubject(TimeProvider timeProvider) =>
        new(GetMongoApplicationDatabase(), timeProvider);

    private static async Task<MongoMigrationLease> GetLease() =>
        await GetMongoDatabase()
            .GetCollection<MongoMigrationLease>(LeaseCollectionName)
            .Find(x => x.Id == LeaseId)
            .SingleAsync(TestContext.Current.CancellationToken);

    private static Task ResetLease() =>
        GetMongoDatabase().DropCollectionAsync(LeaseCollectionName, TestContext.Current.CancellationToken);
}
