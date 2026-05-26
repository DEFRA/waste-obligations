using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Testing.Fixtures.Entities;

namespace Defra.WasteObligations.Api.Tests.Data.Entities;

public class ComplianceDeclarationTests
{
    private DateTime UtcNow { get; } = new(2026, 5, 22, 16, 50, 0, DateTimeKind.Utc);

    [Fact]
    public void Submit_WhenNotUtcTimestamp_ShouldThrow()
    {
        var draft = CreateDraft();
        var user = UserFixture.Default().Create();

        var act = () => draft.Submit(user, DateTime.Now);

        act.Should().Throw<ArgumentException>().And.Message.Should().Be("Timestamp should be UTC");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("reason")]
    public void FromSubmittedToAccepted_ShouldBeAllowed(string? reason)
    {
        var draft = CreateDraft();
        var user = UserFixture.Default().Create();

        var submitted = draft.Submit(user, UtcNow);

        var accepted = submitted.UpdateStatus(
            ComplianceDeclarationStatus.Accepted,
            reason,
            user,
            UtcNow.AddSeconds(10)
        );

        accepted.Status.Should().Be(ComplianceDeclarationStatus.Accepted);
        accepted.Audit.Count().Should().Be(2);

        var audit = accepted.Audit.ToArray();
        audit[0].Action.Should().Be(nameof(ComplianceDeclarationStatus.Submitted));
        audit[0].User.Should().Be(user);
        audit[1].Action.Should().Be(nameof(ComplianceDeclarationStatus.Accepted));
        audit[1].User.Should().Be(user);
        audit[1].Timestamp.Should().BeAfter(audit[0].Timestamp);

        if (reason is not null)
        {
            var reasonAudit = audit[1] as ReasonAuditEntry;
            reasonAudit.Should().NotBeNull();
            reasonAudit.Reason.Should().Be(reason);
        }
    }

    [Theory]
    [InlineData(ComplianceDeclarationStatus.Submitted, ComplianceDeclarationStatus.Submitted)]
    [InlineData(ComplianceDeclarationStatus.Accepted, ComplianceDeclarationStatus.Cancelled)]
    [InlineData(ComplianceDeclarationStatus.Cancelled, ComplianceDeclarationStatus.Accepted)]
    [InlineData(ComplianceDeclarationStatus.Accepted, ComplianceDeclarationStatus.Submitted)]
    [InlineData(ComplianceDeclarationStatus.Cancelled, ComplianceDeclarationStatus.Submitted)]
    public void FromStatusToStatus_ShouldNotBeAllowed(
        ComplianceDeclarationStatus startStatus,
        ComplianceDeclarationStatus nextStatus
    )
    {
        var draft = CreateDraft() with { Status = startStatus };
        var user = UserFixture.Default().Create();

        var act = () => draft.UpdateStatus(nextStatus, null, user, UtcNow.AddSeconds(10));

        act.Should().Throw<EntityException>();
    }

    private static ComplianceDeclaration CreateDraft() =>
        new()
        {
            Organisation = OrganisationFixture.Organisation().Create(),
            ObligationStatus = "Met",
            DeclarationText = LocalizedTextFixture.Default().Create(),
            SubmitterName = "Submitter",
        };
}
