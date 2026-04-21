using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Testing.Fixtures.Entities;

public static class ObligationTonnagesFixture
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<ObligationTonnages> Tonnages()
    {
        return GetFixture().Build<ObligationTonnages>();
    }

    public static IPostprocessComposer<ObligationTonnages> Default()
    {
        return Tonnages()
            .With(x => x.Material, 100)
            .With(x => x.AwaitingAcceptance, 10)
            .With(x => x.Accepted, 2)
            .With(x => x.Outstanding, 20)
            .With(x => x.Obligated, 5);
    }
}
