using Defra.WasteObligations.Api.Services.AccountBackend;

namespace Defra.WasteObligations.Testing.Fixtures.AccountBackend;

public static class PersonEmailFixture
{
    public static PersonEmail Submitter() =>
        new()
        {
            FirstName = "Submitter",
            LastName = "Name",
            Email = "submitter@email.com",
        };

    public static PersonEmail PrimaryContact() =>
        new()
        {
            FirstName = "Approved",
            LastName = "Person",
            Email = "approved-person@email.com",
        };

    public static PersonEmail[] CancellationRecipients() => [Submitter(), PrimaryContact()];
}
