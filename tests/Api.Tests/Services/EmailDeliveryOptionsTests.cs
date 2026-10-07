using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;

namespace Defra.WasteObligations.Api.Tests.Services;

public class EmailDeliveryOptionsTests
{
    [Fact]
    public void TryReadCutover_WhenNull_ShouldDisableCutover()
    {
        var options = new EmailDeliveryOptions();

        options.TryReadCutover(out var cutover).Should().BeTrue();
        cutover.Should().BeNull();
    }

    [Theory]
    [InlineData("2026-10-02T12:00:00.1234567Z")]
    [InlineData("2026-10-02T12:00:00.1234567+00:00")]
    [InlineData("2026-10-02T12:00:00.1234567-00:00")]
    public void TryReadCutover_WhenExplicitUtc_ShouldTruncateToWholeMilliseconds(string value)
    {
        var options = new EmailDeliveryOptions { EmailDeliveryCutoverUtc = value };

        options.TryReadCutover(out var cutover).Should().BeTrue();
        cutover.Should().Be(new DateTimeOffset(2026, 10, 2, 12, 0, 0, 123, TimeSpan.Zero));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("2026-10-02T12:00:00")]
    [InlineData("2026-10-02T12:00:00+01:00")]
    [InlineData("2026-10-02T12:00:00-01:00")]
    [InlineData("2026-10-02")]
    [InlineData("invalidZ")]
    [InlineData("2026-02-30T12:00:00Z")]
    public void TryReadCutover_WhenInvalid_ShouldRejectValue(string value)
    {
        var options = new EmailDeliveryOptions { EmailDeliveryCutoverUtc = value };

        options.TryReadCutover(out var cutover).Should().BeFalse();
        cutover.Should().BeNull();
    }
}
