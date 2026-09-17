using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.AccountBackend;
using ComplianceDeclarationStatus = Defra.WasteObligations.Api.Data.Entities.ComplianceDeclarationStatus;
using WasteOrganisationsOrganisation = Defra.WasteObligations.Api.Services.WasteOrganisations.Organisation;

namespace Defra.WasteObligations.Api.Services;

public interface ICancellationEmailRecipientResolver
{
    Task<IReadOnlyList<PersonEmail>> ResolveAsync(
        ComplianceDeclaration complianceDeclaration,
        WasteOrganisationsOrganisation organisation,
        CancellationToken cancellationToken
    );
}

public class CancellationEmailRecipientResolver(
    IAccountBackendService accountBackendService,
    ILogger<CancellationEmailRecipientResolver> logger
) : ICancellationEmailRecipientResolver
{
    public const string ApprovedPersonServiceRole = "Approved Person";

    public async Task<IReadOnlyList<PersonEmail>> ResolveAsync(
        ComplianceDeclaration complianceDeclaration,
        WasteOrganisationsOrganisation organisation,
        CancellationToken cancellationToken
    )
    {
        var accountOrganisationId = await ResolveAccountOrganisationId(
            complianceDeclaration.Organisation.RegistrationType,
            organisation,
            cancellationToken
        );
        if (accountOrganisationId is null)
        {
            return [];
        }

        var organisationWithPersons = await accountBackendService.ReadOrganisationWithPersons(
            accountOrganisationId.Value,
            cancellationToken
        );
        if (organisationWithPersons is null)
        {
            logger.LogWarning(
                "Cancellation email was not sent because Account returned no organisation-with-persons data for Account organisation {AccountOrganisationId} (Waste Organisations organisation {WasteOrganisationId}, registration type {RegistrationType})",
                accountOrganisationId.Value,
                organisation.Id,
                complianceDeclaration.Organisation.RegistrationType
            );

            return [];
        }

        var recipients = new List<PersonEmail>();

        var submitter = ResolveSubmitter(complianceDeclaration, organisationWithPersons);
        if (submitter is not null)
        {
            recipients.Add(submitter);
        }
        else
        {
            logger.LogInformation(
                "Cancellation email submitter recipient was not resolved for Waste Organisations organisation {WasteOrganisationId}; the submitted audit user is missing, not on the Account organisation, or lacks a complete name",
                organisation.Id
            );
        }

        var primaryContact = ResolvePrimaryContact(organisationWithPersons);
        if (primaryContact is not null)
        {
            recipients.Add(primaryContact);
        }
        else if (submitter is not null)
        {
            logger.LogWarning(
                "Primary contact email was not found for Account organisation {AccountOrganisationId}; cancellation email will be sent to submitter only",
                accountOrganisationId.Value
            );
        }

        var resolvedRecipients = recipients
            .DistinctBy(recipient => recipient.Email, StringComparer.OrdinalIgnoreCase)
            .OrderBy(recipient => recipient.Email, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        logger.LogInformation(
            "Resolved {RecipientCount} cancellation email recipient(s) for Waste Organisations organisation {WasteOrganisationId} using Account organisation {AccountOrganisationId}",
            resolvedRecipients.Length,
            organisation.Id,
            accountOrganisationId.Value
        );

        return resolvedRecipients;
    }

    private async Task<Guid?> ResolveAccountOrganisationId(
        RegistrationType registrationType,
        WasteOrganisationsOrganisation organisation,
        CancellationToken cancellationToken
    )
    {
        if (registrationType is RegistrationType.DirectProducer)
        {
            logger.LogDebug(
                "Using Waste Organisations organisation {OrganisationId} as the Account organisation ID for a direct producer cancellation email",
                organisation.Id
            );

            return organisation.Id;
        }

        if (string.IsNullOrWhiteSpace(organisation.CompaniesHouseNumber))
        {
            logger.LogWarning(
                "Cancellation email was not sent because compliance scheme operator {WasteOrganisationId} has no Companies House number to resolve the Account organisation ID",
                organisation.Id
            );

            return null;
        }

        var matches = (
            await accountBackendService.SearchOrganisationsByCompaniesHouseNumbers(
                [organisation.CompaniesHouseNumber],
                cancellationToken
            )
        )
            .Where(x => x.IsComplianceScheme && !string.IsNullOrWhiteSpace(x.ExternalId))
            .ToArray();

        if (matches.Length == 0)
        {
            logger.LogWarning(
                "Cancellation email was not sent because Account returned no compliance scheme organisation for Companies House number {CompaniesHouseNumber} (Waste Organisations organisation {WasteOrganisationId})",
                organisation.CompaniesHouseNumber,
                organisation.Id
            );

            return null;
        }

        if (matches.Length > 1)
        {
            logger.LogWarning(
                "Cancellation email was not sent because Account returned {MatchCount} compliance scheme organisations for Companies House number {CompaniesHouseNumber} (Waste Organisations organisation {WasteOrganisationId})",
                matches.Length,
                organisation.CompaniesHouseNumber,
                organisation.Id
            );

            return null;
        }

        if (!Guid.TryParse(matches[0].ExternalId, out var accountOrganisationId))
        {
            logger.LogWarning(
                "Cancellation email was not sent because Account returned an invalid external ID '{ExternalId}' for Companies House number {CompaniesHouseNumber}",
                matches[0].ExternalId,
                organisation.CompaniesHouseNumber
            );

            return null;
        }

        logger.LogInformation(
            "Resolved Account organisation {AccountOrganisationId} from Companies House number {CompaniesHouseNumber} for Waste Organisations compliance scheme {WasteOrganisationId}",
            accountOrganisationId,
            organisation.CompaniesHouseNumber,
            organisation.Id
        );

        return accountOrganisationId;
    }

    public static PersonEmail? ResolveSubmitter(
        ComplianceDeclaration complianceDeclaration,
        OrganisationWithPersons? organisationWithPersons
    )
    {
        var submittedAuditEntry = complianceDeclaration.Audit.FirstOrDefault(entry =>
            entry.Action == nameof(ComplianceDeclarationStatus.Submitted)
        );
        var submitter = submittedAuditEntry?.User;
        var email = submitter?.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var matchedPerson = organisationWithPersons?.Persons.FirstOrDefault(person =>
            (!string.IsNullOrWhiteSpace(submitter!.Id) && person.UserId?.ToString() == submitter.Id)
            || string.Equals(person.Email, email, StringComparison.OrdinalIgnoreCase)
        );

        if (
            matchedPerson is null
            || string.IsNullOrWhiteSpace(matchedPerson.FirstName)
            || string.IsNullOrWhiteSpace(matchedPerson.LastName)
        )
        {
            return null;
        }

        return new PersonEmail
        {
            FirstName = matchedPerson.FirstName,
            LastName = matchedPerson.LastName,
            Email = email,
        };
    }

    public static PersonEmail? ResolvePrimaryContact(OrganisationWithPersons? organisationWithPersons)
    {
        var primaryContact = organisationWithPersons?.Persons.FirstOrDefault(person =>
            string.Equals(person.ServiceRole, ApprovedPersonServiceRole, StringComparison.Ordinal)
        );

        if (primaryContact is null)
        {
            return null;
        }

        var email = primaryContact.Email?.Trim();
        if (
            string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(primaryContact.FirstName)
            || string.IsNullOrWhiteSpace(primaryContact.LastName)
        )
        {
            return null;
        }

        return new PersonEmail
        {
            FirstName = primaryContact.FirstName,
            LastName = primaryContact.LastName,
            Email = email,
        };
    }
}
