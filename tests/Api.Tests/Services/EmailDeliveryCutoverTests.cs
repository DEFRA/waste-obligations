using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.GovukNotify;
using Defra.WasteObligations.Api.Utils.Metrics;
using Defra.WasteObligations.Testing.Fixtures.AccountBackend;
using Defra.WasteObligations.Testing.Fixtures.Dtos;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using OrganisationFixture = Defra.WasteObligations.Testing.Fixtures.WasteOrganisations.OrganisationFixture;
using WasteOrganisation = Defra.WasteObligations.Api.Services.WasteOrganisations.Organisation;

namespace Defra.WasteObligations.Api.Tests.Services;

public class EmailDeliveryCutoverTests
{
    private const string CutoverUtc = "2026-10-02T12:00:00Z";
    private const string SubMillisecondCutoverUtc = "2026-10-02T12:00:00.0009Z";
    private const string FollowingMillisecondCutoverUtc = "2026-10-02T12:00:00.0019Z";
    private IGovukNotifyService GovukNotifyService { get; } = Substitute.For<IGovukNotifyService>();
    private ICancellationEmailRecipientResolver CancellationEmailRecipientResolver { get; } =
        Substitute.For<ICancellationEmailRecipientResolver>();
    private IEmailMetrics EmailMetrics { get; } = Substitute.For<IEmailMetrics>();

    public EmailDeliveryCutoverTests()
    {
        CancellationEmailRecipientResolver
            .ResolveAsync(Arg.Any<ComplianceDeclaration>(), Arg.Any<WasteOrganisation>(), Arg.Any<CancellationToken>())
            .Returns([PersonEmailFixture.Submitter()]);
    }

