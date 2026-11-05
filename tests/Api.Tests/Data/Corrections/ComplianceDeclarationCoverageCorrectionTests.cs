using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Corrections;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Data.Corrections;

public class ComplianceDeclarationCoverageCorrectionTests
{
    [Fact]
    public async Task Apply_WhenBatchReadStalls_ShouldReportTimeoutAndCancelBeforeAnyUpdate()
    {
        var collection = Substitute.For<IMongoCollection<ComplianceDeclaration>>();
        collection.DocumentSerializer.Returns(Substitute.For<IBsonSerializer<ComplianceDeclaration>>());
        collection.Settings.Returns(new MongoCollectionSettings());
        collection.WithReadPreference(ReadPreference.Primary).Returns(collection);
        collection
            .FindAsync(
                Arg.Any<FilterDefinition<ComplianceDeclaration>>(),
                Arg.Any<FindOptions<ComplianceDeclaration, ComplianceDeclaration>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(async x =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, x.Arg<CancellationToken>());

                return Substitute.For<IAsyncCursor<ComplianceDeclaration>>();
            });
        var context = Substitute.For<IDbContext>();
        context.ComplianceDeclarations.Returns(collection);
        var declarations = Substitute.For<IComplianceDeclarationService>();
        var logger = new RecordingLogger<ComplianceDeclarationCoverageCorrection>();
        var subject = new ComplianceDeclarationCoverageCorrection(
            context,
            declarations,
            Options.Create(new ApplicationDataCorrectionOptions { OperationTimeoutSeconds = 1 }),
            logger
        );

        var action = () => subject.Apply(TestContext.Current.CancellationToken);
        await action.Should().ThrowAsync<TimeoutException>();

        logger.Messages.Should().Contain(x => x.Contains("batch read stalled"));
        collection.Received(1).WithReadPreference(ReadPreference.Primary);
        declarations.ReceivedCalls().Should().BeEmpty();
    }
}
