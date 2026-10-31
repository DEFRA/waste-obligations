using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;

namespace Defra.WasteObligations.Testing.Fixtures.Entities;

public static class ObligationCalculationInstructionFixture
{
    public static IPostprocessComposer<ObligationCalculationInstruction> Default() =>
        new Fixture()
            .Build<ObligationCalculationInstruction>()
            .With(x => x.Id, ObjectId.GenerateNewId)
            .With(x => x.ObligationYear, 2026)
            .With(x => x.SubmitterId, (Guid?)null)
            .With(x => x.Status, ObligationCalculationInstructionStatus.Pending)
            .With(x => x.CreatedAt, new DateTime(2026, 10, 30, 1, 0, 0, DateTimeKind.Utc));
}
