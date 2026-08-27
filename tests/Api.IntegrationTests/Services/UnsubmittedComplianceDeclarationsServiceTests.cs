using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using OrganisationEligibilityEntity = Defra.WasteObligations.Api.Data.Entities.OrganisationEligibility;

namespace Defra.WasteObligations.Api.IntegrationTests.Services;

public class UnsubmittedOrganisationsServiceTests : IntegrationTestBase
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Search_WhenReady_ShouldReturnOnlyEligibleRowsAndApplySortingAndPaging()
    {
        const string generation = "generation";
        var alpha = Guid.NewGuid();
        var beta = Guid.NewGuid();
        var submitted = Guid.NewGuid();
        await SetReadySnapshot(generation);
        await OrganisationEligibilities.InsertManyAsync(
            [
                Eligibility(alpha, generation, "Alpha Packaging", "100001"),
                Eligibility(beta, generation, "Beta Packaging", "100002"),
                Eligibility(submitted, generation, "Submitted Packaging", "100003"),
                Eligibility(Guid.NewGuid(), generation, "Cancelled Packaging", "100004") with
                {
                    RegistrationStatus = OrganisationRegistrationStatus.Cancelled,
                },
                Eligibility(Guid.NewGuid(), generation, "Unresolved Packaging", null) with
                {
                    ReferenceNumberResolutionState = OrganisationReferenceNumberResolutionState.Pending,
                },
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );
        await ComplianceDeclarationReviewStates.InsertManyAsync(
            [ReviewState(beta, 0), ReviewState(submitted, 1)],
            cancellationToken: TestContext.Current.CancellationToken
        );
        var subject = CreateSubject();

        var descending = await subject.Search(
            2026,
            RegistrationType.DirectProducer,
            [
                new ComplianceDeclarationSort
                {
                    Field = ComplianceDeclarationSortField.OrganisationName,
                    Direction = ComplianceDeclarationSortDirection.Descending,
                },
            ],
            page: 1,
            pageSize: 1,
            TestContext.Current.CancellationToken
        );
        var ascendingSecondPage = await subject.Search(
            2026,
            RegistrationType.DirectProducer,
            [],
            page: 2,
            pageSize: 1,
            TestContext.Current.CancellationToken
        );

        descending.Total.Should().Be(2);
        descending.Rows.Should().ContainSingle().Which.OrganisationId.Should().Be(beta);
        descending.Rows.Single().ReferenceNumber.Should().Be("100002");
        ascendingSecondPage.Total.Should().Be(2);
        ascendingSecondPage.Rows.Should().ContainSingle().Which.OrganisationId.Should().Be(beta);
    }

    [Fact]
    public async Task Search_WhenNoEligibleRows_ShouldReturnAnEmptyPage()
    {
        await SetReadySnapshot("generation");
        var subject = CreateSubject();

        var result = await subject.Search(
            2026,
            RegistrationType.DirectProducer,
            [],
            page: 1,
            pageSize: 20,
            TestContext.Current.CancellationToken
        );

        result.Rows.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task Search_WhenNoActiveGeneration_ShouldReturnAnEmptyPageAndLogAnError()
    {
        var logger = new RecordingLogger<UnsubmittedOrganisationsService>();
        var subject = CreateSubject(logger);

        var result = await subject.Search(
            2026,
            RegistrationType.DirectProducer,
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
                && x.Message == "Unsubmitted organisation query has no active organisation generation"
            );
    }

    [Fact]
    public async Task Search_WhenActiveGenerationIsStale_ShouldReturnItsDataAndLogAnError()
    {
        const string generation = "stale-generation";
        var organisationId = Guid.NewGuid();
        await OrganisationEligibilitySnapshots.InsertOneAsync(
            new OrganisationEligibilitySnapshot
            {
                Id = OrganisationEligibilitySnapshot.SnapshotId,
                ActiveGeneration = generation,
                ActiveContentFingerprint = "fingerprint",
                ActiveRowCount = 1,
                ActiveGenerationPromotedAt = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-3),
                LastVerifiedAt = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-3),
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        await OrganisationEligibilities.InsertOneAsync(
            Eligibility(organisationId, generation, "Alpha Packaging", "100001"),
            cancellationToken: TestContext.Current.CancellationToken
        );
        var logger = new RecordingLogger<UnsubmittedOrganisationsService>();
        var subject = CreateSubject(logger);

        var result = await subject.Search(
            2026,
            RegistrationType.DirectProducer,
            [],
            page: 1,
            pageSize: 20,
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(1);
        result.Rows.Should().ContainSingle().Which.OrganisationId.Should().Be(organisationId);
        logger
            .Entries.Should()
            .ContainSingle(x =>
                x.Level == LogLevel.Error
                && x.Message.StartsWith(
                    "Unsubmitted organisation query is using an organisation generation last verified at"
                )
            );
    }

    private UnsubmittedOrganisationsService CreateSubject(ILogger<UnsubmittedOrganisationsService>? logger = null) =>
        new(
            new MongoDbContext(
                GetMongoDatabase(),
                Options.Create(new MongoDbOptions()),
                NullLogger<MongoDbContext>.Instance
            ),
            Options.Create(new OrganisationEligibilityOptions { MaximumAllowedStaleness = TimeSpan.FromHours(2) }),
            _timeProvider,
            logger ?? NullLogger<UnsubmittedOrganisationsService>.Instance
        );

    private async Task SetReadySnapshot(string generation)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await OrganisationEligibilitySnapshots.InsertOneAsync(
            new OrganisationEligibilitySnapshot
            {
                Id = OrganisationEligibilitySnapshot.SnapshotId,
                ActiveGeneration = generation,
                ActiveContentFingerprint = "fingerprint",
                ActiveRowCount = 1,
                ActiveGenerationPromotedAt = now,
                LastVerifiedAt = now,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        await ComplianceDeclarationReviewStateSnapshots.InsertOneAsync(
            new ComplianceDeclarationReviewStateSnapshot
            {
                Id = ComplianceDeclarationReviewStateSnapshot.SnapshotId,
                BackfillCompletedAt = now,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
    }

    private static OrganisationEligibilityEntity Eligibility(
        Guid organisationId,
        string generation,
        string name,
        string? referenceNumber
    ) =>
        new()
        {
            Generation = generation,
            OrganisationId = organisationId,
            ObligationYear = 2026,
            RegistrationType = RegistrationType.DirectProducer,
            RegistrationStatus = OrganisationRegistrationStatus.Registered,
            Name = name,
            ReferenceNumber = referenceNumber,
            ReferenceNumberResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
            SourceFingerprint = name,
            RefreshedAt = DateTime.UtcNow,
        };

    private static ComplianceDeclarationReviewState ReviewState(Guid organisationId, int unsubmittedExclusionCount) =>
        new()
        {
            OrganisationId = organisationId,
            ObligationYear = 2026,
            RegistrationType = RegistrationType.DirectProducer,
            UnsubmittedExclusionCount = unsubmittedExclusionCount,
            UpdatedAt = DateTime.UtcNow,
        };
}
