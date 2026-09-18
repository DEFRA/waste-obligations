using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Defra.WasteObligations.Testing.Fixtures.AccountBackend;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ComplianceDeclarationStatus = Defra.WasteObligations.Api.Data.Entities.ComplianceDeclarationStatus;
using OrganisationFixture = Defra.WasteObligations.Testing.Fixtures.WasteOrganisations.OrganisationFixture;

namespace Defra.WasteObligations.Api.Tests.Services;

public class CancellationEmailRecipientResolverTests
{
    private IAccountBackendService AccountBackendService { get; } = Substitute.For<IAccountBackendService>();
    private CancellationEmailRecipientResolver Subject { get; }

    public CancellationEmailRecipientResolverTests()
    {
        Subject = new CancellationEmailRecipientResolver(
            AccountBackendService,
            NullLogger<CancellationEmailRecipientResolver>.Instance
        );
    }

    [Fact]
    public async Task ResolveAsync_WhenSubmitterAndPrimaryContactDiffer_ReturnsBothRecipients()
    {
        var organisation = OrganisationFixture.Default().Create();
        AccountBackendService
            .ReadOrganisationWithPersons(organisation.Id, Arg.Any<CancellationToken>())
            .Returns(OrganisationWithPersonsFixture.CancellationRecipients());

        var complianceDeclaration = ComplianceDeclarationFixture.DirectProducer(organisation.Id).Create();

        var recipients = await Subject.ResolveAsync(
            complianceDeclaration,
            organisation,
            TestContext.Current.CancellationToken
        );

        recipients.Should().HaveCount(2);
        recipients.Select(x => x.Email).Should().BeEquivalentTo("approved-person@email.com", "submitter@email.com");
    }

    [Fact]
    public async Task ResolveAsync_WhenSubmitterIsPrimaryContact_ReturnsOneRecipient()
    {
        var organisation = OrganisationFixture.Default().Create();
        AccountBackendService
            .ReadOrganisationWithPersons(organisation.Id, Arg.Any<CancellationToken>())
            .Returns(OrganisationWithPersonsFixture.SubmitterMatchesApprovedPerson());

        var complianceDeclaration = ComplianceDeclarationFixture.DirectProducer(organisation.Id).Create();

        var recipients = await Subject.ResolveAsync(
            complianceDeclaration,
            organisation,
            TestContext.Current.CancellationToken
        );

        recipients.Should().ContainSingle();
        recipients[0].Email.Should().Be("submitter@email.com");
    }

    [Fact]
    public async Task ResolveAsync_WhenPrimaryContactMissing_ReturnsSubmitterOnly()
    {
        var organisation = OrganisationFixture.Default().Create();
        AccountBackendService
            .ReadOrganisationWithPersons(organisation.Id, Arg.Any<CancellationToken>())
            .Returns(OrganisationWithPersonsFixture.SubmitterOnly());

        var complianceDeclaration = ComplianceDeclarationFixture.DirectProducer(organisation.Id).Create();

        var recipients = await Subject.ResolveAsync(
            complianceDeclaration,
            organisation,
            TestContext.Current.CancellationToken
        );

        recipients.Should().ContainSingle();
        recipients[0].Email.Should().Be("submitter@email.com");
    }

    [Fact]
    public async Task ResolveAsync_WhenComplianceSchemeAccountExternalIdDiffers_UsesCompaniesHouseLookup()
    {
        const string companiesHouseNumber = "33892901";
        var wasteOrganisationId = Guid.Parse("f326c755-b0ef-4b7b-9f57-4a711a5fd215");
        var accountOrganisationId = Guid.Parse("7F706042-E0E2-4959-9D9B-9AD87F72B188");
        var organisation = OrganisationFixture
            .Default(wasteOrganisationId)
            .With(x => x.CompaniesHouseNumber, companiesHouseNumber)
            .Create();

        AccountBackendService
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Is<IReadOnlyCollection<string>>(x => x.Single() == companiesHouseNumber),
                Arg.Any<CancellationToken>()
            )
            .Returns([
                new AccountOrganisation
                {
                    ExternalId = accountOrganisationId.ToString("D"),
                    ReferenceNumber = "338929",
                    CompaniesHouseNumber = companiesHouseNumber,
                    IsComplianceScheme = true,
                },
            ]);
        AccountBackendService
            .ReadOrganisationWithPersons(accountOrganisationId, Arg.Any<CancellationToken>())
            .Returns(OrganisationWithPersonsFixture.CancellationRecipients());

        var complianceDeclaration = ComplianceDeclarationFixture.ComplianceScheme(wasteOrganisationId).Create();

        var recipients = await Subject.ResolveAsync(
            complianceDeclaration,
            organisation,
            TestContext.Current.CancellationToken
        );

