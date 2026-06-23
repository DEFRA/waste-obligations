using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Testing.Fixtures.Dtos;

public static class UserFixture
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<User> AddDefaults(this ICustomizationComposer<User> composer)
    {
        return composer.With(x => x.Id, () => Guid.NewGuid().ToString());
    }

    public static IPostprocessComposer<User> User()
    {
        return GetFixture().Build<User>().AddDefaults();
    }

    public static IPostprocessComposer<User> Default()
    {
        return User()
            .With(x => x.Id, "e72be574-8b5b-4836-af47-dd7e0c0d1d87")
            .With(x => x.Email, "submitter@email.com")
            .With(x => x.Name, "Submitter Name");
    }

    public static IPostprocessComposer<User> ApprovedPerson()
    {
        return User()
            .With(x => x.Id, "c8d3a4b1-2f67-4e9d-9a12-6f8e3b7c4d91")
            .With(x => x.Email, "approved-person@email.com")
            .With(x => x.Name, "Approved Person Name");
    }

    public static IPostprocessComposer<User> Regulator()
    {
        return User()
            .With(x => x.Id, "7e91f2ac-5b44-4c8d-ae73-1d9f62b8e0f4")
            .With(x => x.Email, "regulator@email.com")
            .With(x => x.Name, "Regulator Name");
    }
}
