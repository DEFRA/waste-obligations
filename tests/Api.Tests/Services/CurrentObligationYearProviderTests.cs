using System.Globalization;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Microsoft.Extensions.Time.Testing;

namespace Defra.WasteObligations.Api.Tests.Services;

public class CurrentObligationYearProviderTests
{
    [Theory]
    [InlineData("2027-01-01T00:00:00+00:00", 2026)]
    [InlineData("2027-01-31T23:59:59+00:00", 2026)]
    [InlineData("2027-02-01T00:00:00+00:00", 2027)]
    [InlineData("2027-08-27T12:00:00+01:00", 2027)]
    public void GetCurrentObligationYear_ShouldUseTheUnitedKingdomObligationYear(string utcNow, int expected)
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse(utcNow, CultureInfo.InvariantCulture));
        var subject = new CurrentObligationYearProvider(timeProvider);

        var result = subject.GetCurrentObligationYear();

        result.Should().Be(expected);
    }
}