        recipients.Should().HaveCount(2);
        await AccountBackendService
            .DidNotReceive()
            .ReadOrganisationWithPersons(wasteOrganisationId, Arg.Any<CancellationToken>());
        await AccountBackendService
            .Received(1)
            .ReadOrganisationWithPersons(accountOrganisationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenCompaniesHouseLookupReturnsUnrelatedOperators_UsesMatchingOperatorOnly()
    {
        const string companiesHouseNumber = "33892901";
        var wasteOrganisationId = Guid.Parse("f326c755-b0ef-4b7b-9f57-4a711a5fd215");
        var accountOrganisationId = Guid.Parse("7F706042-E0E2-4959-9D9B-9AD87F72B188");
        var organisation = OrganisationFixture
            .Default(wasteOrganisationId)
            .With(x => x.CompaniesHouseNumber, companiesHouseNumber)
            .Create();

        AccountBackendService
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Is<IReadOnlyCollection<string>>(x => x.Single() == companiesHouseNumber),
                Arg.Any<CancellationToken>()
            )
            .Returns([
                new AccountOrganisation
                {
                    ExternalId = accountOrganisationId.ToString("D"),
                    ReferenceNumber = "338929",
                    CompaniesHouseNumber = companiesHouseNumber,
                    IsComplianceScheme = true,
                },
                new AccountOrganisation
                {
                    ExternalId = Guid.NewGuid().ToString("D"),
                    ReferenceNumber = "999999",
                    CompaniesHouseNumber = "99999999",
                    IsComplianceScheme = true,
                },
            ]);
        AccountBackendService
            .ReadOrganisationWithPersons(accountOrganisationId, Arg.Any<CancellationToken>())
            .Returns(OrganisationWithPersonsFixture.CancellationRecipients());

        var complianceDeclaration = ComplianceDeclarationFixture.ComplianceScheme(wasteOrganisationId).Create();

        var recipients = await Subject.ResolveAsync(
            complianceDeclaration,
            organisation,
            TestContext.Current.CancellationToken
        );

        recipients.Should().HaveCount(2);
        await AccountBackendService
            .Received(1)
            .ReadOrganisationWithPersons(accountOrganisationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenComplianceSchemeHasNoCompaniesHouseNumber_ReturnsNoRecipients()
    {
        var wasteOrganisationId = Guid.NewGuid();
        var organisation = OrganisationFixture
            .Default(wasteOrganisationId)
            .With(x => x.CompaniesHouseNumber, (string?)null)
            .Create();
        var complianceDeclaration = ComplianceDeclarationFixture.ComplianceScheme(wasteOrganisationId).Create();

        var recipients = await Subject.ResolveAsync(
            complianceDeclaration,
            organisation,
            TestContext.Current.CancellationToken
        );

        recipients.Should().BeEmpty();
        await AccountBackendService
            .DidNotReceive()
            .ReadOrganisationWithPersons(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenAccountReturnsNoOrganisationWithPersons_ReturnsNoRecipients()
    {
        var organisation = OrganisationFixture.Default().Create();
        AccountBackendService
            .ReadOrganisationWithPersons(organisation.Id, Arg.Any<CancellationToken>())
            .Returns((OrganisationWithPersons?)null);
        var complianceDeclaration = ComplianceDeclarationFixture.DirectProducer(organisation.Id).Create();

        var recipients = await Subject.ResolveAsync(
            complianceDeclaration,
            organisation,
            TestContext.Current.CancellationToken
        );

        recipients.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_WhenSubmitterMissing_ReturnsPrimaryContactOnly()
    {
        var organisation = OrganisationFixture.Default().Create();
        AccountBackendService
            .ReadOrganisationWithPersons(organisation.Id, Arg.Any<CancellationToken>())
            .Returns(
                new OrganisationWithPersons
                {
                    Persons =
                    [
                        new OrganisationPerson
                        {
                            FirstName = "Approved",
                            LastName = "Person",
                            Email = "approved-person@email.com",
                            ServiceRole = CancellationEmailRecipientResolver.ApprovedPersonServiceRole,
                        },
                    ],
                }
            );
        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(organisation.Id)
            .With(
                x => x.Audit,
                [
                    new AuditEntry(nameof(ComplianceDeclarationStatus.Submitted))
                    {
                        User = new User
                        {
                            Id = Guid.NewGuid().ToString("D"),
                            Email = "unknown@email.com",
                            Name = "Unknown Submitter",
                        },
                        Timestamp = new DateTime(2026, 4, 26, 14, 0, 0, DateTimeKind.Utc),
                    },
                ]
            )
            .Create();

        var recipients = await Subject.ResolveAsync(
            complianceDeclaration,
            organisation,
            TestContext.Current.CancellationToken
        );

        recipients.Should().ContainSingle();
        recipients[0].Email.Should().Be("approved-person@email.com");
    }

    [Fact]
    public async Task ResolveAsync_WhenCompaniesHouseLookupReturnsNoMatches_ReturnsNoRecipients()
    {
        const string companiesHouseNumber = "33892901";
        var wasteOrganisationId = Guid.NewGuid();
        var organisation = OrganisationFixture
            .Default(wasteOrganisationId)
            .With(x => x.CompaniesHouseNumber, companiesHouseNumber)
            .Create();
        AccountBackendService
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Is<IReadOnlyCollection<string>>(x => x.Single() == companiesHouseNumber),
                Arg.Any<CancellationToken>()
            )
            .Returns(Array.Empty<AccountOrganisation>());
        var complianceDeclaration = ComplianceDeclarationFixture.ComplianceScheme(wasteOrganisationId).Create();

        var recipients = await Subject.ResolveAsync(
            complianceDeclaration,
            organisation,
            TestContext.Current.CancellationToken
        );

        recipients.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_WhenCompaniesHouseLookupReturnsMultipleMatchingSchemes_ReturnsNoRecipients()
    {
        const string companiesHouseNumber = "33892901";
        var wasteOrganisationId = Guid.NewGuid();
        var organisation = OrganisationFixture
            .Default(wasteOrganisationId)
            .With(x => x.CompaniesHouseNumber, companiesHouseNumber)
            .Create();
        AccountBackendService
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Is<IReadOnlyCollection<string>>(x => x.Single() == companiesHouseNumber),
                Arg.Any<CancellationToken>()
            )
            .Returns([
                new AccountOrganisation
                {
                    ExternalId = Guid.NewGuid().ToString("D"),
                    ReferenceNumber = "338929",
                    CompaniesHouseNumber = companiesHouseNumber,
                    IsComplianceScheme = true,
                },
                new AccountOrganisation
                {
                    ExternalId = Guid.NewGuid().ToString("D"),
                    ReferenceNumber = "338930",
                    CompaniesHouseNumber = companiesHouseNumber,
                    IsComplianceScheme = true,
                },
            ]);
        var complianceDeclaration = ComplianceDeclarationFixture.ComplianceScheme(wasteOrganisationId).Create();

        var recipients = await Subject.ResolveAsync(
            complianceDeclaration,
            organisation,
            TestContext.Current.CancellationToken
        );

        recipients.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_WhenCompaniesHouseLookupReturnsInvalidExternalId_ReturnsNoRecipients()
    {
        const string companiesHouseNumber = "33892901";
        var wasteOrganisationId = Guid.NewGuid();
        var organisation = OrganisationFixture
            .Default(wasteOrganisationId)
            .With(x => x.CompaniesHouseNumber, companiesHouseNumber)
            .Create();
        AccountBackendService
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Is<IReadOnlyCollection<string>>(x => x.Single() == companiesHouseNumber),
                Arg.Any<CancellationToken>()
            )
            .Returns([
                new AccountOrganisation
                {
                    ExternalId = "not-a-guid",
                    ReferenceNumber = "338929",
                    CompaniesHouseNumber = companiesHouseNumber,
                    IsComplianceScheme = true,
                },
            ]);
        var complianceDeclaration = ComplianceDeclarationFixture.ComplianceScheme(wasteOrganisationId).Create();

        var recipients = await Subject.ResolveAsync(
            complianceDeclaration,
            organisation,
            TestContext.Current.CancellationToken
        );

        recipients.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_WhenCompaniesHouseLookupReturnsNonComplianceSchemeOnly_ReturnsNoRecipients()
    {
        const string companiesHouseNumber = "33892901";
        var wasteOrganisationId = Guid.NewGuid();
        var organisation = OrganisationFixture
            .Default(wasteOrganisationId)
            .With(x => x.CompaniesHouseNumber, companiesHouseNumber)
            .Create();
        AccountBackendService
            .SearchOrganisationsByCompaniesHouseNumbers(
                Arg.Is<IReadOnlyCollection<string>>(x => x.Single() == companiesHouseNumber),
                Arg.Any<CancellationToken>()
            )
            .Returns([
                new AccountOrganisation
                {
                    ExternalId = Guid.NewGuid().ToString("D"),
                    ReferenceNumber = "338929",
                    CompaniesHouseNumber = companiesHouseNumber,
                    IsComplianceScheme = false,
                },
            ]);
        var complianceDeclaration = ComplianceDeclarationFixture.ComplianceScheme(wasteOrganisationId).Create();

        var recipients = await Subject.ResolveAsync(
            complianceDeclaration,
            organisation,
            TestContext.Current.CancellationToken
        );

        recipients.Should().BeEmpty();
    }

    [Fact]
    public void ResolveSubmitter_WhenPersonIsNotOnOrganisation_ReturnsNull()
    {
        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(OrganisationFixture.OrganisationId)
            .Create();

        CancellationEmailRecipientResolver
            .ResolveSubmitter(complianceDeclaration, organisationWithPersons: null)
            .Should()
            .BeNull();
    }

    [Fact]
    public void ResolveSubmitter_WhenSubmitterEmailMissing_ReturnsNull()
    {
        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(OrganisationFixture.OrganisationId)
            .With(
                x => x.Audit,
                [
                    new AuditEntry(nameof(ComplianceDeclarationStatus.Submitted))
                    {
                        User = new User
                        {
                            Id = "e72be574-8b5b-4836-af47-dd7e0c0d1d87",
                            Email = "   ",
                            Name = "Submitter Name",
                        },
                        Timestamp = new DateTime(2026, 4, 26, 14, 0, 0, DateTimeKind.Utc),
                    },
                ]
            )
            .Create();

        CancellationEmailRecipientResolver
            .ResolveSubmitter(complianceDeclaration, organisationWithPersons: null)
            .Should()
            .BeNull();
    }

    [Fact]
    public void ResolveSubmitter_WhenPersonMatchesByEmail_ReturnsOrganisationNames()
    {
        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(OrganisationFixture.OrganisationId)
            .Create();
        var organisationWithPersons = new OrganisationWithPersons
        {
            Persons =
            [
                new OrganisationPerson
                {
                    FirstName = "Matched",
                    LastName = "Submitter",
                    Email = "submitter@email.com",
                },
            ],
        };

        var recipient = CancellationEmailRecipientResolver.ResolveSubmitter(
            complianceDeclaration,
            organisationWithPersons
        );

        recipient.Should().NotBeNull();
        recipient.FirstName.Should().Be("Matched");
        recipient.LastName.Should().Be("Submitter");
    }

    [Fact]
    public void ResolveSubmitter_WhenMatchedPersonDetailsIncomplete_ReturnsNull()
    {
        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(OrganisationFixture.OrganisationId)
            .Create();
        var organisationWithPersons = new OrganisationWithPersons
        {
            Persons = [new OrganisationPerson { Email = "submitter@email.com" }],
        };

        CancellationEmailRecipientResolver
            .ResolveSubmitter(complianceDeclaration, organisationWithPersons)
            .Should()
            .BeNull();
    }

    [Fact]
    public void ResolvePrimaryContact_WhenApprovedPersonDetailsIncomplete_ReturnsNull()
    {
        var organisationWithPersons = new OrganisationWithPersons
        {
            Persons =
            [
                new OrganisationPerson
                {
                    Email = "approved-person@email.com",
                    ServiceRole = CancellationEmailRecipientResolver.ApprovedPersonServiceRole,
                },
            ],
        };

        CancellationEmailRecipientResolver.ResolvePrimaryContact(organisationWithPersons).Should().BeNull();
    }

    [Fact]
    public void ResolveSubmitter_WhenPersonMatchesByUserId_ReturnsOrganisationNames()
    {
        const string submitterUserId = "e72be574-8b5b-4836-af47-dd7e0c0d1d87";
        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(OrganisationFixture.OrganisationId)
            .Create();
        var organisationWithPersons = new OrganisationWithPersons
        {
            Persons =
            [
                new OrganisationPerson
                {
                    UserId = Guid.Parse(submitterUserId),
                    FirstName = "Matched",
                    LastName = "ByUserId",
                    Email = "different@email.com",
                },
            ],
        };

        var recipient = CancellationEmailRecipientResolver.ResolveSubmitter(
            complianceDeclaration,
            organisationWithPersons
        );

        recipient.Should().NotBeNull();
        recipient.FirstName.Should().Be("Matched");
        recipient.LastName.Should().Be("ByUserId");
        recipient.Email.Should().Be("submitter@email.com");
    }

    [Fact]
    public void ResolvePrimaryContact_WhenApprovedPersonIsComplete_ReturnsRecipient()
    {
        var organisationWithPersons = new OrganisationWithPersons
        {
            Persons =
            [
                new OrganisationPerson
                {
                    FirstName = "Approved",
                    LastName = "Person",
                    Email = "approved-person@email.com",
                    ServiceRole = CancellationEmailRecipientResolver.ApprovedPersonServiceRole,
                },
            ],
        };

        var recipient = CancellationEmailRecipientResolver.ResolvePrimaryContact(organisationWithPersons);

        recipient.Should().NotBeNull();
        recipient.Email.Should().Be("approved-person@email.com");
    }
}
