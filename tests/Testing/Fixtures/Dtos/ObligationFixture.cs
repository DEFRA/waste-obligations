using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Testing.Extensions;

namespace Defra.WasteObligations.Testing.Fixtures.Dtos;

public static class ObligationFixture
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<Obligation> Obligation()
    {
        return GetFixture()
            .Build<Obligation>()
            .With(x => x.Material, () => Material.All.Random())
            .With(x => x.RecyclingTarget, () => (decimal)Random.Shared.NextDouble())
            .With(x => x.Status, () => ObligationStatus.All.Random());
    }

    public static IPostprocessComposer<Obligation> Default()
    {
        return Obligation()
            .With(x => x.Material, Material.Plastic)
            .With(x => x.RecyclingTarget, 0.75m)
            .With(x => x.Tonnages, ObligationTonnagesFixture.Default().Create())
            .With(x => x.Status, ObligationStatus.NoDataYet);
    }
}
