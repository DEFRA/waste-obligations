using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;

namespace Defra.WasteObligations.Testing.Fixtures.Entities;

public static class ObligationCalculationFixture
{
    public static IPostprocessComposer<ObligationCalculation> Default() =>
        new Fixture()
            .Build<ObligationCalculation>()
            .With(x => x.Id, ObjectId.GenerateNewId)
            .With(x => x.Version, 1)
            .With(x => x.SubmitterOrganisationId, Guid.NewGuid)
            .With(x => x.SubmitterType, ObligationSubmitterType.DirectRegistrant)
            .With(x => x.ObligationYear, 2025)
            .With(x => x.CalculatedOn, new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc))
            .With(
                x => x.Producers,
                [
                    new()
                    {
                        ProducerOrganisationId = Guid.NewGuid(),
                        Materials =
                        [
                            new()
                            {
                                MaterialCode = "PL",
                                InputKilograms = 50_100,
                                InputTonnes = 50,
                                ObligationTonnes = 28,
                            },
                            new()
                            {
                                MaterialCode = "GL",
                                InputKilograms = 3_000,
                                InputTonnes = 3,
                                ObligationTonnes = 1,
                            },
                            new()
                            {
                                MaterialCode = "GR",
                                InputKilograms = 3_000,
                                InputTonnes = 3,
                                ObligationTonnes = 2,
                            },
                        ],
                    },
                    new()
                    {
                        ProducerOrganisationId = Guid.NewGuid(),
                        Materials =
                        [
                            new()
                            {
                                MaterialCode = "PL",
                                InputKilograms = 50_100,
                                InputTonnes = 50,
                                ObligationTonnes = 28,
                            },
                        ],
                    },
                ]
            );
}
