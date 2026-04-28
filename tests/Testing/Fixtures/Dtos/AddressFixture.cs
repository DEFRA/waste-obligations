using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Testing.Fixtures.Dtos;

public static class AddressFixture
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<Address> Address()
    {
        return GetFixture().Build<Address>();
    }

    public static IPostprocessComposer<Address> Default()
    {
        return Address()
            .With(x => x.AddressLine1, "Test Name Ltd")
            .With(x => x.AddressLine2, "123 Street")
            .With(x => x.Town, "Town")
            .With(x => x.County, "County")
            .With(x => x.Postcode, "UK1")
            .With(x => x.Country, "UK");
    }
}
