using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;
using Organisation = Defra.WasteObligations.Api.Services.WasteOrganisations.Organisation;
using OrganisationEligibilityEntity = Defra.WasteObligations.Api.Data.Entities.OrganisationEligibility;
using Registration = Defra.WasteObligations.Api.Services.WasteOrganisations.Registration;
using WasteOrganisationsAddress = Defra.WasteObligations.Api.Services.WasteOrganisations.Address;
using WasteOrganisationsRegistrationStatus = Defra.WasteObligations.Api.Services.WasteOrganisations.RegistrationStatus;
using WasteOrganisationsRegistrationType = Defra.WasteObligations.Api.Services.WasteOrganisations.RegistrationType;

namespace Defra.WasteObligations.Api.IntegrationTests.Services.OrganisationEligibility;

public class OrganisationEligibilityRefreshServiceTests : IntegrationTestBase
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
    private IAccountBackendService AccountBackendService { get; } = Substitute.For<IAccountBackendService>();
    private IWasteOrganisationsService WasteOrganisationsService { get; } =
        Substitute.For<IWasteOrganisationsService>();

    [Fact]
    public async Task Refresh_WhenNoActiveSnapshot_ShouldPromoteResolvedRows()
    {
        var organisationId = Guid.NewGuid();
        ArrangeSource(organisationId);
        ArrangeDirectProducerReference(organisationId, "051829");
        var subject = CreateSubject();

        var result = await subject.Refresh(TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(OrganisationEligibilityRefreshOutcome.Promoted);
        result.ActiveGeneration.Should().NotBeNullOrWhiteSpace();
        result.RowCount.Should().Be(1);
        var snapshot = await OrganisationEligibilitySnapshots
            .Find(x => x.Id == OrganisationEligibilitySnapshot.SnapshotId)
            .SingleAsync(TestContext.Current.CancellationToken);
        snapshot.ActiveGeneration.Should().Be(result.ActiveGeneration);
        snapshot.ActiveContentFingerprint.Should().Be(result.ContentFingerprint);
        var row = await OrganisationEligibilities
            .Find(x => x.Generation == result.ActiveGeneration)
            .SingleAsync(TestContext.Current.CancellationToken);
        row.ReferenceNumber.Should().Be("051829");
        row.ReferenceNumberResolutionState.Should().Be(OrganisationReferenceNumberResolutionState.Resolved);
    }

    [Fact]
    public async Task Refresh_WhenMultipleOrganisationsAreNew_ShouldPromoteEveryRow()
    {
        var firstOrganisationId = Guid.NewGuid();
        var secondOrganisationId = Guid.NewGuid();
        WasteOrganisationsService
            .Search(Arg.Any<CancellationToken>())
            .Returns(
                new OrganisationSearch
                {
                    Organisations =
                    [
                        CreateSourceOrganisation(firstOrganisationId, "First organisation"),
                        CreateSourceOrganisation(secondOrganisationId, "Second organisation"),
                    ],
                }
            );
        AccountBackendService
            .SearchOrganisationsByExternalIds(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(
                new OrganisationsByExternalIdsResponse
                {
                    Organisations =
                    [
                        new AccountOrganisation
                        {
                            ExternalId = firstOrganisationId.ToString("D"),
                            ReferenceNumber = "051829",
                        },
                        new AccountOrganisation
                        {
                            ExternalId = secondOrganisationId.ToString("D"),
                            ReferenceNumber = "051830",
                        },
                    ],
                }
            );
        var subject = CreateSubject();

        var result = await subject.Refresh(TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(OrganisationEligibilityRefreshOutcome.Promoted);
        result.RowCount.Should().Be(2);
        var rows = await OrganisationEligibilities
            .Find(Builders<OrganisationEligibilityEntity>.Filter.Empty)
            .ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(x => x.Id != ObjectId.Empty);
    }

    [Fact]
    public async Task Refresh_WhenContentIsUnchanged_ShouldVerifyWithoutWritingAnotherGeneration()
    {
        var organisationId = Guid.NewGuid();
        ArrangeSource(organisationId);
        ArrangeDirectProducerReference(organisationId, "051829");
        var subject = CreateSubject();
        var initial = await subject.Refresh(TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromMinutes(30));

        var refreshed = await subject.Refresh(TestContext.Current.CancellationToken);

        refreshed.Outcome.Should().Be(OrganisationEligibilityRefreshOutcome.Unchanged);
        refreshed.ActiveGeneration.Should().Be(initial.ActiveGeneration);
        (
            await OrganisationEligibilities.CountDocumentsAsync(
                Builders<OrganisationEligibilityEntity>.Filter.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(1);
        var snapshot = await OrganisationEligibilitySnapshots
            .Find(x => x.Id == OrganisationEligibilitySnapshot.SnapshotId)
            .SingleAsync(TestContext.Current.CancellationToken);
        snapshot.LastVerifiedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        await AccountBackendService
            .Received(1)
            .SearchOrganisationsByExternalIds(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_WhenSourceRowChanges_ShouldPromoteACompleteNewGenerationWithoutAnotherReferenceLookup()
    {
        var organisationId = Guid.NewGuid();
        ArrangeSource(organisationId);
        ArrangeDirectProducerReference(organisationId, "051829");
        var subject = CreateSubject();
        var initial = await subject.Refresh(TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromMinutes(30));
        ArrangeSource(organisationId, name: "Changed organisation name");

        var refreshed = await subject.Refresh(TestContext.Current.CancellationToken);

        refreshed.Outcome.Should().Be(OrganisationEligibilityRefreshOutcome.Promoted);
        refreshed.ActiveGeneration.Should().NotBe(initial.ActiveGeneration);
        (
            await OrganisationEligibilities.CountDocumentsAsync(
                Builders<OrganisationEligibilityEntity>.Filter.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(2);
        var activeRow = await OrganisationEligibilities
            .Find(x => x.Generation == refreshed.ActiveGeneration)
            .SingleAsync(TestContext.Current.CancellationToken);
        activeRow.Name.Should().Be("Changed organisation name");
        activeRow.ReferenceNumber.Should().Be("051829");
        await AccountBackendService
            .Received(1)
            .SearchOrganisationsByExternalIds(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    private OrganisationEligibilityRefreshService CreateSubject()
    {
        var dbContext = new MongoDbContext(
            GetMongoDatabase(),
            Options.Create(new MongoDbOptions()),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<MongoDbContext>>()
        );
        var options = Options.Create(new OrganisationEligibilityOptions { AccountReferenceNumberBatchSize = 10 });
        var cacheService = new OrganisationReferenceCacheService(
            dbContext,
            AccountBackendService,
            options,
            _timeProvider
        );

        return new OrganisationEligibilityRefreshService(
            dbContext,
            WasteOrganisationsService,
            cacheService,
            _timeProvider
        );
    }

    private void ArrangeSource(Guid organisationId, string name = "Example organisation") =>
        WasteOrganisationsService
            .Search(Arg.Any<CancellationToken>())
            .Returns(new OrganisationSearch { Organisations = [CreateSourceOrganisation(organisationId, name)] });

    private static Organisation CreateSourceOrganisation(Guid organisationId, string name) =>
        new()
        {
            Id = organisationId,
            Name = name,
            Address = new WasteOrganisationsAddress(),
            Registrations =
            [
                new Registration
                {
                    Type = WasteOrganisationsRegistrationType.LargeProducer,
                    Status = WasteOrganisationsRegistrationStatus.Registered,
                    RegistrationYear = 2026,
                },
            ],
        };

    private void ArrangeDirectProducerReference(Guid organisationId, string referenceNumber) =>
        AccountBackendService
            .SearchOrganisationsByExternalIds(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(
                new OrganisationsByExternalIdsResponse
                {
                    Organisations =
                    [
                        new AccountOrganisation
                        {
                            ExternalId = organisationId.ToString("D"),
                            ReferenceNumber = referenceNumber,
                        },
                    ],
                }
            );
}
