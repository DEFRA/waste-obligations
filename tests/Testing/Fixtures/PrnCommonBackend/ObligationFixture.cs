using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Dtos;
using Obligation = Defra.WasteObligations.Api.Services.PrnCommonBackend.Obligation;

namespace Defra.WasteObligations.Testing.Fixtures.PrnCommonBackend;

public static class ObligationFixture
{
    private static Fixture GetFixture() => new();

    public static readonly Guid OrganisationId = new("550e8400-e29b-41d4-a716-446655440000");

    public static IPostprocessComposer<Obligation> Obligation()
    {
        return GetFixture().Build<Obligation>();
    }

    public static IPostprocessComposer<Obligation> Default()
    {
        return Obligation()
            .With(x => x.OrganisationId, OrganisationId)
            .With(x => x.MaterialName, Material.Plastic)
            .With(x => x.Tonnage, 100)
            .With(x => x.MaterialTarget, 0.75m)
            .With(x => x.ObligationToMeet, (int?)null)
            .With(x => x.TonnageAwaitingAcceptance, 10)
            .With(x => x.TonnageAccepted, 2)
            .With(x => x.TonnageOutstanding, (int?)null)
            .With(x => x.Status, ObligationStatus.NoDataYet);
    }
}
