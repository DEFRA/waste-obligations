using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;

namespace Defra.WasteObligations.Api.Services;

public class FakeOrganisationService : IOrganisationService
{
    private static readonly Guid s_organisationId = new("923fa611-571c-4948-ab7d-fbb75e75ed65");
    private static readonly Dictionary<Guid, Organisation> s_organisations = new()
    {
        {
            s_organisationId,
            new Organisation
            {
                Id = s_organisationId,
                Name = "Organisation Name",
                Address = new Address(),
            }
        },
    };

    private static readonly Dictionary<(Guid, int), List<Obligation>> s_obligations = new()
    {
        {
            (s_organisationId, 2026),
            [
                new Obligation
                {
                    OrganisationId = s_organisationId,
                    MaterialName = "Plastic",
                    Tonnage = 100,
                    MaterialTarget = 0.75,
                    ObligationToMeet = null,
                    TonnageAwaitingAcceptance = 10,
                    TonnageAccepted = 2,
                    TonnageOutstanding = null,
                    Status = "NoDataYet",
                },
                new Obligation
                {
                    OrganisationId = s_organisationId,
                    MaterialName = "Paper",
                    Tonnage = 100,
                    MaterialTarget = 0.75,
                    ObligationToMeet = 200,
                    TonnageAwaitingAcceptance = 10,
                    TonnageAccepted = 2,
                    TonnageOutstanding = 198,
                    Status = "NotMet",
                },
            ]
        },
    };

    public Task<Organisation?> GetOrganisation(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(s_organisations.TryGetValue(id, out var value) ? value : null);

    public Task<IEnumerable<Obligation>> GetObligations(
        Guid organisationId,
        int year,
        CancellationToken cancellationToken
    ) =>
        Task.FromResult(
            s_obligations.TryGetValue((organisationId, year), out var value) ? value : Enumerable.Empty<Obligation>()
        );
}
