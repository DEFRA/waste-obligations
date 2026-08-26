using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Driver;
using NSubstitute;
using OrganisationEligibilityEntity = Defra.WasteObligations.Api.Data.Entities.OrganisationEligibility;

namespace Defra.WasteObligations.Api.IntegrationTests.Services.OrganisationEligibility;

public class OrganisationReferenceCacheServiceTests : IntegrationTestBase
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
    private IAccountBackendService AccountBackendService { get; } = Substitute.For<IAccountBackendService>();

    [Fact]
    public async Task SynchroniseAndResolve_WhenNewDirectProducer_ShouldResolveAndDeduplicateYears()
    {
        var organisationId = Guid.NewGuid();
        AccountBackendService
            .SearchOrganisationsByExternalIds(
                Arg.Is<IReadOnlyCollection<Guid>>(x => x.SequenceEqual(new[] { organisationId })),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new OrganisationsByExternalIdsResponse
                {
                    Organisations =
                    [
                        new AccountOrganisation
                        {
                            ExternalId = organisationId.ToString("D"),
                            ReferenceNumber = "051829",
                        },
                    ],
                }
            );
        var subject = CreateSubject();

        var result = await subject.SynchroniseAndResolve(
            [
                CreateEligibilityRow(organisationId, RegistrationType.DirectProducer, 2025),
                CreateEligibilityRow(organisationId, RegistrationType.DirectProducer, 2026),
            ],
            TestContext.Current.CancellationToken
        );

        result
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    OrganisationId = organisationId,
                    RegistrationType = RegistrationType.DirectProducer,
                    LookupMode = OrganisationReferenceLookupMode.AccountExternalId,
                    ReferenceNumber = "051829",
                    ResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
                    AttemptCount = 1,
                    ResolvedAt = _timeProvider.GetUtcNow().UtcDateTime,
                },
                options => options.ExcludingMissingMembers()
            );
        var cache = await OrganisationReferenceCaches
            .Find(x => x.OrganisationId == organisationId)
            .SingleAsync(TestContext.Current.CancellationToken);
        cache.ReferenceNumber.Should().Be("051829");
    }

    [Fact]
    public async Task SynchroniseAndResolve_WhenSchemeHasMultipleMatchingAccountOrganisations_ShouldMarkAmbiguous()
    {
        var organisationId = Guid.NewGuid();
        AccountBackendService
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Is<IReadOnlyCollection<string>>(x => x.SequenceEqual(new[] { "12345678" })),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new List<AccountOrganisation>
                {
                    new()
                    {
                        ExternalId = Guid.NewGuid().ToString("D"),
                        CompaniesHouseNumber = "12345678",
                        ReferenceNumber = "530001",
                        IsComplianceScheme = true,
                    },
                    new()
                    {
                        ExternalId = Guid.NewGuid().ToString("D"),
                        CompaniesHouseNumber = "12345678",
                        ReferenceNumber = "530002",
                        IsComplianceScheme = true,
                    },
                }
            );
        var subject = CreateSubject();

        var result = await subject.SynchroniseAndResolve(
            [CreateEligibilityRow(organisationId, RegistrationType.ComplianceScheme, 2026)],
            TestContext.Current.CancellationToken
        );

        result
            .Should()
            .ContainSingle()
            .Which.ResolutionState.Should()
            .Be(OrganisationReferenceNumberResolutionState.Ambiguous);
        result.Single().ReferenceNumber.Should().BeNull();
        result.Single().NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task SynchroniseAndResolve_WhenReferenceIsAlreadyResolved_ShouldReuseCacheWithoutAccountCall()
    {
        var organisationId = Guid.NewGuid();
        await OrganisationReferenceCaches.InsertOneAsync(
            new OrganisationReferenceCache
            {
                OrganisationId = organisationId,
                RegistrationType = RegistrationType.DirectProducer,
                LookupMode = OrganisationReferenceLookupMode.AccountExternalId,
                ReferenceNumber = "051829",
                ResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
                FirstSeenAt = _timeProvider.GetUtcNow().UtcDateTime,
                LastSeenAt = _timeProvider.GetUtcNow().UtcDateTime,
                ResolvedAt = _timeProvider.GetUtcNow().UtcDateTime,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var subject = CreateSubject();

        var result = await subject.SynchroniseAndResolve(
            [CreateEligibilityRow(organisationId, RegistrationType.DirectProducer, 2026)],
            TestContext.Current.CancellationToken
        );

        result.Single().ReferenceNumber.Should().Be("051829");
        await AccountBackendService
            .DidNotReceive()
            .SearchOrganisationsByExternalIds(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SynchroniseAndResolve_WhenSchemeHasNoCompaniesHouseNumber_ShouldAwaitLookupKeyWithoutAccountCall()
    {
        var organisationId = Guid.NewGuid();
        var subject = CreateSubject();

        var result = await subject.SynchroniseAndResolve(
            [CreateEligibilityRow(organisationId, RegistrationType.ComplianceScheme, 2026, companiesHouseNumber: null)],
            TestContext.Current.CancellationToken
        );

        result.Single().ResolutionState.Should().Be(OrganisationReferenceNumberResolutionState.AwaitingLookupKey);
        await AccountBackendService
            .DidNotReceive()
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            );
    }

    private OrganisationReferenceCacheService CreateSubject() =>
        new(
            new MongoDbContext(
                GetMongoDatabase(),
                Options.Create(new MongoDbOptions()),
                Substitute.For<Microsoft.Extensions.Logging.ILogger<MongoDbContext>>()
            ),
            AccountBackendService,
            Options.Create(new OrganisationEligibilityOptions { AccountReferenceNumberBatchSize = 10 }),
            _timeProvider
        );

    private static OrganisationEligibilityEntity CreateEligibilityRow(
        Guid organisationId,
        RegistrationType registrationType,
        int obligationYear,
        string? companiesHouseNumber = "12345678"
    ) =>
        new()
        {
            Generation = "g1",
            OrganisationId = organisationId,
            ObligationYear = obligationYear,
            RegistrationType = registrationType,
            RegistrationStatus = OrganisationRegistrationStatus.Registered,
            Name = "Example organisation",
            CompaniesHouseNumber = companiesHouseNumber,
            ReferenceNumberResolutionState = OrganisationReferenceNumberResolutionState.Pending,
            SourceFingerprint = "source-fingerprint",
        };
}
