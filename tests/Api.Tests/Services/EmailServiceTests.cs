using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Defra.WasteObligations.Api.Services.GovukNotify;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing.Fixtures.AccountBackend;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Defra.WasteObligations.Testing.Fixtures.WasteOrganisations;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OrganisationFixture = Defra.WasteObligations.Testing.Fixtures.WasteOrganisations.OrganisationFixture;

namespace Defra.WasteObligations.Api.Tests.Services;

public class EmailServiceTests
{
    private IGovukNotifyService GovukNotifyService { get; } = Substitute.For<IGovukNotifyService>();
    private IAccountBackendService AccountBackendService { get; } = Substitute.For<IAccountBackendService>();
    private EmailService Subject { get; }

    public EmailServiceTests()
    {
        Subject = new EmailService(GovukNotifyService, AccountBackendService, NullLogger<EmailService>.Instance);
    }

    [Theory]
    [InlineData(false, EntityTypeCode.CS)]
    [InlineData(true, EntityTypeCode.DR)]
    public async Task SendSubmittedEmail_ShouldCallGovukNotify(bool directProducer, EntityTypeCode entityTypeCode)
    {
        var complianceDeclaration = directProducer
            ? ComplianceDeclarationFixture.DirectProducer(OrganisationFixture.OrganisationId).Create()
            : ComplianceDeclarationFixture.ComplianceScheme(OrganisationFixture.OrganisationId).Create();
        var organisation = OrganisationFixture.Default().Create();
        AccountBackendService
            .ReadPersonEmails(OrganisationFixture.OrganisationId, entityTypeCode, TestContext.Current.CancellationToken)
            .Returns([PersonEmailFixture.Default()]);

        await Subject.SendSubmittedEmail(complianceDeclaration, organisation, TestContext.Current.CancellationToken);

        await GovukNotifyService
            .Received()
            .SendComplianceDeclarationSubmittedEmail(
                GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionDirectProducer,
                Arg.Is<IEnumerable<string>>(x => x.SequenceEqual(new List<string> { "first.last@example.com" })),
                Arg.Is<Dictionary<string, object>>(x =>
                    x.Count == 3
                    && (int)x["obligationYear"] == complianceDeclaration.ObligationYear
                    && (string)x["regulator"] == complianceDeclaration.Organisation.Regulator
                    && (string)x["regulatorEmail"] == complianceDeclaration.Organisation.RegulatorEmail
                ),
                complianceDeclaration.DeclarationText.Language
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
