using Defra.WasteObligations.HealthCheck;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};
using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
using var client = new HttpClient(handler)
{
    Timeout = Timeout.InfiniteTimeSpan,
    MaxResponseContentBufferSize = 1_048_576,
};

return await HealthCheckCommand.Run(args, client, Console.Out, Console.Error, cancellation.Token);
