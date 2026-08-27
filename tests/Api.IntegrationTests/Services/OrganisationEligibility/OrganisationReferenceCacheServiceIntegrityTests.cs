using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using OrganisationComplianceDeclarationEligibilityEntity = Defra.WasteObligations.Api.Data.Entities.OrganisationComplianceDeclarationEligibility;

namespace Defra.WasteObligations.Api.IntegrationTests.Services.OrganisationEligibility;

public class OrganisationReferenceCacheServiceIntegrityTests : IntegrationTestBase
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));
    private IOrganisationReferenceSearchService OrganisationReferenceSearchService { get; } =
        Substitute.For<IOrganisationReferenceSearchService>();

    [Fact]
    public async Task SynchroniseAndResolve_WhenResolvedSchemeChangesCompaniesHouseNumber_ShouldRetainReferenceAndLogIntegrityError()
    {
        var organisationId = Guid.NewGuid();
        await OrganisationReferenceCaches.InsertOneAsync(
            new OrganisationReferenceCache
            {
                OrganisationId = organisationId,
                RegistrationType = RegistrationType.ComplianceScheme,
                LookupMode = OrganisationReferenceLookupMode.CompaniesHouseNumber,
                CompaniesHouseNumber = "12345678",
                ReferenceNumber = "530001",
                ResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
                ResolvedUsingCompaniesHouseNumber = "12345678",
                FirstSeenAt = _timeProvider.GetUtcNow().UtcDateTime,
                LastSeenAt = _timeProvider.GetUtcNow().UtcDateTime,
                ResolvedAt = _timeProvider.GetUtcNow().UtcDateTime,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var logger = new RecordingLogger<OrganisationReferenceCacheService>();
        var subject = CreateSubject(logger);

        var result = await subject.SynchroniseAndResolve(
            [CreateEligibilityRow(organisationId, "87654321")],
            TestContext.Current.CancellationToken
        );

        result
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    CompaniesHouseNumber = "87654321",
                    ReferenceNumber = "530001",
                    ResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
                    ResolvedUsingCompaniesHouseNumber = "12345678",
                    LastFailure = "The source lookup key changed after the reference number was resolved",
                },
                options => options.ExcludingMissingMembers()
            );
        logger
            .Entries.Should()
            .ContainSingle(x =>
                x.Level == LogLevel.Error
                && x.Message.StartsWith("Organisation reference cache lookup key changed after resolution")
            );
        await OrganisationReferenceSearchService
            .DidNotReceive()
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            );
    }

    private OrganisationReferenceCacheService CreateSubject(ILogger<OrganisationReferenceCacheService> logger) =>
        new(
            new MongoDbContext(
                GetMongoDatabase(),
                Options.Create(new MongoDbOptions()),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<MongoDbContext>.Instance
            ),
            OrganisationReferenceSearchService,
            Options.Create(new OrganisationEligibilityOptions { AccountReferenceNumberBatchSize = 10 }),
            _timeProvider,
            logger
        );

    private OrganisationComplianceDeclarationEligibilityEntity CreateEligibilityRow(
        Guid organisationId,
        string companiesHouseNumber
    ) =>
        new()
        {
            Generation = "generation",
            OrganisationId = organisationId,
            ObligationYear = 2026,
            RegistrationType = RegistrationType.ComplianceScheme,
            RegistrationStatus = OrganisationRegistrationStatus.Registered,
            Name = "Example scheme",
            CompaniesHouseNumber = companiesHouseNumber,
            ReferenceNumberResolutionState = OrganisationReferenceNumberResolutionState.Pending,
            SourceFingerprint = "source-fingerprint",
            RefreshedAt = _timeProvider.GetUtcNow().UtcDateTime,
        };
}
