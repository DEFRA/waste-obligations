using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Data.Entities;
using UserLocale = Defra.WasteObligations.Api.Dtos.UserLocale;

namespace Defra.WasteObligations.Testing.Fixtures.Entities;

public static class UserFixture
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<User> AddDefaults(this ICustomizationComposer<User> composer)
    {
        return composer.With(x => x.Id, () => Guid.NewGuid().ToString());
    }

    public static IPostprocessComposer<User> BuildUser()
    {
        return GetFixture().Build<User>().AddDefaults();
    }

    public static IPostprocessComposer<User> Default()
    {
        return BuildUser()
            .With(x => x.Id, "e72be574-8b5b-4836-af47-dd7e0c0d1d87")
            .With(x => x.Email, "submitter@email.com")
            .With(x => x.Name, "Submitter Name")
            .With(x => x.Locale, UserLocale.En);
    }
}
