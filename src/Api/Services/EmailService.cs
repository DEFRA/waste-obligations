using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.GovukNotify;

namespace Defra.WasteObligations.Api.Services;

public class EmailService(IGovukNotifyService govukNotifyService, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendSubmittedEmail(
        ComplianceDeclaration complianceDeclaration,
        CancellationToken cancellationToken
    )
    {
        try
        {
            const string recipient = "email@email.com";

            await govukNotifyService.SendComplianceDeclarationSubmittedEmail(
                GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionDirectProducer,
                [recipient],
                new Dictionary<string, object>
                {
                    { "obligationYear", complianceDeclaration.ObligationYear },
                    { "regulator", complianceDeclaration.Organisation.Regulator },
                    { "regulatorEmail", complianceDeclaration.Organisation.RegulatorEmail },
                },
                complianceDeclaration.DeclarationText.Language
            );
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Compliance declaration submitted email could not be sent");

            // intentionally swallowed as failure to send an email should not break anything
        }
    }
}
