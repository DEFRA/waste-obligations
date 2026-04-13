using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing.Extensions;

// ReSharper disable ConvertClosureToMethodGroup

namespace Defra.WasteObligations.Testing.Fixtures.WasteOrganisations;

public static class RegistrationFixture
{
    private static Fixture GetFixture() => new();

    private static int RandomRegistrationYear() => Random.Shared.Next(2023, 2050);

    public static string[] RegistrationTypes = [RegistrationType.LargeProducer, RegistrationType.ComplianceScheme];
    public static string[] RegistrationStatuses = [RegistrationStatus.Registered, RegistrationStatus.Cancelled];

    public static void ConfigureDefaults(Fixture fixture)
    {
        fixture.Customize<Registration>(x =>
            x.With(y => y.Type, () => RegistrationTypes.Random())
                .With(y => y.RegistrationYear, () => RandomRegistrationYear())
                .With(y => y.Status, () => RegistrationStatuses.Random())
        );
    }

    public static IPostprocessComposer<Registration> Registration()
    {
        var fixture = GetFixture();

        ConfigureDefaults(fixture);

        return fixture
            .Build<Registration>()
            .With(x => x.Type, () => RegistrationTypes.Random())
            .With(x => x.RegistrationYear, () => RandomRegistrationYear())
            .With(x => x.Status, () => RegistrationStatuses.Random());
    }

    public static IPostprocessComposer<Registration> Default()
    {
        return Registration()
            .With(x => x.Type, RegistrationType.LargeProducer)
            .With(x => x.RegistrationYear, 2026)
            .With(x => x.Status, RegistrationStatus.Registered);
    }
}
