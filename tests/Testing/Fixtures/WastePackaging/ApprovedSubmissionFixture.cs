using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.WastePackaging;

namespace Defra.WasteObligations.Testing.Fixtures.WastePackaging;

public static class ApprovedSubmissionFixture
{
    public static IPostprocessComposer<ApprovedSubmission> Default() =>
        new Fixture()
            .Build<ApprovedSubmission>()
            .With(x => x.SubmitterOrganisationId, Guid.NewGuid)
            .With(x => x.SubmitterType, ObligationSubmitterType.DirectRegistrant)
            .With(x => x.Producers, [ProducerPackagingFixture.Default().Create()]);
}
