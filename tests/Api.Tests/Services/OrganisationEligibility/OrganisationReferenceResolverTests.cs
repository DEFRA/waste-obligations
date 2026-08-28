using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Services.OrganisationEligibility;

public class OrganisationReferenceResolverTests
{
    private IOrganisationReferenceSearchService OrganisationReferenceSearchService { get; } =
        Substitute.For<IOrganisationReferenceSearchService>();

    [Fact]
    public async Task Resolve_WhenNoSourceRows_ShouldReturnEmptyWithoutAccountCalls()
    {
        var subject = CreateSubject();

        var result = await subject.Resolve([], [], TestContext.Current.CancellationToken);

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
    public async Task Resolve_WhenNewDirectProducerHasMultipleYears_ShouldMakeOneLookupAndResolveEachRow()
    {
        var organisationId = Guid.NewGuid();
        Guid[] organisationIds = [organisationId];
        OrganisationReferenceSearchService
            .SearchOrganisationsByExternalIds(
                Arg.Is<IReadOnlyCollection<Guid>>(x => x.SequenceEqual(organisationIds)),
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

        var result = await subject.Resolve(
            [Row(organisationId, 2025), Row(organisationId, 2026)],
            [],
            TestContext.Current.CancellationToken
        );

        result.Should().HaveCount(2);
        result
            .Should()
            .OnlyContain(x =>
                x.ReferenceNumber == "051829"
                && x.ReferenceNumberResolutionState == OrganisationReferenceNumberResolutionState.Resolved
            );
    }

    [Fact]
    public async Task Resolve_WhenActiveRowsHaveAResolvedReference_ShouldReuseItWithoutAccountCall()
    {
        var organisationId = Guid.NewGuid();
        var activeRow = Row(organisationId, 2026) with
        {
            ReferenceNumber = "051829",
            ReferenceNumberResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
        };
        var subject = CreateSubject();

        var result = await subject.Resolve(
            [Row(organisationId, 2027)],
            [activeRow],
            TestContext.Current.CancellationToken
        );

        result.Single().ReferenceNumber.Should().Be("051829");
        result.Single().ReferenceNumberResolutionState.Should().Be(OrganisationReferenceNumberResolutionState.Resolved);
        await OrganisationReferenceSearchService
            .DidNotReceive()
            .SearchOrganisationsByExternalIds(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_WhenAResolvedSchemeLookupKeyChanges_ShouldRetainTheExistingReference()
    {
        var organisationId = Guid.NewGuid();
        var activeRow = Row(
            organisationId,
            2026,
            RegistrationType.ComplianceScheme,
            companiesHouseNumber: "12345678"
        ) with
        {
            ReferenceNumber = "530001",
            ReferenceNumberResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
        };
        var subject = CreateSubject();

        var result = await subject.Resolve(
            [Row(organisationId, 2027, RegistrationType.ComplianceScheme, companiesHouseNumber: "87654321")],
            [activeRow],
            TestContext.Current.CancellationToken
        );

        result.Single().ReferenceNumber.Should().Be("530001");
        result.Single().ReferenceNumberResolutionState.Should().Be(OrganisationReferenceNumberResolutionState.Resolved);
        await OrganisationReferenceSearchService
            .DidNotReceive()
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Resolve_WhenSchemeHasMultipleAccountMatches_ShouldMarkItAmbiguous()
    {
        const string companiesHouseNumber = "12345678";
        string[] companiesHouseNumbers = [companiesHouseNumber];
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
                        ReferenceNumber = "530001",
                        IsComplianceScheme = true,
                    },
                    new()
                    {
                        CompaniesHouseNumber = companiesHouseNumber,
                        ReferenceNumber = "530002",
                        IsComplianceScheme = true,
                    },
                }
            );
        var subject = CreateSubject();

        var result = await subject.Resolve(
            [Row(Guid.NewGuid(), 2026, RegistrationType.ComplianceScheme, companiesHouseNumber)],
            [],
            TestContext.Current.CancellationToken
        );

        result.Single().ReferenceNumber.Should().BeNull();
        result
            .Single()
            .ReferenceNumberResolutionState.Should()
            .Be(OrganisationReferenceNumberResolutionState.Ambiguous);
    }

    [Fact]
    public async Task Resolve_WhenAccountReturnsNoDirectProducerMatch_ShouldMarkItNotFound()
    {
        OrganisationReferenceSearchService
            .SearchOrganisationsByExternalIds(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new OrganisationsByExternalIdsResponse());
        var subject = CreateSubject();

        var result = await subject.Resolve([Row(Guid.NewGuid(), 2026)], [], TestContext.Current.CancellationToken);

        result.Single().ReferenceNumber.Should().BeNull();
        result.Single().ReferenceNumberResolutionState.Should().Be(OrganisationReferenceNumberResolutionState.NotFound);
    }

    [Fact]
    public async Task Resolve_WhenSchemeHasNoCompaniesHouseNumber_ShouldAwaitLookupKeyWithoutAccountCall()
    {
        var subject = CreateSubject();

        var result = await subject.Resolve(
            [Row(Guid.NewGuid(), 2026, RegistrationType.ComplianceScheme)],
            [],
            TestContext.Current.CancellationToken
        );

        result.Single().ReferenceNumber.Should().BeNull();
        result
            .Single()
            .ReferenceNumberResolutionState.Should()
            .Be(OrganisationReferenceNumberResolutionState.AwaitingLookupKey);
        await OrganisationReferenceSearchService
            .DidNotReceive()
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Resolve_WhenAccountLookupFails_ShouldMarkTheRowsAsFailed()
    {
        OrganisationReferenceSearchService
            .SearchOrganisationsByExternalIds(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException<OrganisationsByExternalIdsResponse>(new HttpRequestException("Account unavailable"))
            );
        var subject = CreateSubject();

        var result = await subject.Resolve([Row(Guid.NewGuid(), 2026)], [], TestContext.Current.CancellationToken);

        result.Single().ReferenceNumber.Should().BeNull();
        result.Single().ReferenceNumberResolutionState.Should().Be(OrganisationReferenceNumberResolutionState.Failed);
    }

    [Fact]
    public async Task Resolve_WhenSchemeLookupFails_ShouldMarkTheRowsAsFailed()
    {
        OrganisationReferenceSearchService
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Task.FromException<IReadOnlyList<AccountOrganisation>>(new HttpRequestException("Account unavailable"))
            );
        var subject = CreateSubject();

        var result = await subject.Resolve(
            [Row(Guid.NewGuid(), 2026, RegistrationType.ComplianceScheme, "12345678")],
            [],
            TestContext.Current.CancellationToken
        );

        result.Single().ReferenceNumber.Should().BeNull();
        result.Single().ReferenceNumberResolutionState.Should().Be(OrganisationReferenceNumberResolutionState.Failed);
    }

    [Fact]
    public async Task Resolve_WhenSourceHasInconsistentSchemeCompaniesHouseNumbers_ShouldFail()
    {
        var organisationId = Guid.NewGuid();
        var subject = CreateSubject();

        var act = () =>
            subject.Resolve(
                [
                    Row(organisationId, 2025, RegistrationType.ComplianceScheme, "12345678"),
                    Row(organisationId, 2026, RegistrationType.ComplianceScheme, "87654321"),
                ],
                [],
                TestContext.Current.CancellationToken
            );

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Resolve_WhenActiveRowsConflictOnAResolvedReference_ShouldFail()
    {
        var organisationId = Guid.NewGuid();
        var subject = CreateSubject();

        var act = () =>
            subject.Resolve(
                [Row(organisationId, 2027)],
                [
                    Row(organisationId, 2025) with
                    {
                        ReferenceNumber = "051829",
                        ReferenceNumberResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
                    },
                    Row(organisationId, 2026) with
                    {
                        ReferenceNumber = "051830",
                        ReferenceNumberResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
                    },
                ],
                TestContext.Current.CancellationToken
            );

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private OrganisationReferenceResolver CreateSubject() =>
        new(
            OrganisationReferenceSearchService,
            Options.Create(new OrganisationEligibilityOptions { AccountReferenceNumberBatchSize = 10 }),
            NullLogger<OrganisationReferenceResolver>.Instance
        );

    private static OrganisationComplianceDeclarationEligibility Row(
        Guid organisationId,
        int obligationYear,
        RegistrationType registrationType = RegistrationType.DirectProducer,
        string? companiesHouseNumber = null
    ) =>
        new()
        {
            Generation = "generation",
            OrganisationId = organisationId,
            ObligationYear = obligationYear,
            RegistrationType = registrationType,
            RegistrationStatus = OrganisationRegistrationStatus.Registered,
            Name = "Organisation",
            CompaniesHouseNumber = companiesHouseNumber,
            ReferenceNumberResolutionState = OrganisationReferenceNumberResolutionState.Pending,
            SourceFingerprint = "fingerprint",
        };
}
