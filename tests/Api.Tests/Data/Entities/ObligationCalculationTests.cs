using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Defra.WasteObligations.Api.Tests.Data.Entities;

public class ObligationCalculationTests
{
    [Fact]
    public void RequiresNewVersion_WhenDataIsEquivalentAndReordered_ShouldBeFalse()
    {
        var current = Calculation();
        var incoming = current with
        {
            Producers = current
                .Producers.Reverse()
                .Select(x => x with { Materials = x.Materials.Reverse().Select(x => x with { }).ToArray() })
                .ToArray(),
        };

        incoming.RequiresNewVersion(current).Should().BeFalse();
    }

    [Theory]
    [InlineData("calculatedOn")]
    [InlineData("id")]
    [InlineData("version")]
    [InlineData("schemaVersion")]
    public void RequiresNewVersion_WhenOnlyIgnoredFieldsChange_ShouldBeFalse(string change)
    {
        var current = Calculation();
        var incoming = change switch
        {
            "calculatedOn" => current with { CalculatedOn = current.CalculatedOn.AddDays(1) },
            "id" => current with { Id = ObjectId.GenerateNewId() },
            "version" => current with { Version = current.Version + 1 },
            _ => WithOlderSchemaVersion(current),
        };

        incoming.RequiresNewVersion(current).Should().BeFalse();
    }

    [Theory]
    [InlineData("submitterType")]
    [InlineData("producerId")]
    [InlineData("producerRemoved")]
    [InlineData("producerAdded")]
    [InlineData("materialCode")]
    [InlineData("materialRemoved")]
    [InlineData("materialAdded")]
    [InlineData("inputKilograms")]
    [InlineData("inputTonnes")]
    [InlineData("obligationTonnes")]
    public void RequiresNewVersion_WhenCalculationDataChanges_ShouldBeTrue(string change)
    {
        var current = Calculation();
        var producer = current.Producers[0];
        var material = producer.Materials[0];
        var incoming = change switch
        {
            "submitterType" => current with { SubmitterType = ObligationSubmitterType.ComplianceScheme },
            "producerId" => current with
            {
                Producers = [producer with { ProducerOrganisationId = Guid.NewGuid() }, current.Producers[1]],
            },
            "producerRemoved" => current with { Producers = [producer] },
            "producerAdded" => current with
            {
                Producers = [.. current.Producers, producer with { ProducerOrganisationId = Guid.NewGuid() }],
            },
            "materialRemoved" => current with
            {
                Producers = [producer with { Materials = [material] }, current.Producers[1]],
            },
            "materialAdded" => current with
            {
                Producers =
                [
                    producer with
                    {
                        Materials = [.. producer.Materials, material with { MaterialCode = "PC" }],
                    },
                    current.Producers[1],
                ],
            },
            _ => current with
            {
                Producers =
                [
                    producer with
                    {
                        Materials =
                        [
                            change switch
                            {
                                "materialCode" => material with { MaterialCode = "PC" },
                                "inputKilograms" => material with { InputKilograms = 50_400 },
                                "inputTonnes" => material with { InputTonnes = 51 },
                                _ => material with { ObligationTonnes = 29 },
                            },
                            .. producer.Materials.Skip(1),
                        ],
                    },
                    current.Producers[1],
                ],
            },
        };

        incoming.RequiresNewVersion(current).Should().BeTrue();
    }

    private static ObligationCalculation WithOlderSchemaVersion(ObligationCalculation calculation)
    {
        var document = calculation.ToBsonDocument();
        document["schemaVersion"] = "v0.9";

        return BsonSerializer.Deserialize<ObligationCalculation>(document);
    }

    private static ObligationCalculation Calculation() =>
        new()
        {
            Id = ObjectId.GenerateNewId(),
            Version = 1,
            SubmitterOrganisationId = Guid.NewGuid(),
            SubmitterType = ObligationSubmitterType.DirectRegistrant,
            ObligationYear = 2025,
            CalculatedOn = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            Producers =
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
            ],
        };
}
