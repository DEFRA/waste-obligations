using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;

namespace Defra.WasteObligations.Testing.Fixtures.PrnCommonBackend;

public static class ObligationsFixture
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<Obligations> Obligations()
    {
        return GetFixture().Build<Obligations>();
    }

    public static IPostprocessComposer<Obligations> Default()
    {
        return Obligations()
            .With(x => x.NumberOfPrnsAwaitingAcceptance, 3)
            .With(x => x.ObligationData, [ObligationFixture.Default().Create()]);
    }
}
