using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Testing.Fixtures.Dtos;

public static class OrganisationFixture
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<Organisation> Organisation()
    {
        return GetFixture().Build<Organisation>();
    }

    public static IPostprocessComposer<Organisation> DirectProducer(Guid? id = null)
    {
        return Organisation()
            .With(x => x.Id, () => id ?? Guid.NewGuid())
            .With(x => x.RegistrationType, RegistrationType.DirectProducer)
            .With(x => x.Name, "Org Name")
            .With(x => x.ReferenceNumber, "123456")
            .With(x => x.Address, AddressFixture.Default().Create())
            .With(x => x.Regulator, "Regulator")
            .With(x => x.RegulatorEmail, "regulator@email.com")
            .With(x => x.ComplianceSchemeName, (string?)null)
            .With(x => x.SchemeOperatorName, (string?)null);
    }

    public static IPostprocessComposer<Organisation> ComplianceScheme(Guid? id = null)
    {
        return Organisation()
            .With(x => x.Id, () => id ?? Guid.NewGuid())
            .With(x => x.RegistrationType, RegistrationType.ComplianceScheme)
            .With(x => x.ComplianceSchemeName, "Scheme Name")
            .With(x => x.SchemeOperatorName, "Operator Name")
            .With(x => x.ReferenceNumber, "123456")
            .With(x => x.Address, AddressFixture.Default().Create())
            .With(x => x.Regulator, "Regulator")
            .With(x => x.RegulatorEmail, "regulator@email.com")
            .With(x => x.Name, (string?)null);
    }
}
