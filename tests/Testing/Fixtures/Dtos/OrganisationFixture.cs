using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Testing.Fixtures.Dtos;

public static class OrganisationFixture
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<OrganisationRequest> Organisation()
    {
        return GetFixture().Build<OrganisationRequest>();
    }

    public static IPostprocessComposer<OrganisationRequest> DirectProducer(Guid? id = null)
    {
        return Organisation()
            .With(x => x.Id, () => id ?? Guid.NewGuid())
            .With(x => x.Name, "Org Name")
            .With(x => x.ReferenceNumber, "123456")
            .With(x => x.Address, AddressFixture.Default().Create())
            .With(x => x.Regulator, "Regulator")
            .With(x => x.ComplianceSchemeName, (string?)null)
            .With(x => x.SchemeOperatorName, (string?)null);
    }

    public static IPostprocessComposer<OrganisationRequest> ComplianceScheme(Guid? id = null)
    {
        return Organisation()
            .With(x => x.Id, () => id ?? Guid.NewGuid())
            .With(x => x.ComplianceSchemeName, "Scheme Name")
            .With(x => x.SchemeOperatorName, "Operator Name")
            .With(x => x.ReferenceNumber, "123456")
            .With(x => x.Address, AddressFixture.Default().Create())
            .With(x => x.Regulator, "Regulator")
            .With(x => x.Name, (string?)null);
    }
}
