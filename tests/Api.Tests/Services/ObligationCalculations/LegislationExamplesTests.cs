using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Defra.WasteObligations.Api.Tests.Services.ObligationCalculations;

// Independent examples: docs/adr/0022-recycling-calculation-evidence.md.
// The calculator accepts already-selected totals. Previous-year selection, exemptions,
// eligibility and reusable packaging (C04, C05, C12 and C15) need upstream inputs.
// Partial-year and group responsibility cases (C09–C11) belong to future work.
// Schedule 5 does not specify output rounding; EA guidance rounds obligations up.
// The 2026 application of that guidance, group aggregation and partial-year rules
// need confirmation. C06 and C14 are covered in ObligationCalculatorTests.
public class LegislationExamplesTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly Guid SubmitterId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherProducerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Calculate_MixedMaterialsIn2026_AppliesTargetsAndKeepsRemeltWithinGlassTotal()
    {
        var submission = DirectSubmission(("PL", 100_000), ("PC", 200_000), ("GL", 100_000));

        var materials = Calculator().Calculate(submission, 2026).Producers.Single().Materials;

        materials.Single(x => x.MaterialCode == "PL").ObligationTonnes.Should().Be(57);
        materials.Single(x => x.MaterialCode == "PC").ObligationTonnes.Should().Be(154);
        var glass = materials.Single(x => x.MaterialCode == "GL").ObligationTonnes;
        var remelt = materials.Single(x => x.MaterialCode == "GR").ObligationTonnes;
        (glass + remelt).Should().Be(76);
        // C01: statutory arithmetic gives 57.76; the EA method rounds up to 58.
        remelt.Should().Be(58);
    }

    [Fact]
    public void Calculate_ZeroOfEachMaterial_ProducesZeroObligations()
    {
        var submission = DirectSubmission(("PL", 0), ("PC", 0), ("FC", 0), ("AL", 0), ("ST", 0), ("WD", 0), ("GL", 0));

        var materials = Calculator().Calculate(submission, 2026).Producers.Single().Materials;

        materials.Select(x => x.MaterialCode).Should().BeEquivalentTo("PL", "PC", "FC", "AL", "ST", "WD", "GL", "GR");
        materials.Should().OnlyContain(x => x.InputTonnes == 0 && x.ObligationTonnes == 0);
    }

    [Fact]
    public void Calculate_TwoSchemeMembers_PreservesEachFullYearObligation()
    {
        var submission = new ApprovedSubmission
        {
            SubmitterOrganisationId = SubmitterId,
            SubmitterType = ObligationSubmitterType.ComplianceScheme,
            Producers = [Producer(SubmitterId, ("PL", 100_000)), Producer(OtherProducerId, ("PL", 100_000))],
        };

        var producers = Calculator().Calculate(submission, 2026).Producers;

        producers.Should().HaveCount(2);
        producers
            .Single(x => x.ProducerOrganisationId == SubmitterId)
            .Materials.Single()
            .ObligationTonnes.Should()
            .Be(57);
        producers
            .Single(x => x.ProducerOrganisationId == OtherProducerId)
            .Materials.Single()
            .ObligationTonnes.Should()
            .Be(57);
        producers.Sum(x => x.Materials.Single().ObligationTonnes).Should().Be(114);
    }

    [Fact]
    public void DirectRegistrant_WithSubsidiaries_PreservesSubmitterAndProducerObligations()
    {
        var thirdProducerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var submission = new ApprovedSubmission
        {
            SubmitterOrganisationId = SubmitterId,
            SubmitterType = ObligationSubmitterType.DirectRegistrant,
            Producers = [Producer(OtherProducerId, ("PL", 100_000)), Producer(thirdProducerId, ("PL", 200_000))],
        };

        var result = Calculator().Calculate(submission, 2026);

        result.SubmitterOrganisationId.Should().Be(SubmitterId);
        result.SubmitterType.Should().Be(ObligationSubmitterType.DirectRegistrant);
        result.Producers.Should().HaveCount(2);
        result
            .Producers.Single(x => x.ProducerOrganisationId == OtherProducerId)
            .Materials.Single()
            .ObligationTonnes.Should()
            .Be(57);
        result
            .Producers.Single(x => x.ProducerOrganisationId == thirdProducerId)
            .Materials.Single()
            .ObligationTonnes.Should()
            .Be(114);
    }

    [Fact]
    public void C03_OneTonnePerMaterial_RoundsEachObligationUp()
    {
        var submission = DirectSubmission(
            ("PL", 1_000),
            ("PC", 1_000),
            ("FC", 1_000),
            ("AL", 1_000),
            ("ST", 1_000),
            ("WD", 1_000),
            ("GL", 1_000)
        );

        var materials = Calculator().Calculate(submission, 2026).Producers.Single().Materials;

        materials.Where(x => x.MaterialCode != "GL").Should().OnlyContain(x => x.ObligationTonnes == 1);
        materials.Single(x => x.MaterialCode == "GL").ObligationTonnes.Should().Be(0);
        materials.Should().HaveCount(8);
    }

    [Fact]
    public void C08_TwoSchemeMembers_RoundsEachBeforeAggregation()
    {
        // Scheme members are rounded independently; the direct-registrant case is tested separately.
        var submission = new ApprovedSubmission
        {
            SubmitterOrganisationId = SubmitterId,
            SubmitterType = ObligationSubmitterType.ComplianceScheme,
            Producers = [Producer(SubmitterId, ("PL", 1_490)), Producer(OtherProducerId, ("PL", 1_490))],
        };

        var producers = Calculator().Calculate(submission, 2026).Producers;

        producers.Select(x => x.Materials.Single().InputTonnes).Should().Equal(1, 1);
        producers.Select(x => x.Materials.Single().ObligationTonnes).Should().Equal(1, 1);
        producers.Sum(x => x.Materials.Single().ObligationTonnes).Should().Be(2);
    }

    [Fact]
    public void PaperAndComposite_AreRoundedSeparatelyBeforeAdding()
    {
        var materials = Calculator()
            .Calculate(DirectSubmission(("PC", 3_000), ("FC", 3_000)), 2026)
            .Producers.Single()
            .Materials;

        materials.Select(x => x.ObligationTonnes).Should().Equal(3, 3);
        materials.Sum(x => x.ObligationTonnes).Should().Be(6);
    }

    [Fact]
    public void Glass_RoundsInputBeforeRemeltCalculation()
    {
        // Schedule 5 rounds L to tonnes; the EA workbook example appears to use unrounded L.
        var materials = Calculator().Calculate(DirectSubmission(("GL", 23_400)), 2027).Producers.Single().Materials;

        materials.Single(x => x.MaterialCode == "GR").ObligationTonnes.Should().Be(14);
        materials.Single(x => x.MaterialCode == "GL").ObligationTonnes.Should().Be(4);
    }

    [Theory]
    [InlineData(10_450, 10, 8)]
    [InlineData(10_567, 11, 9)]
    public void Paper_EaInputRoundingExamples(long kilograms, long inputTonnes, long obligation)
    {
        var material = Calculator()
            .Calculate(DirectSubmission(("PC", kilograms)), 2027)
            .Producers.Single()
            .Materials.Single();

        material.InputTonnes.Should().Be(inputTonnes);
        material.ObligationTonnes.Should().Be(obligation);
    }

    [Theory]
    [InlineData(2024)]
    [InlineData(2031)]
    public void Calculate_YearOutsideConfiguredTargets_RejectsRequest(int year)
    {
        var act = () => Calculator().Calculate(DirectSubmission(("PL", 100_000)), year);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private ObligationCalculator Calculator() =>
        new(factory.Services.GetRequiredService<IOptions<ObligationCalculationOptions>>(), new FakeTimeProvider());

    private static ApprovedSubmission DirectSubmission(params (string Code, long Kilograms)[] materials) =>
        new()
        {
            SubmitterOrganisationId = SubmitterId,
            SubmitterType = ObligationSubmitterType.DirectRegistrant,
            Producers = [Producer(SubmitterId, materials)],
        };

    private static ProducerPackaging Producer(Guid id, params (string Code, long Kilograms)[] materials) =>
        new()
        {
            ProducerOrganisationId = id,
            Materials = materials
                .Select(x => new MaterialWeight { MaterialCode = x.Code, Kilograms = x.Kilograms })
                .ToArray(),
        };
}
