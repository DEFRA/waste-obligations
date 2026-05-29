using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Defra.WasteObligations.Api.Services.GovukNotify;
using Organisation = Defra.WasteObligations.Api.Services.WasteOrganisations.Organisation;

namespace Defra.WasteObligations.Api.Services;

public class EmailService(
    IGovukNotifyService govukNotifyService,
    IAccountBackendService accountBackendService,
    ILogger<EmailService> logger
) : IEmailService
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
            var entityTypeCode =
                complianceDeclaration.Organisation.RegistrationType == RegistrationType.ComplianceScheme
                    ? EntityTypeCode.CS
                    : EntityTypeCode.DR;
            var personEmails = await accountBackendService.ReadPersonEmails(
                organisation.Id,
                entityTypeCode,
                cancellationToken
            );
            var recipients = personEmails.Select(x => x.Email).ToList();

            logger.LogInformation("Found {Count} recipient(s) for submitted email", recipients.Count);

            await govukNotifyService.SendComplianceDeclarationSubmittedEmail(
                GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionDirectProducer,
                recipients,
                new Dictionary<string, object>
                {
                    { "obligationYear", complianceDeclaration.ObligationYear },
                    { "regulator", complianceDeclaration.Organisation.Regulator },
                    { "regulatorEmail", complianceDeclaration.Organisation.RegulatorEmail },
                },
                complianceDeclaration.DeclarationText.Language
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
