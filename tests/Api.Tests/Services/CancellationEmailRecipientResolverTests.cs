using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Defra.WasteObligations.Testing.Fixtures.AccountBackend;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
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
            .Returns(new OrganisationWithPersons());

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
    public void ResolveSubmitter_SplitsDisplayNameWhenPersonIsNotOnOrganisation()
    {
        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(OrganisationFixture.OrganisationId)
            .Create();

        var recipient = CancellationEmailRecipientResolver.ResolveSubmitter(
            complianceDeclaration,
            organisationWithPersons: null
        );

        recipient.Should().NotBeNull();
        recipient!.FirstName.Should().Be("Submitter");
        recipient.LastName.Should().Be("Name");
    }
}
