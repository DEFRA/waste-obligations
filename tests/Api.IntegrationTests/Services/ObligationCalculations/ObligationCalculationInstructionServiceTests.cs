using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.IntegrationTests.Services.ObligationCalculations;

public class ObligationCalculationInstructionServiceTests : IntegrationTestBase
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 10, 30, 1, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Create_ShouldPersistPendingInstructionForExplicitYear()
    {
        var subject = CreateSubject();

        var instruction = await subject.Create(2025, null, TestContext.Current.CancellationToken);

        instruction.Id.Should().NotBe(ObjectId.Empty);
        instruction.ObligationYear.Should().Be(2025);
        instruction.SubmitterId.Should().BeNull();
        instruction.Status.Should().Be(ObligationCalculationInstructionStatus.Pending);
        instruction.CreatedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        var stored = await GetMongoDatabase()
            .GetCollection<BsonDocument>(ObligationCalculationInstruction.CollectionName)
            .Find(new BsonDocument("_id", instruction.Id))
            .SingleAsync(TestContext.Current.CancellationToken);
        stored
            .Should()
            .BeEquivalentTo(
                new BsonDocument
                {
                    { "_id", instruction.Id },
                    { "obligationYear", 2025 },
                    { "submitterId", BsonNull.Value },
                    { "status", "Pending" },
                    { "createdAt", _timeProvider.GetUtcNow().UtcDateTime },
                }
            );
    }

    [Theory]
    [InlineData(ObligationCalculationInstructionStatus.Pending)]
    [InlineData(ObligationCalculationInstructionStatus.Running)]
    public async Task Create_WhenInstructionIsActive_ShouldReturnItUnchanged(
        ObligationCalculationInstructionStatus status
    )
    {
        var existing = ObligationCalculationInstructionFixture
            .Default()
            .With(x => x.Status, status)
            .With(x => x.SubmitterId, Guid.NewGuid())
            .Create();
        await ObligationCalculationInstructions.InsertOneAsync(
            existing,
            cancellationToken: TestContext.Current.CancellationToken
        );
        _timeProvider.Advance(TimeSpan.FromDays(1));

        var result = await CreateSubject()
            .Create(existing.ObligationYear, existing.SubmitterId, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(existing);
        (
            await ObligationCalculationInstructions.CountDocumentsAsync(
                FilterDefinition<ObligationCalculationInstruction>.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_WhenHostsRace_ShouldReturnOneInstruction(bool submitterSpecific)
    {
        Guid? submitterId = submitterSpecific ? Guid.NewGuid() : null;

        var results = await Task.WhenAll(
            Enumerable
                .Range(0, 20)
                .Select(_ => CreateSubject().Create(2026, submitterId, TestContext.Current.CancellationToken))
        );

        results.Select(x => x.Id).Distinct().Should().ContainSingle();
        results
            .Should()
            .AllSatisfy(x =>
            {
                x.ObligationYear.Should().Be(2026);
                x.SubmitterId.Should().Be(submitterId);
                x.Status.Should().Be(ObligationCalculationInstructionStatus.Pending);
            });
        (
            await ObligationCalculationInstructions.CountDocumentsAsync(
                FilterDefinition<ObligationCalculationInstruction>.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task Create_ShouldAllowWholeYearDifferentSubmittersAndDifferentYearsIndependently()
    {
        var subject = CreateSubject();
        var submitterId = Guid.NewGuid();

        var wholeYear = await subject.Create(2026, null, TestContext.Current.CancellationToken);
        var submitter = await subject.Create(2026, submitterId, TestContext.Current.CancellationToken);
        var otherSubmitter = await subject.Create(2026, Guid.NewGuid(), TestContext.Current.CancellationToken);
        var otherYear = await subject.Create(2025, submitterId, TestContext.Current.CancellationToken);
        var duplicate = await subject.Create(2026, null, TestContext.Current.CancellationToken);

        new[] { wholeYear.Id, submitter.Id, otherSubmitter.Id, otherYear.Id }.Should().OnlyHaveUniqueItems();
        duplicate.Should().BeEquivalentTo(wholeYear);
        (
            await ObligationCalculationInstructions.CountDocumentsAsync(
                FilterDefinition<ObligationCalculationInstruction>.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(4);
    }

    [Theory]
    [InlineData(ObligationCalculationInstructionStatus.Completed)]
    [InlineData(ObligationCalculationInstructionStatus.Failed)]
    public async Task Create_WhenPreviousInstructionIsTerminal_ShouldCreateNewInstruction(
        ObligationCalculationInstructionStatus status
    )
    {
        var previous = ObligationCalculationInstructionFixture.Default().With(x => x.Status, status).Create();
        await ObligationCalculationInstructions.InsertOneAsync(
            previous,
            cancellationToken: TestContext.Current.CancellationToken
        );

        var result = await CreateSubject()
            .Create(previous.ObligationYear, previous.SubmitterId, TestContext.Current.CancellationToken);

        result.Id.Should().NotBe(previous.Id);
        result.Status.Should().Be(ObligationCalculationInstructionStatus.Pending);
        (
            await ObligationCalculationInstructions
                .Find(x => x.Id == previous.Id)
                .SingleAsync(TestContext.Current.CancellationToken)
        )
            .Should()
            .BeEquivalentTo(previous);
        (
            await ObligationCalculationInstructions.CountDocumentsAsync(
                FilterDefinition<ObligationCalculationInstruction>.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(2);
    }

    private ObligationCalculationInstructionService CreateSubject() =>
        new(GetMongoApplicationDatabase(), _timeProvider);
}