    [Fact]
    public void CreateEmailService_WhenCutoverInvalid_ShouldRejectConfiguration()
    {
        var act = () => CreateSubject("2026-10-02T12:00:00");

        act.Should().Throw<InvalidOperationException>();
        GovukNotifyService.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, -10000, true)]
    [InlineData(null, 0, true)]
    [InlineData(null, 10000, true)]
    [InlineData(CutoverUtc, -10000, true)]
    [InlineData(CutoverUtc, 0, false)]
    [InlineData(CutoverUtc, 10000, false)]
    [InlineData(SubMillisecondCutoverUtc, -1, true)]
    [InlineData(SubMillisecondCutoverUtc, 0, false)]
    [InlineData(SubMillisecondCutoverUtc, 8000, false)]
    [InlineData(FollowingMillisecondCutoverUtc, 9999, true)]
    [InlineData(FollowingMillisecondCutoverUtc, 10000, false)]
    public async Task SendSubmittedEmail_ShouldUseSubmittedAuditTimestamp(
        string? cutover,
        int actionTicks,
        bool shouldSend
    )
    {
        var actionTimestamp = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc).AddTicks(actionTicks);
        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(OrganisationFixture.OrganisationId)
            .With(x => x.Created, actionTimestamp.AddDays(-1))
            .With(x => x.Updated, actionTimestamp.AddDays(1))
            .With(x => x.Audit, AuditEntryFixture.Submitted(actionTimestamp))
            .Create();

        await CreateSubject(cutover)
            .SendSubmittedEmail(
                complianceDeclaration,
                OrganisationFixture.Default().Create(),
                TestContext.Current.CancellationToken
            );

        await GovukNotifyService
            .Received(shouldSend ? 1 : 0)
            .SendComplianceDeclarationSubmittedEmail(
                Arg.Any<GovukNotifyOptions.TemplateName>(),
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<string>()
            );
        EmailMetrics.Received(shouldSend ? 1 : 0).SendStarted(Arg.Any<string>(), Arg.Any<string>());
        EmailMetrics
            .Received(shouldSend ? 1 : 0)
            .SendCompleted(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<double>());
        EmailMetrics.DidNotReceive().SendFaulted(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Exception>());
    }

    [Theory]
    [InlineData(null, -10000, true)]
    [InlineData(null, 0, true)]
    [InlineData(null, 10000, true)]
    [InlineData(CutoverUtc, -10000, true)]
    [InlineData(CutoverUtc, 0, false)]
    [InlineData(CutoverUtc, 10000, false)]
    [InlineData(SubMillisecondCutoverUtc, -1, true)]
    [InlineData(SubMillisecondCutoverUtc, 0, false)]
    [InlineData(SubMillisecondCutoverUtc, 8000, false)]
    [InlineData(FollowingMillisecondCutoverUtc, 9999, true)]
    [InlineData(FollowingMillisecondCutoverUtc, 10000, false)]
    public async Task SendCancelledEmail_ShouldUseCancelledAuditTimestamp(
        string? cutover,
        int actionTicks,
        bool shouldSend
    )
    {
        var actionTimestamp = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc).AddTicks(actionTicks);
        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(OrganisationFixture.OrganisationId)
            .With(x => x.Created, actionTimestamp.AddDays(-1))
            .With(x => x.Updated, actionTimestamp.AddDays(1))
            .With(x => x.Status, ComplianceDeclarationStatus.Cancelled)
            .With(
                x => x.Audit,
                AuditEntryFixture
                    .Submitted(actionTimestamp.AddDays(-1))
                    .Concat(AuditEntryFixture.Cancelled(actionTimestamp))
            )
            .Create();

        await CreateSubject(cutover)
            .SendCancelledEmail(
                complianceDeclaration,
                OrganisationFixture.Default().Create(),
                ComplianceDeclarationCancellationReasons.ProducerRequestedToCancel,
                NotificationFixture.DirectProducerCancellationParameters(),
                TestContext.Current.CancellationToken
            );

        await GovukNotifyService
            .Received(shouldSend ? 1 : 0)
            .SendComplianceDeclarationCancelledEmail(
                Arg.Any<GovukNotifyOptions.TemplateName>(),
                Arg.Any<IEnumerable<(string Email, Dictionary<string, object> Personalisation)>>(),
                Arg.Any<string>()
            );
        await CancellationEmailRecipientResolver
            .Received(shouldSend ? 1 : 0)
            .ResolveAsync(complianceDeclaration, Arg.Any<WasteOrganisation>(), Arg.Any<CancellationToken>());
        EmailMetrics.Received(shouldSend ? 1 : 0).SendStarted(Arg.Any<string>(), Arg.Any<string>());
        EmailMetrics
            .Received(shouldSend ? 1 : 0)
            .SendCompleted(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<double>());
        EmailMetrics.DidNotReceive().SendFaulted(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Exception>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendEmail_WhenCutoverConfiguredAndActionMissing_ShouldNotSendOrFail(bool submitted)
    {
        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(OrganisationFixture.OrganisationId)
            .With(x => x.Audit, submitted ? AuditEntryFixture.Cancelled() : AuditEntryFixture.Submitted())
            .Create();
        var subject = CreateSubject(CutoverUtc);

        if (submitted)
            await subject.SendSubmittedEmail(
                complianceDeclaration,
                OrganisationFixture.Default().Create(),
                TestContext.Current.CancellationToken
            );
        else
            await subject.SendCancelledEmail(
                complianceDeclaration,
                OrganisationFixture.Default().Create(),
                ComplianceDeclarationCancellationReasons.ProducerRequestedToCancel,
                NotificationFixture.DirectProducerCancellationParameters(),
                TestContext.Current.CancellationToken
            );

        GovukNotifyService.ReceivedCalls().Should().BeEmpty();
        CancellationEmailRecipientResolver.ReceivedCalls().Should().BeEmpty();
        EmailMetrics.ReceivedCalls().Should().BeEmpty();
    }

    private EmailService CreateSubject(string? cutover) =>
        new(
            GovukNotifyService,
            CancellationEmailRecipientResolver,
            EmailMetrics,
            Options.Create(new EmailDeliveryOptions { EmailDeliveryCutoverUtc = cutover }),
            NullLogger<EmailService>.Instance
        );
}
