using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.AccountBackend;

namespace Defra.WasteObligations.Testing.Fixtures.AccountBackend;

public static class OrganisationWithPersonsFixture
{
    public static OrganisationWithPersons CancellationRecipients() =>
        new()
        {
            Persons =
            [
                new OrganisationPerson
                {
                    UserId = Guid.Parse("e72be574-8b5b-4836-af47-dd7e0c0d1d87"),
                    FirstName = "Submitter",
                    LastName = "Name",
                    Email = "submitter@email.com",
                    ServiceRole = "Delegated Person",
                },
                new OrganisationPerson
                {
                    FirstName = "Approved",
                    LastName = "Person",
                    Email = "approved-person@email.com",
                    ServiceRole = CancellationEmailRecipientResolver.ApprovedPersonServiceRole,
                },
                new OrganisationPerson
                {
                    FirstName = "Primary",
                    LastName = "Contact",
                    Email = "primary.contact@email.com",
                    ServiceRole = "Delegated Person",
                },
            ],
        };

    public static OrganisationWithPersons SubmitterOnly() =>
        new()
        {
            Persons =
            [
                new OrganisationPerson
                {
                    UserId = Guid.Parse("e72be574-8b5b-4836-af47-dd7e0c0d1d87"),
                    FirstName = "Submitter",
                    LastName = "Name",
                    Email = "submitter@email.com",
                    ServiceRole = "Delegated Person",
                },
            ],
        };

    public static OrganisationWithPersons SubmitterMatchesApprovedPerson() =>
        new()
        {
            Persons =
            [
                new OrganisationPerson
                {
                    UserId = Guid.Parse("e72be574-8b5b-4836-af47-dd7e0c0d1d87"),
                    FirstName = "Submitter",
                    LastName = "Name",
                    Email = "submitter@email.com",
                    ServiceRole = CancellationEmailRecipientResolver.ApprovedPersonServiceRole,
                },
            ],
        };
}
