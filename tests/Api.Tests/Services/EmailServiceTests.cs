using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.GovukNotify;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Defra.WasteObligations.Api.Tests.Services;

public class EmailServiceTests
{
    private IGovukNotifyService GovukNotifyService { get; } = Substitute.For<IGovukNotifyService>();
    private EmailService Subject { get; }

    public EmailServiceTests()
    {
        Subject = new EmailService(GovukNotifyService, NullLogger<EmailService>.Instance);
    }

    [Fact]
    public async Task SendSubmittedEmail_ShouldCallGovukNotify()
    {
        var complianceDeclaration = ComplianceDeclarationFixture.DirectProducer().Create();

        await Subject.SendSubmittedEmail(complianceDeclaration, TestContext.Current.CancellationToken);

        await GovukNotifyService
            .Received()
            .SendComplianceDeclarationSubmittedEmail(
                GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionDirectProducer,
                Arg.Is<IEnumerable<string>>(x => x.SequenceEqual(new List<string> { "email@email.com" })),
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
                ComplianceDeclarationFixture.DirectProducer().Create(),
                TestContext.Current.CancellationToken
            );

        await act.Should().NotThrowAsync();
    }
}
