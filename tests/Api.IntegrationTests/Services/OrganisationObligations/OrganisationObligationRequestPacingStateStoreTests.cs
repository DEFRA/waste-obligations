using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Microsoft.Extensions.Time.Testing;

namespace Defra.WasteObligations.Api.IntegrationTests.Services.OrganisationObligations;

public class OrganisationObligationRequestPacingStateStoreTests : IntegrationTestBase
{
    [Fact]
    public async Task Update_WhenConcurrent_ShouldRetainEveryStateTransition()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 9, 8, 16, 0, 0, TimeSpan.Zero));
        var subject = new OrganisationObligationRequestPacingStateStore(GetMongoApplicationDatabase(), timeProvider);
        var updates = Enumerable
            .Range(0, 10)
            .Select(_ =>
                subject.Update(
                    state => state with { DesiredRequestsPerMinute = state.DesiredRequestsPerMinute + 1 },
                    TestContext.Current.CancellationToken
                )
            );

        await Task.WhenAll(updates);

        var state = await subject.Get(TestContext.Current.CancellationToken);
        state.Should().NotBeNull();
        state.DesiredRequestsPerMinute.Should().Be(10);
        state.Version.Should().Be(10);
    }
}
