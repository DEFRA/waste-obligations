using AutoFixture;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Testing.Fixtures.PrnCommonBackend;
using Obligation = Defra.WasteObligations.Api.Services.PrnCommonBackend.Obligation;

namespace Defra.WasteObligations.Testing.Fakes;

public class FakePrnCommonBackendService : IPrnCommonBackendService
{
    public const string InvalidPrnId = "923615ff-1372-4ba6-a068-f050b67980cd";
    public const string MismatchedPrnId = "3990be58-e16e-4fb8-9fc1-cfb1ce5a9295";

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

    private static readonly Dictionary<(Guid, string), PrnData> s_prns = new()
    {
        {
            (FakeWasteOrganisationsService.OrganisationId, PrnDataFixture.PrnId.ToString("D")),
            PrnDataFixture.Default().With(x => x.OrganisationId, FakeWasteOrganisationsService.OrganisationId).Create()
        },
        {
            (FakeWasteOrganisationsService.OrganisationId, MismatchedPrnId),
            PrnDataFixture
                .Default()
                .With(x => x.ExternalId, Guid.Parse(MismatchedPrnId))
                .With(x => x.OrganisationId, Guid.NewGuid())
                .Create()
        },
        {
            (FakeWasteOrganisationsService.OrganisationId, InvalidPrnId),
            PrnDataFixture
                .Default()
                .With(x => x.ExternalId, Guid.Empty)
                .With(x => x.OrganisationId, FakeWasteOrganisationsService.OrganisationId)
                .Create()
        },
    };

    public PrnStatusUpdate? LastPrnStatusUpdate { get; private set; }
    public PrnStatusUpdateResult StatusUpdateResult { get; set; } = PrnStatusUpdateResult.Updated;
    public bool ConcurrencyError { get; set; }
    public bool Throws { get; set; }

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

    public Task<PrnData?> ReadPrn(Guid organisationId, string prnId, CancellationToken cancellationToken)
    {
        return Task.FromResult(s_prns.GetValueOrDefault((organisationId, prnId)));
    }

    public Task<PrnSearchResponse> SearchPrns(
        Guid organisationId,
        PrnSearchRequest search,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(new PrnSearchResponse());
    }

    public Task<PrnStatusUpdateResult> UpdatePrnStatus(
        Guid organisationId,
        Guid userId,
        string prnId,
        string status,
        CancellationToken cancellationToken
    )
    {
        if (ConcurrencyError)
            throw new ConcurrencyException("The PRN status has already been updated.");

        if (Throws)
            throw new HttpRequestException();

        if (!Guid.TryParse(prnId, out var commonBackendPrnId))
            return Task.FromResult(PrnStatusUpdateResult.NotFound);

        LastPrnStatusUpdate = new PrnStatusUpdate { PrnId = commonBackendPrnId, Status = status };

        return Task.FromResult(StatusUpdateResult);
    }
}
