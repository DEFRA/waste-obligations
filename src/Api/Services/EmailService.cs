using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.GovukNotify;
using Defra.WasteObligations.Api.Utils.Metrics;
using BusinessCountry = Defra.WasteObligations.Api.Services.WasteOrganisations.BusinessCountry;
using Organisation = Defra.WasteObligations.Api.Services.WasteOrganisations.Organisation;

namespace Defra.WasteObligations.Api.Services;

public class EmailService(
    IGovukNotifyService govukNotifyService,
    IEmailMetrics emailMetrics,
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

        var template =
            complianceDeclaration.Organisation.RegistrationType is RegistrationType.ComplianceScheme
                ? GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionComplianceScheme
                : GovukNotifyOptions.TemplateName.ComplianceDeclarationSubmissionDirectProducer;
        var templateName = template.ToString();
        var language = organisation.BusinessCountry == BusinessCountry.Wales ? "cy" : "en";
        var startingTimestamp = TimeProvider.System.GetTimestamp();
        emailMetrics.SendStarted(templateName, language);

        try
        {
            var submittedAuditEntry = complianceDeclaration.Audit.First(x =>
                x.Action == nameof(ComplianceDeclarationStatus.Submitted)
            );
            var personalisation = new Dictionary<string, object>
            {
                { "obligationYear", complianceDeclaration.ObligationYear },
                { "regulator", complianceDeclaration.Organisation.Regulator },
                { "regulatorEmail", complianceDeclaration.Organisation.RegulatorEmail },
                { "user", submittedAuditEntry.User.Name },
            };

            logger.LogInformation("Sending submitted email to submitter email address");

            await govukNotifyService.SendComplianceDeclarationSubmittedEmail(
                template,
                [submittedAuditEntry.User.Email],
                personalisation,
                language
            );

            logger.LogInformation("Sent submitted email");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Submitted email could not be sent");
            emailMetrics.SendFaulted(templateName, language, exception);

            // intentionally swallowed as failure to send an email should not break anything
        }
        finally
        {
            emailMetrics.SendCompleted(
                templateName,
                language,
                TimeProvider.System.GetElapsedTime(startingTimestamp).TotalMilliseconds
            );
        }
    }
}
