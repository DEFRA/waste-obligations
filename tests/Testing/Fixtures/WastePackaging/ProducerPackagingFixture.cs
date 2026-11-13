using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Services.WastePackaging;

namespace Defra.WasteObligations.Testing.Fixtures.WastePackaging;

public static class ProducerPackagingFixture
{
    public static IPostprocessComposer<ProducerPackaging> Default() =>
        new Fixture()
            .Build<ProducerPackaging>()
            .With(x => x.ProducerOrganisationId, Guid.NewGuid)
            .With(x => x.Events, [])
            .With(x => x.Materials, [MaterialWeightFixture.Default().Create()]);
}
