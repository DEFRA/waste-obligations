using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Testing.Fixtures.PrnCommonBackend;
using PrnCommonBackendMappers = Defra.WasteObligations.Api.Services.PrnCommonBackend.Mappers;

namespace Defra.WasteObligations.Api.Tests.Services.PrnCommonBackend;

public class MappersTests
{
    [Fact]
    public void ToDto_ShouldMapPrn()
    {
        var result = PrnDataFixture.Default().Create().ToDto();

        result.Id.Should().Be(PrnDataFixture.PrnId.ToString("D"));
        result.Number.Should().Be("PRN123");
        result.Type.Should().Be(PrnType.Prn);
        result.Status.Should().Be(PrnStatus.AwaitingAcceptance);
        result.IssuedAt.Should().Be(new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero));
        result.ObligationYear.Should().Be(2025);
        result.AccreditationYear.Should().Be(2025);
        result.DecemberWaste.Should().BeFalse();
        result.Material.Should().Be(Material.Aluminium);
        result.RecyclingProcess.Should().Be("R3");
        result.Tonnage.Should().Be(999);
        result.Issuer.OrganisationName.Should().Be("Acme Reprocessors Ltd");
        result.Recipient.OrganisationId.Should().Be(PrnDataFixture.OrganisationId);
        result.Recipient.DisplayName.Should().Be("Test Producer Ltd");
        result.Recipient.Name.Should().BeNull();
        result.Recipient.TradingName.Should().BeNull();
        result.Recipient.RegistrationType.Should().BeNull();
        result.AuthorisedBy.Name.Should().Be("Jane Smith");
        result.AuthorisedBy.Position.Should().Be("Director");
        result.AccreditationNumber.Should().Be("ACC123");
        result.ReprocessingSite.Should().Be("42 Factory Road, Manchester");
        result.ReprocessorExporterAgency.Should().Be("Environment Agency");
        result.AdditionalNotes.Should().Be("Important note about this PRN");
        result.Audit.CreatedAt.Should().Be(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        result.Audit.UpdatedAt.Should().Be(new DateTimeOffset(2026, 1, 15, 10, 5, 0, TimeSpan.Zero));
        result.Audit.AcceptedAt.Should().BeNull();
        result.Audit.RejectedAt.Should().BeNull();
        result.Audit.CancelledAt.Should().BeNull();
    }

    [Fact]
    public void ToDto_WhenExport_ShouldMapPern()
    {
        PrnDataFixture.Default().With(x => x.IsExport, true).Create().ToDto().Type.Should().Be(PrnType.Pern);
    }

    [Theory]
    [InlineData("Aluminium", Material.Aluminium)]
    [InlineData("Plastic", Material.Plastic)]
    [InlineData("Steel", Material.Steel)]
    [InlineData("Wood", Material.Wood)]
    [InlineData("Wood Composting", Material.Wood)]
    [InlineData("Paper/board", Material.Paper)]
    [InlineData("Paper Composting", Material.Paper)]
    [InlineData("Fibre", Material.Fibre)]
    [InlineData("Glass Other", Material.Glass)]
    [InlineData("Glass Re-melt", Material.GlassRemelt)]
    public void ToDto_WhenMaterialKnown_ShouldMap(string source, string expected)
    {
        PrnDataFixture.Default().With(x => x.MaterialName, source).Create().ToDto().Material.Should().Be(expected);
    }

    [Fact]
    public void ToDto_WhenMaterialUnknown_ShouldThrow()
    {
        const string material = "Unknown";

        var act = () => PrnDataFixture.Default().With(x => x.MaterialName, material).Create().ToDto();

        act.Should().Throw<InvalidOperationException>().WithMessage($"*MaterialName*{material}*");
    }

    [Theory]
    [InlineData("AWAITINGACCEPTANCE", PrnStatus.AwaitingAcceptance)]
    [InlineData("ACCEPTED", PrnStatus.Accepted)]
    [InlineData("REJECTED", PrnStatus.Rejected)]
    [InlineData("CANCELLED", PrnStatus.Cancelled)]
    [InlineData("CANCELED", PrnStatus.Cancelled)]
    public void ToDto_WhenStatusKnown_ShouldMap(string source, string expected)
    {
        PrnDataFixture.Default().With(x => x.PrnStatus, source).Create().ToDto().Status.Should().Be(expected);
    }

