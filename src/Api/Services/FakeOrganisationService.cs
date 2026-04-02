using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;

namespace Defra.WasteObligations.Api.Services;

public class FakeOrganisationService : IOrganisationService
{
    public static readonly Guid OrganisationId = new("923fa611-571c-4948-ab7d-fbb75e75ed65");

    private static readonly Dictionary<Guid, Organisation> s_organisations = new()
    {
        {
            OrganisationId,
            new Organisation
            {
                Id = OrganisationId,
                Name = "Organisation Name",
                Address = new Address(),
            }
        },
    };

    private static readonly Dictionary<(Guid, int), List<Obligation>> s_obligations = new()
    {
        {
            (OrganisationId, 2026),
            [
                new Obligation
                {
                    OrganisationId = OrganisationId,
                    MaterialName = "Plastic",
                    Tonnage = 100,
                    MaterialTarget = 0.75m,
                    ObligationToMeet = null,
                    TonnageAwaitingAcceptance = 10,
                    TonnageAccepted = 2,
                    TonnageOutstanding = null,
                    Status = "NoDataYet",
                },
                new Obligation
                {
                    OrganisationId = OrganisationId,
                    MaterialName = "Paper",
                    Tonnage = 100,
                    MaterialTarget = 0.75m,
                    ObligationToMeet = 200,
                    TonnageAwaitingAcceptance = 10,
                    TonnageAccepted = 2,
                    TonnageOutstanding = 198,
                    Status = "NotMet",
                },
            ]
        },
    };

    public Task<Organisation?> ReadOrganisation(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(s_organisations.TryGetValue(id, out var value) ? value : null);

    public Task<IEnumerable<Obligation>> ReadObligations(
        Guid organisationId,
        int year,
        CancellationToken cancellationToken
    ) =>
        Task.FromResult(
            s_obligations.TryGetValue((organisationId, year), out var value) ? value : Enumerable.Empty<Obligation>()
        );
}
