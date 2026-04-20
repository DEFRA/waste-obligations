using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing.Extensions;

// ReSharper disable ConvertClosureToMethodGroup

namespace Defra.WasteObligations.Testing.Fixtures.WasteOrganisations;

public static class RegistrationFixture
{
    private static Fixture GetFixture() => new();

    private static int RandomRegistrationYear() => Random.Shared.Next(2023, 2051);

    private static readonly string[] s_registrationTypes =
    [
        RegistrationType.LargeProducer,
        RegistrationType.ComplianceScheme,
    ];
    private static readonly string[] s_registrationStatuses =
    [
        RegistrationStatus.Registered,
        RegistrationStatus.Cancelled,
    ];

    public static void ConfigureDefaults(Fixture fixture)
    {
        fixture.Customize<Registration>(x =>
            x.With(y => y.Type, () => s_registrationTypes.Random())
                .With(y => y.RegistrationYear, () => RandomRegistrationYear())
                .With(y => y.Status, () => s_registrationStatuses.Random())
        );
    }

    public static IPostprocessComposer<Registration> Registration()
    {
        var fixture = GetFixture();

        ConfigureDefaults(fixture);

        return fixture
            .Build<Registration>()
            .With(x => x.Type, () => s_registrationTypes.Random())
            .With(x => x.RegistrationYear, () => RandomRegistrationYear())
            .With(x => x.Status, () => s_registrationStatuses.Random());
    }

    public static IPostprocessComposer<Registration> Default()
    {
        return Registration()
            .With(x => x.Type, RegistrationType.LargeProducer)
            .With(x => x.RegistrationYear, 2026)
            .With(x => x.Status, RegistrationStatus.Registered);
    }
}
