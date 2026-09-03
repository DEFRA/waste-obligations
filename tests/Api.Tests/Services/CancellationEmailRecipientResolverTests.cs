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
        AccountBackendService
            .ReadOrganisationWithPersons(OrganisationFixture.OrganisationId, Arg.Any<CancellationToken>())
            .Returns(OrganisationWithPersonsFixture.CancellationRecipients());

        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(OrganisationFixture.OrganisationId)
            .Create();

        var recipients = await Subject.ResolveAsync(
            complianceDeclaration,
            OrganisationFixture.OrganisationId,
            TestContext.Current.CancellationToken
        );

        recipients.Should().HaveCount(2);
        recipients.Select(x => x.Email).Should().BeEquivalentTo("approved-person@email.com", "submitter@email.com");
    }

    [Fact]
    public async Task ResolveAsync_WhenSubmitterIsPrimaryContact_ReturnsOneRecipient()
    {
        AccountBackendService
            .ReadOrganisationWithPersons(OrganisationFixture.OrganisationId, Arg.Any<CancellationToken>())
            .Returns(OrganisationWithPersonsFixture.SubmitterMatchesApprovedPerson());

        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(OrganisationFixture.OrganisationId)
            .Create();

        var recipients = await Subject.ResolveAsync(
            complianceDeclaration,
            OrganisationFixture.OrganisationId,
            TestContext.Current.CancellationToken
        );

        recipients.Should().ContainSingle();
        recipients[0].Email.Should().Be("submitter@email.com");
    }

    [Fact]
    public async Task ResolveAsync_WhenPrimaryContactMissing_ReturnsSubmitterOnly()
    {
        AccountBackendService
            .ReadOrganisationWithPersons(OrganisationFixture.OrganisationId, Arg.Any<CancellationToken>())
            .Returns(OrganisationWithPersonsFixture.SubmitterOnly());

        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(OrganisationFixture.OrganisationId)
            .Create();

        var recipients = await Subject.ResolveAsync(
            complianceDeclaration,
            OrganisationFixture.OrganisationId,
            TestContext.Current.CancellationToken
        );

        recipients.Should().ContainSingle();
        recipients[0].Email.Should().Be("submitter@email.com");
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
}
