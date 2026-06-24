using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.GovukNotify;
using Organisation = Defra.WasteObligations.Api.Services.WasteOrganisations.Organisation;

namespace Defra.WasteObligations.Api.Services;

public class EmailService(IGovukNotifyService govukNotifyService, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendSubmittedEmail(
        ComplianceDeclaration complianceDeclaration,
        Organisation organisation,
        CancellationToken cancellationToken
    )
    {
        if (complianceDeclaration.Organisation.Id != organisation.Id)
            throw new InvalidOperationException("Organisations do not match");

        try
        {
            var recipient = complianceDeclaration
                .Audit.First(x => x.Action == nameof(ComplianceDeclarationStatus.Submitted))
                .User.Email;

            logger.LogInformation("Sending submitted email to submitter email address");

            await govukNotifyService.SendComplianceDeclarationSubmittedEmail(
                GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionDirectProducer,
                [recipient],
                new Dictionary<string, object>
                {
                    { "obligationYear", complianceDeclaration.ObligationYear },
                    { "regulator", complianceDeclaration.Organisation.Regulator },
                    { "regulatorEmail", complianceDeclaration.Organisation.RegulatorEmail },
                },
                "en"
            );

            logger.LogInformation("Sent submitted email");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Submitted email could not be sent");

            // intentionally swallowed as failure to send an email should not break anything
        }
    }
}
