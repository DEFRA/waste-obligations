using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.GovukNotify;

namespace Defra.WasteObligations.Api.Tests.Services;

public class ComplianceDeclarationCancellationReasonsTests
{
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
    public void TryGetTemplate_ShouldReturnExpectedTemplate(
        string reason,
        GovukNotifyOptions.TemplateName expectedTemplate
    )
    {
        ComplianceDeclarationCancellationReasons.TryGetTemplate(reason).Should().Be(expectedTemplate);
    }

    [Fact]
    public void TryGetTemplate_WhenReasonIsUnrecognised_ShouldReturnNull()
    {
        const string reason = "Unknown reason";

        ComplianceDeclarationCancellationReasons.TryGetTemplate(reason).Should().BeNull();
    }
}
