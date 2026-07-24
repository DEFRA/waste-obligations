using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Testing.Fixtures.Dtos;

public static class UpdatePrnRequestFixture
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<UpdatePrnRequest> AddDefaults(
        this ICustomizationComposer<UpdatePrnRequest> composer
    )
    {
        return composer;
    }

    public static IPostprocessComposer<UpdatePrnRequest> Request()
    {
        return GetFixture().Build<UpdatePrnRequest>().AddDefaults();
    }

    public static IPostprocessComposer<UpdatePrnRequest> Accepted()
    {
        return Request()
            .With(x => x.Status, UpdatePrnStatus.Accepted)
            .With(x => x.User, UserFixture.Regulator().Create());
    }

    public static IPostprocessComposer<UpdatePrnRequest> Rejected()
    {
        return Request()
            .With(x => x.Status, UpdatePrnStatus.Rejected)
            .With(x => x.User, UserFixture.Regulator().Create());
    }
}
