using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.AccountBackend;
using ComplianceDeclarationStatus = Defra.WasteObligations.Api.Data.Entities.ComplianceDeclarationStatus;

namespace Defra.WasteObligations.Api.Services;

public interface ICancellationEmailRecipientResolver
{
    Task<IReadOnlyList<PersonEmail>> ResolveAsync(
        ComplianceDeclaration complianceDeclaration,
        Guid organisationId,
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
        Guid organisationId,
        CancellationToken cancellationToken
    )
    {
        var organisationWithPersons = await accountBackendService.ReadOrganisationWithPersons(
            organisationId,
            cancellationToken
        );
        var recipients = new List<PersonEmail>();

        var submitter = ResolveSubmitter(complianceDeclaration, organisationWithPersons);
        if (submitter is not null)
        {
            recipients.Add(submitter);
        }

        var primaryContact = ResolvePrimaryContact(organisationWithPersons);
        if (primaryContact is not null)
        {
            recipients.Add(primaryContact);
        }
        else if (submitter is not null)
        {
            logger.LogWarning(
                "Primary contact email was not found for organisation {OrganisationId}; cancellation email will be sent to submitter only",
                organisationId
            );
        }

        return recipients
            .DistinctBy(recipient => recipient.Email, StringComparer.OrdinalIgnoreCase)
            .OrderBy(recipient => recipient.Email, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
