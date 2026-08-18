using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.GovukNotify;

namespace Defra.WasteObligations.Api.Services;

public static class ComplianceDeclarationCancellationReasonExtensions
{
    public static GovukNotifyOptions.TemplateName? TryGetTemplate(
        this ComplianceDeclarationCancellationReason reason
    ) =>
        reason switch
        {
            ComplianceDeclarationCancellationReason.IncorrectSigner => GovukNotifyOptions
                .TemplateName
                .ComplianceDeclarationCancellationNotSignedByCorrectPerson,
            ComplianceDeclarationCancellationReason.RecyclingObligationsChanged => GovukNotifyOptions
                .TemplateName
                .ComplianceDeclarationCancellationRecyclingObligationsChanged,
            ComplianceDeclarationCancellationReason.CanMeetRecyclingObligations => GovukNotifyOptions
                .TemplateName
                .ComplianceDeclarationCancellationCanMeetRecyclingObligations,
            ComplianceDeclarationCancellationReason.RequestedToCancel => GovukNotifyOptions
                .TemplateName
                .ComplianceDeclarationCancellationProducerRequested,
            _ => null,
        };
}
