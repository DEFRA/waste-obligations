using System.Globalization;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Microsoft.Extensions.Time.Testing;

namespace Defra.WasteObligations.Api.Tests.Services;

public class CurrentComplianceYearProviderTests
{
    [Theory]
    [InlineData("2027-01-01T00:00:00+00:00", 2026)]
    [InlineData("2027-01-31T23:59:59+00:00", 2026)]
    [InlineData("2027-02-01T00:00:00+00:00", 2027)]
    [InlineData("2027-08-27T12:00:00+01:00", 2027)]
    public void GetCurrentComplianceYear_ShouldUseTheUnitedKingdomComplianceYear(string utcNow, int expected)
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse(utcNow, CultureInfo.InvariantCulture));
        var subject = new CurrentComplianceYearProvider(timeProvider);

        var result = subject.GetCurrentComplianceYear();

        result.Should().Be(expected);
    }

    [Fact]
    public void GetHandover_DuringJanuary_ShouldIncludeTheIncomingComplianceYear()
    {
        var timeProvider = new FakeTimeProvider(
            DateTimeOffset.Parse("2027-01-15T12:00:00+00:00", CultureInfo.InvariantCulture)
        );
        var subject = new CurrentComplianceYearProvider(timeProvider);

        var result = subject.GetHandover(TimeSpan.FromHours(1));

        result.CurrentComplianceYear.Should().Be(2026);
        result.IncomingComplianceYear.Should().Be(2027);
        result.OutgoingComplianceYear.Should().BeNull();
        result.OutgoingYearCutoverAt.Should().BeNull();
    }

    [Fact]
    public void GetHandover_DuringOutgoingYearGrace_ShouldIncludeTheOutgoingComplianceYear()
    {
        var timeProvider = new FakeTimeProvider(
            DateTimeOffset.Parse("2027-02-01T00:30:00+00:00", CultureInfo.InvariantCulture)
        );
        var subject = new CurrentComplianceYearProvider(timeProvider);

        var result = subject.GetHandover(TimeSpan.FromHours(1));

        result.CurrentComplianceYear.Should().Be(2027);
        result.IncomingComplianceYear.Should().BeNull();
        result.OutgoingComplianceYear.Should().Be(2026);
        result.OutgoingYearCutoverAt.Should().Be(new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GetHandover_AfterOutgoingYearGrace_ShouldOnlyIncludeTheCurrentComplianceYear()
    {
        var timeProvider = new FakeTimeProvider(
            DateTimeOffset.Parse("2027-02-01T01:00:00+00:00", CultureInfo.InvariantCulture)
        );
        var subject = new CurrentComplianceYearProvider(timeProvider);

        var result = subject.GetHandover(TimeSpan.FromHours(1));

        result.CurrentComplianceYear.Should().Be(2027);
        result.IncomingComplianceYear.Should().BeNull();
        result.OutgoingComplianceYear.Should().BeNull();
        result.OutgoingYearCutoverAt.Should().BeNull();
    }
}
