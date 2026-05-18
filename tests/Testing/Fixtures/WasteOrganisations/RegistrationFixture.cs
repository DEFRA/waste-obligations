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

    public static IPostprocessComposer<Registration> AddDefaults(this ICustomizationComposer<Registration> composer)
    {
        return composer
            .With(x => x.Type, () => s_registrationTypes.Random())
            .With(x => x.RegistrationYear, () => RandomRegistrationYear())
            .With(x => x.Status, () => s_registrationStatuses.Random());
    }

    public static IPostprocessComposer<Registration> Registration()
    {
        var fixture = GetFixture();

        fixture.Customize<Registration>(x => x.AddDefaults());

        return fixture.Build<Registration>().AddDefaults();
    }

    public static IPostprocessComposer<Registration> Default()
    {
        return Registration()
            .With(x => x.Type, RegistrationType.LargeProducer)
            .With(x => x.RegistrationYear, 2026)
            .With(x => x.Status, RegistrationStatus.Registered)
            .With(x => x.Created, new DateTimeOffset(2026, 5, 18, 11, 20, 0, TimeSpan.Zero))
            .With(x => x.Updated, new DateTimeOffset(2026, 5, 18, 11, 20, 0, TimeSpan.Zero));
    }
}
