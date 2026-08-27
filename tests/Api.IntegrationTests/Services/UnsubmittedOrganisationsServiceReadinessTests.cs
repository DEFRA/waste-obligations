using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Defra.WasteObligations.Api.IntegrationTests.Services;

public class UnsubmittedOrganisationsServiceReadinessTests : IntegrationTestBase
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Search_WhenDeclarationReviewStateBackfillIsIncomplete_ShouldReturnAnEmptyPageAndLogAnError()
    {
        await OrganisationEligibilitySnapshots.InsertOneAsync(
            new OrganisationEligibilitySnapshot
            {
                Id = OrganisationEligibilitySnapshot.SnapshotId,
                ActiveGeneration = "generation",
                ActiveContentFingerprint = "fingerprint",
                ActiveRowCount = 1,
                ActiveGenerationPromotedAt = _timeProvider.GetUtcNow().UtcDateTime,
                LastVerifiedAt = _timeProvider.GetUtcNow().UtcDateTime,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var logger = new RecordingLogger<UnsubmittedOrganisationsService>();
        var subject = CreateSubject(logger);

        var result = await subject.Search(
            2026,
            RegistrationType.DirectProducer,
            null,
            [],
            page: 1,
            pageSize: 20,
            TestContext.Current.CancellationToken
        );

        result.Rows.Should().BeEmpty();
        result.Total.Should().Be(0);
        logger
            .Entries.Should()
            .ContainSingle(x =>
                x.Level == LogLevel.Error
                && x.Message == "Unsubmitted organisation query has no completed declaration review state backfill"
            );
    }

    private UnsubmittedOrganisationsService CreateSubject(ILogger<UnsubmittedOrganisationsService> logger) =>
        new(
            new MongoDbContext(
                GetMongoDatabase(),
                Options.Create(new MongoDbOptions()),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<MongoDbContext>.Instance
            ),
            Options.Create(new OrganisationEligibilityOptions { MaximumAllowedStaleness = TimeSpan.FromHours(2) }),
            Options.Create(new OrganisationObligationHydrationOptions()),
            _timeProvider,
            logger
        );
}
