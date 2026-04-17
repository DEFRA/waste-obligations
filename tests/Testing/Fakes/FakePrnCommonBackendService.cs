using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Obligation = Defra.WasteObligations.Api.Services.PrnCommonBackend.Obligation;

namespace Defra.WasteObligations.Testing.Fakes;

public class FakePrnCommonBackendService : IPrnCommonBackendService
{
    private static readonly Dictionary<(Guid, int), List<Obligation>> s_obligations = new()
    {
        {
            (FakeWasteOrganisationsService.OrganisationId, FakeWasteOrganisationsService.Year),
            [
                new Obligation
                {
                    OrganisationId = FakeWasteOrganisationsService.OrganisationId,
                    MaterialName = Material.Plastic,
                    Tonnage = 100,
                    MaterialTarget = 0.75m,
                    ObligationToMeet = null,
                    TonnageAwaitingAcceptance = 10,
                    TonnageAccepted = 2,
                    TonnageOutstanding = null,
                    Status = ObligationStatus.NoDataYet,
                },
                new Obligation
                {
                    OrganisationId = FakeWasteOrganisationsService.OrganisationId,
                    MaterialName = Material.Paper,
                    Tonnage = 100,
                    MaterialTarget = 0.75m,
                    ObligationToMeet = 200,
                    TonnageAwaitingAcceptance = 10,
                    TonnageAccepted = 2,
                    TonnageOutstanding = 198,
                    Status = ObligationStatus.NotMet,
                },
            ]
        },
    };

    public Task<IEnumerable<Obligation>> ReadObligations(
        Guid organisationId,
        int year,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(
            s_obligations.TryGetValue((organisationId, year), out var value) ? value : Enumerable.Empty<Obligation>()
        );
    }
}
