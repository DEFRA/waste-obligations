using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Testing.Fixtures.Dtos;

public static class UpdatePrnsRequestFixture
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<UpdatePrnsRequest> AddDefaults(
        this ICustomizationComposer<UpdatePrnsRequest> composer
    )
    {
        return composer
            .With(x => x.User, UserFixture.Regulator().Create())
            .With(x => x.PrnIds, [Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D")])
            .Without(x => x.ObligationYear);
    }

    public static IPostprocessComposer<UpdatePrnsRequest> Request()
    {
        return GetFixture().Build<UpdatePrnsRequest>().AddDefaults();
    }

    public static IPostprocessComposer<UpdatePrnsRequest> Accepted()
    {
        return Request().With(x => x.Status, UpdatePrnStatus.Accepted);
    }

    public static IPostprocessComposer<UpdatePrnsRequest> Rejected()
    {
        return Request().With(x => x.Status, UpdatePrnStatus.Rejected);
    }
}
