using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.GovukNotify;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OrganisationFixture = Defra.WasteObligations.Testing.Fixtures.WasteOrganisations.OrganisationFixture;

namespace Defra.WasteObligations.Api.Tests.Services;

public class EmailServiceTests
{
    private IGovukNotifyService GovukNotifyService { get; } = Substitute.For<IGovukNotifyService>();
    private EmailService Subject { get; }

    public EmailServiceTests()
    {
        Subject = new EmailService(GovukNotifyService, NullLogger<EmailService>.Instance);
    }

    [Theory]
    [InlineData(true, GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionDirectProducer)]
    [InlineData(false, GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionComplianceScheme)]
    public async Task SendSubmittedEmail_ShouldCallGovukNotify(
        bool directProducer,
        GovukNotifyOptions.TemplateName expectedTemplate
    )
    {
        var complianceDeclaration = directProducer
            ? ComplianceDeclarationFixture.DirectProducer(OrganisationFixture.OrganisationId).Create()
            : ComplianceDeclarationFixture.ComplianceScheme(OrganisationFixture.OrganisationId).Create();
        var organisation = OrganisationFixture.Default().Create();

        await Subject.SendSubmittedEmail(complianceDeclaration, organisation, TestContext.Current.CancellationToken);

        await GovukNotifyService
            .Received()
            .SendComplianceDeclarationSubmittedEmail(
                expectedTemplate,
                Arg.Is<IEnumerable<string>>(x => x.SequenceEqual(new[] { "submitter@email.com" })),
                Arg.Is<Dictionary<string, object>>(x =>
                    x.Count == 4
                    && (int)x["obligationYear"] == complianceDeclaration.ObligationYear
                    && (string)x["regulator"] == complianceDeclaration.Organisation.Regulator
                    && (string)x["regulatorEmail"] == complianceDeclaration.Organisation.RegulatorEmail
                    && (string)x["user"] == "Submitter Name"
                ),
                "en"
            );
    }

    [Fact]
    public async Task SendSubmittedEmail_WhenGovukNotifyThrows_IsSwallowed()
    {
        GovukNotifyService
            .SendComplianceDeclarationSubmittedEmail(
                Arg.Any<GovukNotifyOptions.TemplateName>(),
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<string>()
            )
            .ThrowsAsync(new Exception("BOOM!"));

        var act = () =>
            Subject.SendSubmittedEmail(
                ComplianceDeclarationFixture.DirectProducer(OrganisationFixture.OrganisationId).Create(),
                OrganisationFixture.Default().Create(),
                TestContext.Current.CancellationToken
            );

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendSubmittedEmail_WhenOrganisationIdMismatch_ShouldThrow()
    {
        var act = () =>
            Subject.SendSubmittedEmail(
                ComplianceDeclarationFixture.DirectProducer().Create(),
                OrganisationFixture.Default().Create(),
                TestContext.Current.CancellationToken
            );

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
