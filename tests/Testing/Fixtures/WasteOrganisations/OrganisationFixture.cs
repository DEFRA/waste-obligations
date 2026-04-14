using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing.Extensions;

namespace Defra.WasteObligations.Testing.Fixtures.WasteOrganisations;

public static class OrganisationFixture
{
    private static Fixture GetFixture() => new();

    public static readonly Guid OrganisationId = new("87cbc010-90f7-4c79-8bd5-099cdda2ca24");

    private static readonly string[] s_businessCountries =
    [
        BusinessCountry.England,
        BusinessCountry.NorthernIreland,
        BusinessCountry.Scotland,
        BusinessCountry.Wales,
    ];

    public static IPostprocessComposer<Organisation> Organisation()
    {
        var fixture = GetFixture();

        RegistrationFixture.ConfigureDefaults(fixture);

        return fixture.Build<Organisation>().With(x => x.BusinessCountry, () => s_businessCountries.Random());
    }

    public static IPostprocessComposer<Organisation> Default()
    {
        return Organisation()
            .With(x => x.Id, OrganisationId)
            .With(x => x.Name, "Test Name Ltd")
            .With(x => x.TradingName, "Trading Name")
            .With(x => x.BusinessCountry, BusinessCountry.England)
            .With(x => x.CompaniesHouseNumber, "12345678")
            .With(x => x.Address, AddressFixture.Default().Create())
            .With(x => x.Registrations, () => [RegistrationFixture.Default().Create()]);
    }
}
