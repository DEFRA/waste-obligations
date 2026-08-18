using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.GovukNotify;

namespace Defra.WasteObligations.Api.Tests.Services;

public class ComplianceDeclarationCancellationReasonExtensionsTests
{
    [Theory]
    [InlineData(
        ComplianceDeclarationCancellationReason.IncorrectSigner,
        GovukNotifyOptions.TemplateName.ComplianceDeclarationCancellationNotSignedByCorrectPerson
    )]
    [InlineData(
        ComplianceDeclarationCancellationReason.RecyclingObligationsChanged,
        GovukNotifyOptions.TemplateName.ComplianceDeclarationCancellationRecyclingObligationsChanged
    )]
    [InlineData(
        ComplianceDeclarationCancellationReason.CanMeetRecyclingObligations,
        GovukNotifyOptions.TemplateName.ComplianceDeclarationCancellationCanMeetRecyclingObligations
    )]
    [InlineData(
        ComplianceDeclarationCancellationReason.RequestedToCancel,
        GovukNotifyOptions.TemplateName.ComplianceDeclarationCancellationProducerRequested
    )]
    public void TryGetTemplate_ShouldReturnExpectedTemplate(
        ComplianceDeclarationCancellationReason reason,
        GovukNotifyOptions.TemplateName expectedTemplate
    )
    {
        reason.TryGetTemplate().Should().Be(expectedTemplate);
    }

    [Fact]
    public void TryGetTemplate_WhenReasonIsUndefined_ShouldReturnNull()
    {
        const ComplianceDeclarationCancellationReason reason = (ComplianceDeclarationCancellationReason)999;

        reason.TryGetTemplate().Should().BeNull();
    }
}
