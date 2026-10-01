using System.Text;
using System.Text.Json;
using Amazon.SQS;
using AwesomeAssertions;
using Xunit;

namespace Defra.WasteObligations.Tools.AnalyticsMessages.Tests;

public class MessageGeneratorTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"analytics-messages-tests-{Guid.NewGuid():N}"
    );

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Run_WhenValidInput_ShouldWriteConsumerMessageAndPublishFiles(bool forceCompression)
    {
        const string input = "{\n  \"eventId\": \"sample\", \"entityId\": \"cdec_sample\", \"name\": \"café 😀\"\n}\n";
        var inputPath = await WriteInput(input);
        var outputPath = Path.Combine(_directory, "output");
        string[] args = forceCompression ? [inputPath, outputPath, "--force-compression"] : [inputPath, outputPath];
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await MessageGenerator.Run(args, output, error);

        exitCode.Should().Be(0);
        error.ToString().Should().BeEmpty();
        var bodyBytes = await File.ReadAllBytesAsync(
            Path.Combine(outputPath, "message-body.txt"),
            TestContext.Current.CancellationToken
        );
        bodyBytes.AsSpan().StartsWith(Encoding.UTF8.Preamble).Should().BeFalse();
        var body = Encoding.UTF8.GetString(bodyBytes);
        using var attributes = JsonDocument.Parse(
            await File.ReadAllTextAsync(
                Path.Combine(outputPath, "message-attributes.json"),
                TestContext.Current.CancellationToken
            )
        );
        using var message = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(outputPath, "message.json"), TestContext.Current.CancellationToken)
        );
        message.RootElement.GetProperty("Body").GetString().Should().Be(body);
        JsonElement
            .DeepEquals(message.RootElement.GetProperty("MessageAttributes"), attributes.RootElement)
            .Should()
            .BeTrue();
        attributes.RootElement.GetProperty("Content-Type").GetProperty("DataType").GetString().Should().Be("String");
        attributes
            .RootElement.GetProperty("Content-Type")
            .GetProperty("StringValue")
            .GetString()
            .Should()
            .Be("application/json");
        attributes.RootElement.TryGetProperty("Content-Encoding", out _).Should().Be(forceCompression);

        if (!forceCompression)
        {
            body.Should().Be(input);
        }
    }

    [Fact]
    public async Task Run_WhenUtf8BomPresent_ShouldExcludeBomFromMessageBody()
    {
        const string input = "{\"eventId\":\"sample\"}";
        var inputPath = await WriteInput(input, new UTF8Encoding(true));
        var outputPath = Path.Combine(_directory, "output");

        var exitCode = await MessageGenerator.Run([inputPath, outputPath], TextWriter.Null, TextWriter.Null);

        exitCode.Should().Be(0);
        (
            await File.ReadAllBytesAsync(
                Path.Combine(outputPath, "message-body.txt"),
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Equal(Encoding.UTF8.GetBytes(input));
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task Run_WhenInputIsNotJsonObject_ShouldFailBeforeCreatingOutput(string input)
    {
        var inputPath = await WriteInput(input);
        var outputPath = Path.Combine(_directory, "output");
        using var error = new StringWriter();

        var exitCode = await MessageGenerator.Run([inputPath, outputPath], TextWriter.Null, error);

        exitCode.Should().Be(1);
        error.ToString().Should().NotBeEmpty();
        Directory.Exists(outputPath).Should().BeFalse();
    }

    [Fact]
    public async Task Run_WhenOutputContainsFiles_ShouldPreserveExistingFiles()
    {
        const string original = "existing message";
        var inputPath = await WriteInput("{}");
        var outputPath = Path.Combine(_directory, "output");
        Directory.CreateDirectory(outputPath);
        var existingPath = Path.Combine(outputPath, "message-body.txt");
        await File.WriteAllTextAsync(existingPath, original, TestContext.Current.CancellationToken);

        var sent = false;
        var exitCode = await MessageGenerator.Run(
            [
                inputPath,
                outputPath,
                "--queue-url",
                "http://localhost:14566/000000000000/analytics_message_samples_queue",
            ],
            TextWriter.Null,
            TextWriter.Null,
            (_, _) =>
            {
                sent = true;

                return Task.FromResult("sample-message-id");
            }
        );

        exitCode.Should().Be(1);
        (await File.ReadAllTextAsync(existingPath, TestContext.Current.CancellationToken)).Should().Be(original);
        Directory.GetFiles(outputPath).Should().ContainSingle();
        sent.Should().BeFalse();
    }

    [Fact]
    public async Task Run_WhenUnknownOption_ShouldPrintUsageAndFail()
    {
        using var error = new StringWriter();

        var exitCode = await MessageGenerator.Run(["input.json", "output", "--unknown"], TextWriter.Null, error);

        exitCode.Should().Be(2);
        error.ToString().Should().Contain("Usage:");
    }

    [Fact]
    public async Task Run_WhenInputHasInvalidUtf8_ShouldFailBeforeCreatingOutput()
    {
        var inputPath = await WriteInput("{}");
        await File.WriteAllBytesAsync(inputPath, [0xff, 0xff], TestContext.Current.CancellationToken);
        var outputPath = Path.Combine(_directory, "output");

        var exitCode = await MessageGenerator.Run([inputPath, outputPath], TextWriter.Null, TextWriter.Null);

        exitCode.Should().Be(1);
        Directory.Exists(outputPath).Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Run_WhenQueueRequested_ShouldSendTheGeneratedMessage(bool forceCompression, bool queueOptionFirst)
    {
        const string queueUrl = "http://localhost:14566/000000000000/analytics_message_samples_queue";
        const string messageId = "sample-message-id";
        var inputPath = await WriteInput("{\"eventId\":\"sample\",\"name\":\"café 😀\"}");
        var outputPath = Path.Combine(_directory, "output");
        string[] queueOptions = ["--queue-url", queueUrl];
        string[] compressionOptions = forceCompression ? ["--force-compression"] : [];
        string[] args = queueOptionFirst
            ? [inputPath, outputPath, .. queueOptions, .. compressionOptions]
            : [inputPath, outputPath, .. compressionOptions, .. queueOptions];
        EncodedMessage? sent = null;
        Uri? destination = null;
        using var output = new StringWriter();

        var exitCode = await MessageGenerator.Run(
            args,
            output,
            TextWriter.Null,
            (message, uri) =>
            {
                sent = message;
                destination = uri;
                File.Exists(Path.Combine(outputPath, "message.json")).Should().BeTrue();

                return Task.FromResult(messageId);
            }
        );

        exitCode.Should().Be(0);
        sent.Should().NotBeNull();
        destination.Should().Be(new Uri(queueUrl));
        sent.Body.Should()
            .Be(
                await File.ReadAllTextAsync(
                    Path.Combine(outputPath, "message-body.txt"),
                    TestContext.Current.CancellationToken
                )
            );
        sent.MessageAttributes.ContainsKey("Content-Encoding").Should().Be(forceCompression);
        output.ToString().Should().Contain($"Sent message {messageId}");
    }

    [Fact]
    public async Task Run_WhenQueueSendFails_ShouldKeepFilesAndReturnFailure()
    {
        var inputPath = await WriteInput("{}");
        var outputPath = Path.Combine(_directory, "output");
        using var error = new StringWriter();

        var exitCode = await MessageGenerator.Run(
            [
                inputPath,
                outputPath,
                "--queue-url",
                "http://localhost:14566/000000000000/analytics_message_samples_queue",
            ],
            TextWriter.Null,
            error,
            (_, _) => Task.FromException<string>(new AmazonSQSException("Queue does not exist"))
        );

        exitCode.Should().Be(1);
        error.ToString().Should().Contain("Queue does not exist");
        Directory.GetFiles(outputPath).Should().HaveCount(3);
        (
            await File.ReadAllTextAsync(
                Path.Combine(outputPath, "message-body.txt"),
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be("{}");
    }

    [Theory]
    [InlineData("--queue-url")]
    [InlineData("--queue-url", "not-a-url")]
    [InlineData("--queue-url", "https://localhost:14566/account/queue")]
    [InlineData("--queue-url", "http://example.com/account/queue")]
    [InlineData("--queue-url", "http://localhost:14566")]
    [InlineData("--queue-url", "http://test:test@localhost:14566/account/queue")]
    [InlineData("--queue-url", "http://localhost:14566/account/queue?query=value")]
    [InlineData("--queue-url", "http://localhost:14566/account/queue#fragment")]
    [InlineData(
        "--queue-url",
        "http://localhost:14566/account/queue",
        "--queue-url",
        "http://localhost:14566/account/queue"
    )]
    [InlineData("--force-compression", "--force-compression")]
    public async Task Run_WhenInvalidQueueOptions_ShouldFailBeforeWritingOrSending(params string[] options)
    {
        var outputPath = Path.Combine(_directory, "output");
        var sent = false;
        using var error = new StringWriter();

        var exitCode = await MessageGenerator.Run(
            ["missing-input.json", outputPath, .. options],
            TextWriter.Null,
            error,
            (_, _) =>
            {
                sent = true;

                return Task.FromResult("sample-message-id");
            }
        );

        exitCode.Should().Be(2);
        error.ToString().Should().Contain("Usage:");
        Directory.Exists(outputPath).Should().BeFalse();
        sent.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://127.0.0.1:14566/000000000000/analytics_message_samples_queue")]
    [InlineData("http://[::1]:14566/000000000000/analytics_message_samples_queue")]
    [InlineData("http://floci:4566/000000000000/analytics_message_samples_queue")]
    public void Parse_ShouldSupportHostLoopbackAndComposeQueueUrls(string queueUrl)
    {
        var options = MessageGeneratorOptions.Parse(["input.json", "output", "--queue-url", queueUrl]);

        options.QueueUrl.Should().Be(new Uri(queueUrl));
    }

    private async Task<string> WriteInput(string input, Encoding? encoding = null)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "input.json");
        await File.WriteAllTextAsync(
            path,
            input,
            encoding ?? new UTF8Encoding(false),
            TestContext.Current.CancellationToken
        );

        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }

        GC.SuppressFinalize(this);
    }
}
