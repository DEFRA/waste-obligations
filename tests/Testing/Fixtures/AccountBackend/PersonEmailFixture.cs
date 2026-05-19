using Defra.WasteObligations.Api.Services.AccountBackend;

namespace Defra.WasteObligations.Testing.Fixtures.AccountBackend;

public static class PersonEmailFixture
{
    public static PersonEmail Default() =>
        new()
        {
            FirstName = "First",
            LastName = "Last",
            Email = "first.last@example.com",
        };
}
