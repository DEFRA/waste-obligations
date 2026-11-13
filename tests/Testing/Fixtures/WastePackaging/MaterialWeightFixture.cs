using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Api.Services.WastePackaging;

namespace Defra.WasteObligations.Testing.Fixtures.WastePackaging;

public static class MaterialWeightFixture
{
    public static IPostprocessComposer<MaterialWeight> Default() =>
        new Fixture()
            .Build<MaterialWeight>()
            .With(x => x.MaterialCode, ObligationMaterials.Plastic)
            .With(x => x.Kilograms, 10000L);
}
