using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Defra.WasteObligations.Api.Services.GovukNotify;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Api.Utils.Metrics;
using Defra.WasteObligations.Testing.Fixtures.AccountBackend;
using Defra.WasteObligations.Testing.Fixtures.Dtos;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OrganisationFixture = Defra.WasteObligations.Testing.Fixtures.WasteOrganisations.OrganisationFixture;

namespace Defra.WasteObligations.Api.Tests.Services;

public class EmailServiceTests
{
    private IGovukNotifyService GovukNotifyService { get; } = Substitute.For<IGovukNotifyService>();
    private ICancellationEmailRecipientResolver CancellationEmailRecipientResolver { get; } =
        Substitute.For<ICancellationEmailRecipientResolver>();
    private IEmailMetrics EmailMetrics { get; } = Substitute.For<IEmailMetrics>();
    private EmailService Subject { get; }

    public EmailServiceTests()
    {
        CancellationEmailRecipientResolver
            .ResolveAsync(Arg.Any<ComplianceDeclaration>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([PersonEmailFixture.Submitter()]);

        Subject = new EmailService(
            GovukNotifyService,
            CancellationEmailRecipientResolver,
            EmailMetrics,
            NullLogger<EmailService>.Instance
        );
    }

    [Theory]
    [InlineData(
        true,
        BusinessCountry.England,
        GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionDirectProducer,
        nameof(GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionDirectProducer),
        "en"
    )]
    [InlineData(
        false,
        BusinessCountry.England,
        GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionComplianceScheme,
        nameof(GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionComplianceScheme),
        "en"
    )]
    public async Task SendSubmittedEmail_ShouldCallGovukNotify(
        bool directProducer,
        string businessCountry,
        GovukNotifyOptions.TemplateName expectedTemplate,
        string expectedMetricTemplateName,
        string expectedLanguage
    )
    {
        var complianceDeclaration = directProducer
            ? ComplianceDeclarationFixture.DirectProducer(OrganisationFixture.OrganisationId).Create()
            : ComplianceDeclarationFixture.ComplianceScheme(OrganisationFixture.OrganisationId).Create();
        var organisation = OrganisationFixture.Default().With(x => x.BusinessCountry, businessCountry).Create();

        await Subject.SendSubmittedEmail(complianceDeclaration, organisation, TestContext.Current.CancellationToken);

        await GovukNotifyService
            .Received()
            .SendComplianceDeclarationSubmittedEmail(
                expectedTemplate,
                Arg.Is<IEnumerable<string>>(x => x.Single() == "submitter@email.com"),
                Arg.Is<Dictionary<string, object>>(x =>
                    x.Count == 5
                    && (int)x["obligationYear"] == complianceDeclaration.ObligationYear
                    && (string)x["regulatorLeading"] == $"The {complianceDeclaration.Organisation.Regulator}"
                    && (string)x["regulatorInline"] == $"the {complianceDeclaration.Organisation.Regulator}"
                    && (string)x["regulatorEmail"] == complianceDeclaration.Organisation.RegulatorEmail
                    && (string)x["user"] == "Submitter Name"
                ),
                expectedLanguage
            );
        EmailMetrics.Received(1).SendStarted(expectedMetricTemplateName, expectedLanguage);
        EmailMetrics.Received(1).SendCompleted(expectedMetricTemplateName, expectedLanguage, Arg.Any<double>());
    }

    [Theory]
    [InlineData(BusinessCountry.England, "The Regulator", "the Regulator")]
    [InlineData(BusinessCountry.NorthernIreland, "The Regulator", "the Regulator")]
    [InlineData(BusinessCountry.Scotland, "The Regulator", "the Regulator")]
    [InlineData(BusinessCountry.Wales, "Regulator", "Regulator")]
    public async Task SendSubmittedEmail_ShouldUseRegulatorPrefixForOrganisationBusinessCountry(
        string businessCountry,
        string expectedRegulatorLeading,
        string expectedRegulatorInline
    )
    {
        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(OrganisationFixture.OrganisationId)
            .Create();
        var organisation = OrganisationFixture.Default().With(x => x.BusinessCountry, businessCountry).Create();

        await Subject.SendSubmittedEmail(complianceDeclaration, organisation, TestContext.Current.CancellationToken);

        await GovukNotifyService
            .Received()
            .SendComplianceDeclarationSubmittedEmail(
                Arg.Any<GovukNotifyOptions.TemplateName>(),
                Arg.Any<IEnumerable<string>>(),
                Arg.Is<Dictionary<string, object>>(x =>
                    (string)x["regulatorLeading"] == expectedRegulatorLeading
                    && (string)x["regulatorInline"] == expectedRegulatorInline
                ),
                Arg.Any<string>()
            );
    }

    [Theory]
    [InlineData(BusinessCountry.England, "en")]
    [InlineData(BusinessCountry.NorthernIreland, "en")]
    [InlineData(BusinessCountry.Scotland, "en")]
    [InlineData(BusinessCountry.Wales, "cy")]
    public async Task SendSubmittedEmail_ShouldUseLanguageForOrganisationBusinessCountry(
        string businessCountry,
        string expectedLanguage
    )
    {
        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(OrganisationFixture.OrganisationId)
            .Create();
        var organisation = OrganisationFixture.Default().With(x => x.BusinessCountry, businessCountry).Create();

        await Subject.SendSubmittedEmail(complianceDeclaration, organisation, TestContext.Current.CancellationToken);

        await GovukNotifyService
            .Received()
            .SendComplianceDeclarationSubmittedEmail(
                Arg.Any<GovukNotifyOptions.TemplateName>(),
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<Dictionary<string, object>>(),
                expectedLanguage
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
        EmailMetrics
            .Received(1)
            .SendFaulted(
                nameof(GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionDirectProducer),
                "en",
                Arg.Any<Exception>()
            );
        EmailMetrics
            .Received(1)
            .SendCompleted(
                nameof(GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionDirectProducer),
                "en",
                Arg.Any<double>()
            );
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
        EmailMetrics.DidNotReceive().SendStarted(Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory]
    [InlineData(
        ComplianceDeclarationCancellationReasons.NotSignedByCorrectPerson,
        GovukNotifyOptions.TemplateName.ComplianceDeclarationCancellationNotSignedByCorrectPerson
    )]
    [InlineData(
        ComplianceDeclarationCancellationReasons.RecyclingObligationsChanged,
        GovukNotifyOptions.TemplateName.ComplianceDeclarationCancellationRecyclingObligationsChanged
    )]
    [InlineData(
        ComplianceDeclarationCancellationReasons.ProducerCanMeetRecyclingObligations,
        GovukNotifyOptions.TemplateName.ComplianceDeclarationCancellationCanMeetRecyclingObligations
    )]
    [InlineData(
        ComplianceDeclarationCancellationReasons.ComplianceSchemeCanMeetRecyclingObligations,
        GovukNotifyOptions.TemplateName.ComplianceDeclarationCancellationCanMeetRecyclingObligations
    )]
    [InlineData(
        ComplianceDeclarationCancellationReasons.ProducerRequestedToCancel,
        GovukNotifyOptions.TemplateName.ComplianceDeclarationCancellationProducerRequested
    )]
    [InlineData(
        ComplianceDeclarationCancellationReasons.ComplianceSchemeRequestedToCancel,
        GovukNotifyOptions.TemplateName.ComplianceDeclarationCancellationProducerRequested
    )]
    public async Task SendCancelledEmail_ShouldCallGovukNotify(
        string reason,
        GovukNotifyOptions.TemplateName expectedTemplate
    )
    {
        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(OrganisationFixture.OrganisationId)
            .Create();
        var organisation = OrganisationFixture.Default().Create();

        await Subject.SendCancelledEmail(
            complianceDeclaration,
            organisation,
            reason,
            NotificationFixture.DirectProducerCancellationParameters(),
            TestContext.Current.CancellationToken
        );

        await GovukNotifyService
            .Received()
            .SendComplianceDeclarationCancelledEmail(
                expectedTemplate,
                Arg.Is<IEnumerable<(string Email, Dictionary<string, object> Personalisation)>>(x =>
                    x.Count() == 1
                    && x.First().Email == PersonEmailFixture.Submitter().Email
                    && x.First().Personalisation.Count == 8
                    && (string)x.First().Personalisation["certOrStatement"] == "certificate"
                    && (string)x.First().Personalisation["certOrStatement_cy"] == "tystysgrif"
                    && (int)x.First().Personalisation["year"] == complianceDeclaration.ObligationYear
                    && (string)x.First().Personalisation["regulator"] == complianceDeclaration.Organisation.Regulator
                    && (string)x.First().Personalisation["regulator_cy"] == complianceDeclaration.Organisation.Regulator
                    && (string)x.First().Personalisation["regulatorEmail"]
                        == complianceDeclaration.Organisation.RegulatorEmail
                    && (string)x.First().Personalisation["firstName"] == PersonEmailFixture.Submitter().FirstName
                    && (string)x.First().Personalisation["lastName"] == PersonEmailFixture.Submitter().LastName
                ),
                "en"
            );
        await CancellationEmailRecipientResolver
            .Received(1)
            .ResolveAsync(complianceDeclaration, OrganisationFixture.OrganisationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendCancelledEmail_WhenComplianceScheme_ShouldPassThroughCallerParameters()
    {
        var complianceDeclaration = ComplianceDeclarationFixture
            .ComplianceScheme(OrganisationFixture.OrganisationId)
            .Create();
        var organisation = OrganisationFixture.Default().Create();

        await Subject.SendCancelledEmail(
            complianceDeclaration,
            organisation,
            ComplianceDeclarationCancellationReasons.ProducerRequestedToCancel,
            NotificationFixture.ComplianceSchemeCancellationParameters(),
            TestContext.Current.CancellationToken
        );

        await GovukNotifyService
            .Received()
            .SendComplianceDeclarationCancelledEmail(
                Arg.Any<GovukNotifyOptions.TemplateName>(),
                Arg.Is<IEnumerable<(string Email, Dictionary<string, object> Personalisation)>>(x =>
                    (string)x.Single().Personalisation["certOrStatement"] == "statement"
                    && (string)x.Single().Personalisation["certOrStatement_cy"] == "datganiad"
                ),
                Arg.Any<string>()
            );
        await CancellationEmailRecipientResolver
            .Received(1)
            .ResolveAsync(complianceDeclaration, OrganisationFixture.OrganisationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendCancelledEmail_WhenWelshOrganisation_ShouldUseWelshTemplate()
    {
        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(OrganisationFixture.OrganisationId)
            .Create();
        var organisation = OrganisationFixture.Default().With(x => x.BusinessCountry, BusinessCountry.Wales).Create();

        await Subject.SendCancelledEmail(
            complianceDeclaration,
            organisation,
            ComplianceDeclarationCancellationReasons.ProducerRequestedToCancel,
            NotificationFixture.DirectProducerCancellationParameters(),
            TestContext.Current.CancellationToken
        );

        await GovukNotifyService
            .Received()
            .SendComplianceDeclarationCancelledEmail(
                Arg.Any<GovukNotifyOptions.TemplateName>(),
                Arg.Is<IEnumerable<(string Email, Dictionary<string, object> Personalisation)>>(x =>
                    (string)x.Single().Personalisation["certOrStatement"] == "certificate"
                    && (string)x.Single().Personalisation["certOrStatement_cy"] == "tystysgrif"
                    && (string)x.Single().Personalisation["regulator_cy"] == "Regulator"
                ),
                "cy"
            );
    }

    [Fact]
    public async Task SendCancelledEmail_WhenWelshComplianceScheme_ShouldUseWelshTemplate()
    {
        var complianceDeclaration = ComplianceDeclarationFixture
            .ComplianceScheme(OrganisationFixture.OrganisationId)
            .Create();
        var organisation = OrganisationFixture.Default().With(x => x.BusinessCountry, BusinessCountry.Wales).Create();

        await Subject.SendCancelledEmail(
            complianceDeclaration,
            organisation,
            ComplianceDeclarationCancellationReasons.ProducerRequestedToCancel,
            NotificationFixture.ComplianceSchemeCancellationParameters(),
            TestContext.Current.CancellationToken
        );

        await GovukNotifyService
            .Received()
            .SendComplianceDeclarationCancelledEmail(
                Arg.Any<GovukNotifyOptions.TemplateName>(),
                Arg.Is<IEnumerable<(string Email, Dictionary<string, object> Personalisation)>>(x =>
                    (string)x.Single().Personalisation["certOrStatement"] == "statement"
                    && (string)x.Single().Personalisation["certOrStatement_cy"] == "datganiad"
                ),
                "cy"
            );
    }

    [Fact]
    public async Task SendCancelledEmail_ShouldApplyCallerParameterOverrides()
    {
        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(OrganisationFixture.OrganisationId)
            .Create();
        var organisation = OrganisationFixture.Default().Create();
        const string overrideValue = "custom Welsh regulator";

        await Subject.SendCancelledEmail(
            complianceDeclaration,
            organisation,
            ComplianceDeclarationCancellationReasons.ProducerRequestedToCancel,
            new Dictionary<string, string> { ["regulator_cy"] = overrideValue },
            TestContext.Current.CancellationToken
        );

        await GovukNotifyService
            .Received()
            .SendComplianceDeclarationCancelledEmail(
                Arg.Any<GovukNotifyOptions.TemplateName>(),
                Arg.Is<IEnumerable<(string Email, Dictionary<string, object> Personalisation)>>(x =>
                    (string)x.Single().Personalisation["regulator_cy"] == overrideValue
                ),
                Arg.Any<string>()
            );
    }

    [Fact]
    public async Task SendCancelledEmail_WhenReasonHasNoTemplate_ShouldNotCallGovukNotify()
    {
        const string reason = "Unknown reason";

        await Subject.SendCancelledEmail(
            ComplianceDeclarationFixture.DirectProducer(OrganisationFixture.OrganisationId).Create(),
            OrganisationFixture.Default().Create(),
            reason,
            NotificationFixture.DirectProducerCancellationParameters(),
            TestContext.Current.CancellationToken
        );

        await GovukNotifyService
            .DidNotReceive()
            .SendComplianceDeclarationCancelledEmail(
                Arg.Any<GovukNotifyOptions.TemplateName>(),
                Arg.Any<IEnumerable<(string Email, Dictionary<string, object> Personalisation)>>(),
                Arg.Any<string>()
            );
        EmailMetrics.DidNotReceive().SendStarted(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SendCancelledEmail_WhenNoRecipients_ShouldNotCallGovukNotify()
    {
        CancellationEmailRecipientResolver
            .ResolveAsync(Arg.Any<ComplianceDeclaration>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await Subject.SendCancelledEmail(
            ComplianceDeclarationFixture.DirectProducer(OrganisationFixture.OrganisationId).Create(),
            OrganisationFixture.Default().Create(),
            ComplianceDeclarationCancellationReasons.ProducerRequestedToCancel,
            NotificationFixture.DirectProducerCancellationParameters(),
            TestContext.Current.CancellationToken
        );

        await GovukNotifyService
            .DidNotReceive()
            .SendComplianceDeclarationCancelledEmail(
                Arg.Any<GovukNotifyOptions.TemplateName>(),
                Arg.Any<IEnumerable<(string Email, Dictionary<string, object> Personalisation)>>(),
                Arg.Any<string>()
            );
    }

    [Fact]
    public async Task SendCancelledEmail_WhenGovukNotifyThrows_IsSwallowed()
    {
        GovukNotifyService
            .SendComplianceDeclarationCancelledEmail(
                Arg.Any<GovukNotifyOptions.TemplateName>(),
                Arg.Any<IEnumerable<(string Email, Dictionary<string, object> Personalisation)>>(),
                Arg.Any<string>()
            )
            .ThrowsAsync(new Exception("BOOM!"));

        var act = () =>
            Subject.SendCancelledEmail(
                ComplianceDeclarationFixture.DirectProducer(OrganisationFixture.OrganisationId).Create(),
                OrganisationFixture.Default().Create(),
                ComplianceDeclarationCancellationReasons.ProducerRequestedToCancel,
                NotificationFixture.DirectProducerCancellationParameters(),
                TestContext.Current.CancellationToken
            );

        await act.Should().NotThrowAsync();
        EmailMetrics
            .Received(1)
            .SendFaulted(
                nameof(GovukNotifyOptions.TemplateName.ComplianceDeclarationCancellationProducerRequested),
                "en",
                Arg.Any<Exception>()
            );
    }

    [Fact]
    public async Task SendCancelledEmail_WhenOrganisationIdMismatch_ShouldThrow()
    {
        var act = () =>
            Subject.SendCancelledEmail(
                ComplianceDeclarationFixture.DirectProducer().Create(),
                OrganisationFixture.Default().Create(),
                ComplianceDeclarationCancellationReasons.ProducerRequestedToCancel,
                NotificationFixture.DirectProducerCancellationParameters(),
                TestContext.Current.CancellationToken
            );

        await act.Should().ThrowAsync<InvalidOperationException>();
        EmailMetrics.DidNotReceive().SendStarted(Arg.Any<string>(), Arg.Any<string>());
    }
}
