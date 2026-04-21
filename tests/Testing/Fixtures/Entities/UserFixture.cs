using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Testing.Fixtures.Entities;

public static class UserFixture
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<User> User()
    {
        return GetFixture().Build<User>().With(x => x.Id, () => Guid.NewGuid().ToString());
    }

    public static IPostprocessComposer<User> Default()
    {
        return User().With(x => x.Id, "e72be574-8b5b-4836-af47-dd7e0c0d1d87").With(x => x.Email, "submitter@email.com");
    }
}
