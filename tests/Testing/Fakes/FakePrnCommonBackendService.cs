using AutoFixture;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Testing.Fixtures.PrnCommonBackend;
using Obligation = Defra.WasteObligations.Api.Services.PrnCommonBackend.Obligation;

namespace Defra.WasteObligations.Testing.Fakes;

public class FakePrnCommonBackendService : IPrnCommonBackendService
{
    private static readonly Dictionary<(Guid, int), List<Obligation>> s_obligations = new()
    {
        {
            (FakeWasteOrganisationsService.OrganisationId, FakeWasteOrganisationsService.Year),
            [
                ObligationFixture
                    .Default()
                    .With(x => x.OrganisationId, FakeWasteOrganisationsService.OrganisationId)
                    .Create(),
                ObligationFixture
                    .Default()
                    .With(x => x.OrganisationId, FakeWasteOrganisationsService.OrganisationId)
                    .With(x => x.MaterialName, Material.Paper)
                    .With(x => x.ObligationToMeet, 200)
                    .With(x => x.TonnageOutstanding, 198)
                    .With(x => x.Status, ObligationStatus.NotMet)
                    .Create(),
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
