using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Data;

public class ApplicationDataCorrectionRunnerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Run_WhenVersionsOrIdsAreDuplicated_ShouldRejectBeforeAnyCorrectionRuns(bool duplicateVersion)
    {
        var first = Substitute.For<IApplicationDataCorrection>();
        first.Id.Returns("first");
        first.Version.Returns(1);
        var second = Substitute.For<IApplicationDataCorrection>();
        second.Id.Returns(duplicateVersion ? "second" : "first");
        second.Version.Returns(duplicateVersion ? 1 : 2);
        var subject = new ApplicationDataCorrectionRunner(
            Substitute.For<IMongoDatabase>(),
            [first, second],
            TimeProvider.System,
            Substitute.For<ILogger<ApplicationDataCorrectionRunner>>(),
            Options.Create(new ApplicationDataCorrectionOptions())
        );

        var action = () => subject.Run(TestContext.Current.CancellationToken);
        await action.Should().ThrowAsync<InvalidOperationException>();
        await first.DidNotReceive().Apply(Arg.Any<CancellationToken>());
        await second.DidNotReceive().Apply(Arg.Any<CancellationToken>());
    }
}
