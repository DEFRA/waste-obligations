using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Organisation = Defra.WasteObligations.Api.Services.WasteOrganisations.Organisation;

namespace Defra.WasteObligations.Testing.Fakes;

public class FakeWasteOrganisationsService : IWasteOrganisationsService
{
    public static readonly Guid OrganisationId = new("923fa611-571c-4948-ab7d-fbb75e75ed65");
    public const int Year = 2026;

    private static readonly Dictionary<Guid, Organisation> s_organisations = new()
    {
        {
            OrganisationId,
            new Organisation
            {
                Id = OrganisationId,
                Name = "Organisation Name",
                TradingName = "Trading Name",
                BusinessCountry = BusinessCountry.England,
                Address = new Address(),
                Registrations =
                [
                    new Registration
                    {
                        Status = RegistrationStatus.Registered,
                        Type = RegistrationType.ComplianceScheme,
                        RegistrationYear = Year,
                    },
                    new Registration
                    {
                        Status = RegistrationStatus.Registered,
                        Type = RegistrationType.LargeProducer,
                        RegistrationYear = Year + 1,
                    },
                ],
            }
        },
    };

    public Task<Organisation?> ReadOrganisation(Guid organisationId, CancellationToken cancellationToken)
    {
        return Task.FromResult(s_organisations.TryGetValue(organisationId, out var value) ? value : null);
    }
}
