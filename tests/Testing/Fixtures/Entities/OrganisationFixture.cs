using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Data.Entities;

// ReSharper disable ConvertClosureToMethodGroup

namespace Defra.WasteObligations.Testing.Fixtures.Entities;

public static class OrganisationFixture
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<Organisation> Organisation()
    {
        return GetFixture().Build<Organisation>();
    }

    public static IPostprocessComposer<Organisation> DirectProducer()
    {
        return Organisation()
            .With(x => x.Name, "Org Name")
            .With(x => x.ReferenceNumber, "123456")
            .With(x => x.Address, AddressFixture.Default().Create())
            .With(x => x.Regulator, "Regulator")
            .With(x => x.ComplianceSchemeName, (string?)null)
            .With(x => x.SchemeOperatorName, (string?)null);
    }

    public static IPostprocessComposer<Organisation> ComplianceScheme()
    {
        return Organisation()
            .With(x => x.ComplianceSchemeName, "Scheme Name")
            .With(x => x.SchemeOperatorName, "Operator Name")
            .With(x => x.ReferenceNumber, "123456")
            .With(x => x.Address, AddressFixture.Default().Create())
            .With(x => x.Regulator, "Regulator")
            .With(x => x.Name, (string?)null);
    }
}
