using AutoFixture;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing.Fixtures.WasteOrganisations;
using Organisation = Defra.WasteObligations.Api.Services.WasteOrganisations.Organisation;

namespace Defra.WasteObligations.Testing.Fakes;

public class FakeWasteOrganisationsService : IWasteOrganisationsService
{
    public bool Throws = false;

    public static readonly Guid OrganisationId = new("923fa611-571c-4948-ab7d-fbb75e75ed65");
    public const int Year = 2026;

    private static readonly Dictionary<Guid, Organisation> s_organisations = new()
    {
        {
            OrganisationId,
            OrganisationFixture
                .Default(OrganisationId)
                .With(x => x.Name, "Organisation Name")
                .With(x => x.TradingName, "Trading Name")
                .With(
                    x => x.Registrations,
                    [
                        RegistrationFixture
                            .Default()
                            .With(x => x.Type, RegistrationType.ComplianceScheme)
                            .With(x => x.RegistrationYear, Year)
                            .Create(),
                        RegistrationFixture
                            .Default()
                            .With(x => x.Type, RegistrationType.LargeProducer)
                            .With(x => x.RegistrationYear, Year + 1)
                            .Create(),
                    ]
                )
                .Create()
        },
    };

    public Task<Organisation?> Read(Guid organisationId, CancellationToken cancellationToken)
    {
        if (Throws)
            throw new InvalidOperationException("The operation failed");

        return Task.FromResult(s_organisations.TryGetValue(organisationId, out var value) ? value : null);
    }
}
