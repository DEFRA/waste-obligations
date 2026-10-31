using System.Net;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Services.ObligationCalculations;

public class ObligationCalculationInstructionServiceTests
{
    [Fact]
    public async Task Create_WhenDuplicateCollisionOccurs_ShouldRetryAndReturnExistingInstruction()
    {
        var collection = Substitute.For<IMongoCollection<ObligationCalculationInstruction>>();
        var existing = ObligationCalculationInstructionFixture.Default().Create();
        collection
            .FindOneAndUpdateAsync(
                Arg.Any<FilterDefinition<ObligationCalculationInstruction>>(),
                Arg.Any<UpdateDefinition<ObligationCalculationInstruction>>(),
                Arg.Any<FindOneAndUpdateOptions<ObligationCalculationInstruction, ObligationCalculationInstruction>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                _ => Task.FromException<ObligationCalculationInstruction>(CommandFailure(11000)),
                _ => Task.FromResult(existing)
            );

        var result = await CreateSubject(collection)
            .Create(existing.ObligationYear, existing.SubmitterId, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(existing);
        await collection
            .Received(2)
            .FindOneAndUpdateAsync(
                Arg.Any<FilterDefinition<ObligationCalculationInstruction>>(),
                Arg.Any<UpdateDefinition<ObligationCalculationInstruction>>(),
                Arg.Any<FindOneAndUpdateOptions<ObligationCalculationInstruction, ObligationCalculationInstruction>>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Theory]
    [InlineData(11000, 5)]
    [InlineData(12345, 1)]
    public async Task Create_WhenCommandKeepsFailing_ShouldPropagateAfterApplicableAttemptLimit(
        int code,
        int expectedAttempts
    )
    {
        var collection = Substitute.For<IMongoCollection<ObligationCalculationInstruction>>();
        var failure = CommandFailure(code);
        collection
            .FindOneAndUpdateAsync(
                Arg.Any<FilterDefinition<ObligationCalculationInstruction>>(),
                Arg.Any<UpdateDefinition<ObligationCalculationInstruction>>(),
                Arg.Any<FindOneAndUpdateOptions<ObligationCalculationInstruction, ObligationCalculationInstruction>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.FromException<ObligationCalculationInstruction>(failure));

        var action = () => CreateSubject(collection).Create(2026, null, TestContext.Current.CancellationToken);

        (await action.Should().ThrowAsync<MongoCommandException>()).Which.Should().BeSameAs(failure);
        await collection
            .Received(expectedAttempts)
            .FindOneAndUpdateAsync(
                Arg.Any<FilterDefinition<ObligationCalculationInstruction>>(),
                Arg.Any<UpdateDefinition<ObligationCalculationInstruction>>(),
                Arg.Any<FindOneAndUpdateOptions<ObligationCalculationInstruction, ObligationCalculationInstruction>>(),
                Arg.Any<CancellationToken>()
            );
    }

    private static ObligationCalculationInstructionService CreateSubject(
        IMongoCollection<ObligationCalculationInstruction> collection
    )
    {
        var database = Substitute.For<IMongoDatabase>();
        database
            .GetCollection<ObligationCalculationInstruction>(ObligationCalculationInstruction.CollectionName)
            .Returns(collection);

        return new ObligationCalculationInstructionService(database, TimeProvider.System);
    }

    private static MongoCommandException CommandFailure(int code) =>
        new(
            new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017))),
            "Command failed",
            new BsonDocument("findAndModify", ObligationCalculationInstruction.CollectionName),
            new BsonDocument("code", code)
        );
}
