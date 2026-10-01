using System.Text;
using System.Text.Json;
using Amazon.Runtime;

namespace Defra.WasteObligations.Tools.AnalyticsMessages;

public static class MessageGenerator
{
    private const string Usage =
        "Usage: analytics-messages <input.json> <output-directory> [--force-compression] [--queue-url <local-sqs-url>]";
    private static readonly UTF8Encoding FileEncoding = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task<int> Run(
        string[] args,
        TextWriter output,
        TextWriter error,
        Func<EncodedMessage, Uri, Task<string>>? sendMessage = null
    )
    {
        if (args is ["--help"] or ["-h"])
        {
            await output.WriteLineAsync(Usage);

            return 0;
        }

        MessageGeneratorOptions options;

        try
        {
            options = MessageGeneratorOptions.Parse(args);
        }
        catch (ArgumentException exception)
        {
            await error.WriteLineAsync(exception.Message);
            await error.WriteLineAsync(Usage);

            return 2;
        }

        try
        {
            var inputBytes = await File.ReadAllBytesAsync(options.InputPath);
            var offset = inputBytes.AsSpan().StartsWith(Encoding.UTF8.Preamble) ? Encoding.UTF8.Preamble.Length : 0;
            var serializedMessage = FileEncoding.GetString(inputBytes, offset, inputBytes.Length - offset);
            using var document = JsonDocument.Parse(serializedMessage);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("The input must be a UTF-8 JSON object containing the event to send.");
            }

            var message = MessageEncoder.Encode(serializedMessage, options.ForceCompression);
            var outputDirectory = Path.GetFullPath(options.OutputDirectory);

            if (Directory.Exists(outputDirectory) && Directory.EnumerateFileSystemEntries(outputDirectory).Any())
            {
                throw new IOException("The output directory must be empty; existing files will not be overwritten.");
            }

            Directory.CreateDirectory(outputDirectory);
            await WriteFile(outputDirectory, "message-body.txt", message.Body);
            await WriteFile(
                outputDirectory,
                "message-attributes.json",
                JsonSerializer.Serialize(message.MessageAttributes, JsonOptions)
            );
            await WriteFile(outputDirectory, "message.json", JsonSerializer.Serialize(message, JsonOptions));
            var encoding = message.MessageAttributes.TryGetValue("Content-Encoding", out var attribute)
                ? attribute.StringValue
                : "plain JSON";
            await output.WriteLineAsync(
                $"Wrote {encoding} message to {outputDirectory} ({Encoding.UTF8.GetByteCount(serializedMessage)} input bytes, {Encoding.UTF8.GetByteCount(message.Body)} body bytes)."
            );

            if (options.QueueUrl is not null)
            {
                var publish = sendMessage ?? LocalQueuePublisher.Send;
                var messageId = await publish(message, options.QueueUrl);
                await output.WriteLineAsync($"Sent message {messageId} to {options.QueueUrl}.");
            }

            return 0;
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or IOException
                        or UnauthorizedAccessException
                        or JsonException
                        or InvalidOperationException
                        or AmazonServiceException
                        or AmazonClientException
                        or HttpRequestException
                        or OperationCanceledException
            )
        {
            await error.WriteLineAsync(exception.Message);

            return 1;
        }
    }

    private static async Task WriteFile(string directory, string name, string content)
    {
        await using var stream = new FileStream(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write);
        await using var writer = new StreamWriter(stream, FileEncoding);
        await writer.WriteAsync(content);
    }
}
