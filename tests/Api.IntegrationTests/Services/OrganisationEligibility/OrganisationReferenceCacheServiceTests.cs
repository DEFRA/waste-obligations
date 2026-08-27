using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;
using OrganisationComplianceDeclarationEligibilityEntity = Defra.WasteObligations.Api.Data.Entities.OrganisationComplianceDeclarationEligibility;

namespace Defra.WasteObligations.Api.IntegrationTests.Services.OrganisationEligibility;

public class OrganisationReferenceCacheServiceTests : IntegrationTestBase
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
    private IOrganisationReferenceSearchService OrganisationReferenceSearchService { get; } =
        Substitute.For<IOrganisationReferenceSearchService>();

    [Fact]
    public async Task SynchroniseAndResolve_WhenNoEligibilityRows_ShouldReturnEmptyWithoutAccountCall()
    {
        var subject = CreateSubject();

        var result = await subject.SynchroniseAndResolve([], TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        await OrganisationReferenceSearchService
            .DidNotReceive()
            .SearchOrganisationsByExternalIds(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        await OrganisationReferenceSearchService
            .DidNotReceive()
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task SynchroniseAndResolve_WhenNewDirectProducer_ShouldResolveAndDeduplicateYears()
    {
        var organisationId = Guid.NewGuid();
        OrganisationReferenceSearchService
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
    public async Task SynchroniseAndResolve_WhenMultipleNewOrganisations_ShouldPersistEachCache()
    {
        var firstOrganisationId = Guid.NewGuid();
        var secondOrganisationId = Guid.NewGuid();
        OrganisationReferenceSearchService
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

        var result = await subject.SynchroniseAndResolve(
            [
                CreateEligibilityRow(firstOrganisationId, RegistrationType.DirectProducer, 2026),
                CreateEligibilityRow(secondOrganisationId, RegistrationType.DirectProducer, 2026),
            ],
            TestContext.Current.CancellationToken
        );

        result.Should().HaveCount(2);
        var caches = await OrganisationReferenceCaches
            .Find(Builders<OrganisationReferenceCache>.Filter.Empty)
            .ToListAsync(TestContext.Current.CancellationToken);
        caches.Should().HaveCount(2);
        caches.Should().OnlyContain(x => x.Id != ObjectId.Empty);
    }

    [Fact]
    public async Task SynchroniseAndResolve_WhenSchemeHasMultipleMatchingAccountOrganisations_ShouldMarkAmbiguous()
    {
        var organisationId = Guid.NewGuid();
        var companiesHouseNumbers = new[] { "12345678" };
        OrganisationReferenceSearchService
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Is<IReadOnlyCollection<string>>(x => x.SequenceEqual(companiesHouseNumbers)),
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
        result.Single().NextAttemptAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddHours(24));
    }

    [Fact]
    public async Task SynchroniseAndResolve_WhenAmbiguousReferenceBecomesDueAndAccountIsCorrected_ShouldResolve()
    {
        const string companiesHouseNumber = "12345678";
        const string referenceNumber = "530001";
        var organisationId = Guid.NewGuid();
        var companiesHouseNumbers = new[] { companiesHouseNumber };
        OrganisationReferenceSearchService
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Is<IReadOnlyCollection<string>>(x => x.SequenceEqual(companiesHouseNumbers)),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new List<AccountOrganisation>
                {
                    new()
                    {
                        CompaniesHouseNumber = companiesHouseNumber,
                        ReferenceNumber = referenceNumber,
                        IsComplianceScheme = true,
                    },
                    new()
                    {
                        CompaniesHouseNumber = companiesHouseNumber,
                        ReferenceNumber = "530002",
                        IsComplianceScheme = true,
                    },
                },
                new List<AccountOrganisation>
                {
                    new()
                    {
                        CompaniesHouseNumber = companiesHouseNumber,
                        ReferenceNumber = referenceNumber,
                        IsComplianceScheme = true,
                    },
                }
            );
        var subject = CreateSubject();
        var row = CreateEligibilityRow(organisationId, RegistrationType.ComplianceScheme, 2026);
        var ambiguous = await subject.SynchroniseAndResolve([row], TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromHours(24).Subtract(TimeSpan.FromMinutes(1)));

        var beforeDue = await subject.SynchroniseAndResolve([row], TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        var resolved = await subject.SynchroniseAndResolve([row], TestContext.Current.CancellationToken);

        ambiguous.Single().ResolutionState.Should().Be(OrganisationReferenceNumberResolutionState.Ambiguous);
        beforeDue.Single().AttemptCount.Should().Be(1);
        resolved
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    ReferenceNumber = referenceNumber,
                    ResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
                    AttemptCount = 2,
                    NextAttemptAt = (DateTime?)null,
                },
                options => options.ExcludingMissingMembers()
            );
        await OrganisationReferenceSearchService
            .Received(2)
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task SynchroniseAndResolve_WhenLegacyAmbiguousReferenceHasNoNextAttempt_ShouldRetry()
    {
        const string companiesHouseNumber = "12345678";
        const string referenceNumber = "530001";
        var organisationId = Guid.NewGuid();
        await OrganisationReferenceCaches.InsertOneAsync(
            new OrganisationReferenceCache
            {
                OrganisationId = organisationId,
                RegistrationType = RegistrationType.ComplianceScheme,
                LookupMode = OrganisationReferenceLookupMode.CompaniesHouseNumber,
                CompaniesHouseNumber = companiesHouseNumber,
                ResolutionState = OrganisationReferenceNumberResolutionState.Ambiguous,
                FirstSeenAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-1),
                LastSeenAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-1),
                LastAttemptedAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-1),
                NextAttemptAt = null,
                AttemptCount = 1,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        OrganisationReferenceSearchService
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new List<AccountOrganisation>
                {
                    new()
                    {
                        CompaniesHouseNumber = companiesHouseNumber,
                        ReferenceNumber = referenceNumber,
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
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    ReferenceNumber = referenceNumber,
                    ResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
                    AttemptCount = 2,
                },
                options => options.ExcludingMissingMembers()
            );
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
        await OrganisationReferenceSearchService
            .DidNotReceive()
            .SearchOrganisationsByExternalIds(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SynchroniseAndResolve_WhenReferenceCacheMatchesSource_ShouldReuseItWithoutDataChurn()
    {
        var organisationId = Guid.NewGuid();
        var firstSeenAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-1);
        await OrganisationReferenceCaches.InsertOneAsync(
            new OrganisationReferenceCache
            {
                OrganisationId = organisationId,
                RegistrationType = RegistrationType.DirectProducer,
                LookupMode = OrganisationReferenceLookupMode.AccountExternalId,
                CompaniesHouseNumber = "12345678",
                ReferenceNumber = "051829",
                ResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
                FirstSeenAt = firstSeenAt,
                LastSeenAt = firstSeenAt,
                ResolvedAt = firstSeenAt,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var subject = CreateSubject();

        var result = await subject.SynchroniseAndResolve(
            [CreateEligibilityRow(organisationId, RegistrationType.DirectProducer, 2026)],
            TestContext.Current.CancellationToken
        );

        result.Single().LastSeenAt.Should().Be(firstSeenAt);
        var cache = await OrganisationReferenceCaches
            .Find(x => x.OrganisationId == organisationId)
            .SingleAsync(TestContext.Current.CancellationToken);
        cache.LastSeenAt.Should().Be(firstSeenAt);
        await OrganisationReferenceSearchService
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
        await OrganisationReferenceSearchService
            .DidNotReceive()
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task SynchroniseAndResolve_WhenAccountHasNoMatchingOrganisation_ShouldMarkReferenceAsNotFound()
    {
        var organisationId = Guid.NewGuid();
        OrganisationReferenceSearchService
            .SearchOrganisationsByExternalIds(
                Arg.Is<IReadOnlyCollection<Guid>>(x => x.SequenceEqual(new[] { organisationId })),
                Arg.Any<CancellationToken>()
            )
            .Returns(new OrganisationsByExternalIdsResponse { Organisations = [] });
        var subject = CreateSubject();

        var result = await subject.SynchroniseAndResolve(
            [CreateEligibilityRow(organisationId, RegistrationType.DirectProducer, 2026)],
            TestContext.Current.CancellationToken
        );

        result
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    ResolutionState = OrganisationReferenceNumberResolutionState.NotFound,
                    ReferenceNumber = (string?)null,
                    AttemptCount = 1,
                    NextAttemptAt = _timeProvider.GetUtcNow().UtcDateTime.AddHours(6),
                },
                options => options.ExcludingMissingMembers()
            );
    }

    [Fact]
    public async Task SynchroniseAndResolve_WhenAccountSearchFails_ShouldMarkReferenceForRetry()
    {
        var organisationId = Guid.NewGuid();
        var failure = new string('x', 501);
        OrganisationReferenceSearchService
            .SearchOrganisationsByExternalIds(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<OrganisationsByExternalIdsResponse>(new InvalidOperationException(failure)));
        var subject = CreateSubject();

        var result = await subject.SynchroniseAndResolve(
            [CreateEligibilityRow(organisationId, RegistrationType.DirectProducer, 2026)],
            TestContext.Current.CancellationToken
        );

        result
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    ResolutionState = OrganisationReferenceNumberResolutionState.Failed,
                    AttemptCount = 1,
                    NextAttemptAt = _timeProvider.GetUtcNow().UtcDateTime.AddHours(6),
                    LastFailure = new string('x', 500),
                },
                options => options.ExcludingMissingMembers()
            );
    }

    [Fact]
    public async Task SynchroniseAndResolve_WhenFailedReferenceBecomesDue_ShouldRetryAndResolve()
    {
        const string referenceNumber = "051829";
        var organisationId = Guid.NewGuid();
        OrganisationReferenceSearchService
            .SearchOrganisationsByExternalIds(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException<OrganisationsByExternalIdsResponse>(
                    new HttpRequestException("Account is unavailable")
                )
            );
        var subject = CreateSubject();
        var row = CreateEligibilityRow(organisationId, RegistrationType.DirectProducer, 2026);
        var failed = await subject.SynchroniseAndResolve([row], TestContext.Current.CancellationToken);
        _timeProvider.Advance(TimeSpan.FromHours(6).Subtract(TimeSpan.FromMinutes(1)));

        var beforeDue = await subject.SynchroniseAndResolve([row], TestContext.Current.CancellationToken);
        OrganisationReferenceSearchService
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
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        var resolved = await subject.SynchroniseAndResolve([row], TestContext.Current.CancellationToken);

        failed.Single().ResolutionState.Should().Be(OrganisationReferenceNumberResolutionState.Failed);
        beforeDue.Single().AttemptCount.Should().Be(1);
        resolved
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    ReferenceNumber = referenceNumber,
                    ResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
                    AttemptCount = 2,
                    NextAttemptAt = (DateTime?)null,
                },
                options => options.ExcludingMissingMembers()
            );
        await OrganisationReferenceSearchService
            .Received(2)
            .SearchOrganisationsByExternalIds(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SynchroniseAndResolve_WhenComplianceSchemeAccountSearchFails_ShouldMarkReferenceForRetry()
    {
        var organisationId = Guid.NewGuid();
        OrganisationReferenceSearchService
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Task.FromException<IReadOnlyList<AccountOrganisation>>(
                    new HttpRequestException("Account is unavailable")
                )
            );
        var subject = CreateSubject();

        var result = await subject.SynchroniseAndResolve(
            [CreateEligibilityRow(organisationId, RegistrationType.ComplianceScheme, 2026)],
            TestContext.Current.CancellationToken
        );

        result
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    ResolutionState = OrganisationReferenceNumberResolutionState.Failed,
                    AttemptCount = 1,
                    NextAttemptAt = _timeProvider.GetUtcNow().UtcDateTime.AddHours(6),
                    LastFailure = "Account is unavailable",
                },
                options => options.ExcludingMissingMembers()
            );
    }

    [Fact]
    public async Task SynchroniseAndResolve_WhenCompaniesHouseMatchIsNotAComplianceScheme_ShouldNotResolveIt()
    {
        var organisationId = Guid.NewGuid();
        OrganisationReferenceSearchService
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new List<AccountOrganisation>
                {
                    new()
                    {
                        CompaniesHouseNumber = "12345678",
                        ReferenceNumber = "530001",
                        IsComplianceScheme = false,
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
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    ResolutionState = OrganisationReferenceNumberResolutionState.NotFound,
                    ReferenceNumber = (string?)null,
                    AttemptCount = 1,
                    NextAttemptAt = _timeProvider.GetUtcNow().UtcDateTime.AddHours(6),
                },
                options => options.ExcludingMissingMembers()
            );
    }

    [Fact]
    public async Task SynchroniseAndResolve_WhenSchemeGainsCompaniesHouseNumber_ShouldResolveItsReference()
    {
        var organisationId = Guid.NewGuid();
        var companiesHouseNumbers = new[] { "12345678" };
        await OrganisationReferenceCaches.InsertOneAsync(
            new OrganisationReferenceCache
            {
                OrganisationId = organisationId,
                RegistrationType = RegistrationType.ComplianceScheme,
                LookupMode = OrganisationReferenceLookupMode.CompaniesHouseNumber,
                ResolutionState = OrganisationReferenceNumberResolutionState.AwaitingLookupKey,
                FirstSeenAt = _timeProvider.GetUtcNow().UtcDateTime,
                LastSeenAt = _timeProvider.GetUtcNow().UtcDateTime,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        OrganisationReferenceSearchService
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Is<IReadOnlyCollection<string>>(x => x.SequenceEqual(companiesHouseNumbers)),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new List<AccountOrganisation>
                {
                    new()
                    {
                        CompaniesHouseNumber = "12345678",
                        ReferenceNumber = "530001",
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
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    ReferenceNumber = "530001",
                    ResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
                    ResolvedUsingCompaniesHouseNumber = "12345678",
                    AttemptCount = 1,
                },
                options => options.ExcludingMissingMembers()
            );
    }

    [Fact]
    public async Task SynchroniseAndResolve_WhenOrganisationHasInconsistentCompaniesHouseNumbers_ShouldRejectTheSource()
    {
        var organisationId = Guid.NewGuid();
        var subject = CreateSubject();

        var act = () =>
            subject.SynchroniseAndResolve(
                [
                    CreateEligibilityRow(
                        organisationId,
                        RegistrationType.ComplianceScheme,
                        2025,
                        companiesHouseNumber: "12345678"
                    ),
                    CreateEligibilityRow(
                        organisationId,
                        RegistrationType.ComplianceScheme,
                        2026,
                        companiesHouseNumber: "87654321"
                    ),
                ],
                TestContext.Current.CancellationToken
            );

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Organisation * has inconsistent Companies House numbers for ComplianceScheme");
    }

    private OrganisationReferenceCacheService CreateSubject() =>
        new(
            new MongoDbContext(
                GetMongoDatabase(),
                Options.Create(new MongoDbOptions()),
                Substitute.For<Microsoft.Extensions.Logging.ILogger<MongoDbContext>>()
            ),
            OrganisationReferenceSearchService,
            Options.Create(new OrganisationEligibilityOptions { AccountReferenceNumberBatchSize = 10 }),
            _timeProvider,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<OrganisationReferenceCacheService>.Instance
        );

    private static OrganisationComplianceDeclarationEligibilityEntity CreateEligibilityRow(
        Guid organisationId,
        RegistrationType registrationType,
        int obligationYear,
        string? companiesHouseNumber = "12345678"
    ) =>
        OrganisationComplianceDeclarationEligibilityFixture
            .Default(organisationId)
            .With(x => x.Generation, "g1")
            .With(x => x.ObligationYear, obligationYear)
            .With(x => x.RegistrationType, registrationType)
            .With(x => x.CompaniesHouseNumber, companiesHouseNumber)
            .Without(x => x.ReferenceNumber)
            .With(x => x.ReferenceNumberResolutionState, OrganisationReferenceNumberResolutionState.Pending)
            .Create();
}
