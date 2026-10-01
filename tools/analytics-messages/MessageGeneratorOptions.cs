namespace Defra.WasteObligations.Tools.AnalyticsMessages;

public sealed record MessageGeneratorOptions(
    string InputPath,
    string OutputDirectory,
    bool ForceCompression,
    Uri? QueueUrl
)
{
    public static MessageGeneratorOptions Parse(string[] args)
    {
        if (
            args.Length < 2
            || args.Take(2).Any(x => string.IsNullOrWhiteSpace(x) || x.StartsWith("--", StringComparison.Ordinal))
        )
        {
            throw new ArgumentException("An input JSON file and output directory are required.");
        }

        var forceCompression = false;
        Uri? queueUrl = null;

        for (var i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--force-compression" when !forceCompression:
                    forceCompression = true;
                    break;
                case "--queue-url" when queueUrl is null && i + 1 < args.Length:
                    queueUrl = ParseQueueUrl(args[++i]);
                    break;
                default:
                    throw new ArgumentException($"Unknown, repeated or incomplete option: {args[i]}");
            }
        }

        return new MessageGeneratorOptions(args[0], args[1], forceCompression, queueUrl);
    }

    private static Uri ParseQueueUrl(string value)
    {
        if (
            Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttp
            && (uri.IsLoopback || uri.Host.Equals("floci", StringComparison.OrdinalIgnoreCase))
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)
            && uri.AbsolutePath != "/"
        )
        {
            return uri;
        }

        throw new ArgumentException(
            "Queue URL must be an HTTP SQS URL on localhost, a loopback address, or the Compose floci service, without credentials, query or fragment."
        );
    }
}