    [Fact]
    public void ToDto_WhenStatusUnknown_ShouldThrow()
    {
        const string status = "Unknown";

        var act = () => PrnDataFixture.Default().With(x => x.PrnStatus, status).Create().ToDto();

        act.Should().Throw<InvalidOperationException>().WithMessage($"*PrnStatus*{status}*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-year")]
    public void ToDto_WhenObligationYearInvalid_ShouldThrow(string? year)
    {
        var act = () => PrnDataFixture.Default().With(x => x.ObligationYear, year).Create().ToDto();

        act.Should().Throw<InvalidOperationException>().WithMessage("*ObligationYear*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-year")]
    public void ToDto_WhenAccreditationYearInvalid_ShouldThrow(string? year)
    {
        var act = () => PrnDataFixture.Default().With(x => x.AccreditationYear, year).Create().ToDto();

        act.Should().Throw<InvalidOperationException>().WithMessage("*AccreditationYear*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ToDto_WhenRecyclingProcessInvalid_ShouldThrow(string? process)
    {
        var act = () => PrnDataFixture.Default().With(x => x.ProcessToBeUsed, process).Create().ToDto();

        act.Should().Throw<InvalidOperationException>().WithMessage("*ProcessToBeUsed*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ToDto_WhenReprocessorExporterAgencyInvalid_ShouldThrow(string? agency)
    {
        var act = () => PrnDataFixture.Default().With(x => x.ReprocessorExporterAgency, agency).Create().ToDto();

        act.Should().Throw<InvalidOperationException>().WithMessage("*ReprocessorExporterAgency*");
    }

    [Fact]
    public void ToDto_WhenExternalIdEmpty_ShouldThrow()
    {
        var act = () => PrnDataFixture.Default().With(x => x.ExternalId, Guid.Empty).Create().ToDto();

        act.Should().Throw<InvalidOperationException>().WithMessage("*ExternalId*");
    }

    [Fact]
    public void ToDto_WhenOrganisationIdEmpty_ShouldThrow()
    {
        var act = () => PrnDataFixture.Default().With(x => x.OrganisationId, Guid.Empty).Create().ToDto();

        act.Should().Throw<InvalidOperationException>().WithMessage("*OrganisationId*");
    }

    [Fact]
    public void ToDto_WhenIssueDateDefault_ShouldThrow()
    {
        var act = () => PrnDataFixture.Default().With(x => x.IssueDate, default(DateTime)).Create().ToDto();

        act.Should().Throw<InvalidOperationException>().WithMessage("*IssueDate*");
    }

    [Fact]
    public void ToDto_WhenCreatedOnDefault_ShouldThrow()
    {
        var act = () => PrnDataFixture.Default().With(x => x.CreatedOn, default(DateTime)).Create().ToDto();

        act.Should().Throw<InvalidOperationException>().WithMessage("*CreatedOn*");
    }

    [Fact]
    public void ToDto_WhenLastUpdatedDateDefault_ShouldThrow()
    {
        var act = () => PrnDataFixture.Default().With(x => x.LastUpdatedDate, default(DateTime)).Create().ToDto();

        act.Should().Throw<InvalidOperationException>().WithMessage("*LastUpdatedDate*");
    }

    [Fact]
    public void ToDto_WhenTonnageValueDefault_ShouldThrow()
    {
        var act = () => PrnDataFixture.Default().With(x => x.TonnageValue, default(int)).Create().ToDto();

        act.Should().Throw<InvalidOperationException>().WithMessage("*TonnageValue*");
    }

    [Fact]
    public void ToDto_WhenOptionalStringsBlank_ShouldMapNull()
    {
        var result = PrnDataFixture
            .Default()
            .With(x => x.PrnSignatory, " ")
            .With(x => x.PrnSignatoryPosition, "")
            .With(x => x.ReprocessingSite, (string?)null)
            .With(x => x.IssuerNotes, " ")
            .Create()
            .ToDto();

        result.AuthorisedBy.Name.Should().BeNull();
        result.AuthorisedBy.Position.Should().BeNull();
        result.ReprocessingSite.Should().BeNull();
        result.AdditionalNotes.Should().BeNull();
    }

    [Fact]
    public void ToUtcDateTimeOffset_WhenUtc_ShouldPreserveClockValue()
    {
        var value = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        PrnCommonBackendMappers
            .ToUtcDateTimeOffset(value)
            .Should()
            .Be(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ToUtcDateTimeOffset_WhenUnspecified_ShouldAttachUtcWithoutChangingClockValue()
    {
        var value = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Unspecified);

        PrnCommonBackendMappers
            .ToUtcDateTimeOffset(value)
            .Should()
            .Be(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ToUtcDateTimeOffset_WhenLocal_ShouldConvertToUtc()
    {
        var value = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Local);

        PrnCommonBackendMappers.ToUtcDateTimeOffset(value).Should().Be(new DateTimeOffset(value.ToUniversalTime()));
    }
}
