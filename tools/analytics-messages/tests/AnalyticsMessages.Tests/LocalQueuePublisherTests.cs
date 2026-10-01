using Amazon.SQS;
using Amazon.SQS.Model;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace Defra.WasteObligations.Tools.AnalyticsMessages.Tests;

public class LocalQueuePublisherTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_ShouldPassEncodedBodyAndAttributesToSqs(bool forceCompression)
    {
        const string queueUrl = "http://localhost:14566/000000000000/analytics_message_samples_queue";
        const string messageId = "sample-message-id";
        var message = MessageEncoder.Encode("{\"eventId\":\"sample\",\"name\":\"café 😀\"}", forceCompression);
        SendMessageRequest? request = null;
        var client = Substitute.For<IAmazonSQS>();
        client
            .SendMessageAsync(Arg.Do<SendMessageRequest>(x => request = x), TestContext.Current.CancellationToken)
            .Returns(new SendMessageResponse { MessageId = messageId });

        var result = await LocalQueuePublisher.Send(
            client,
            message,
            new Uri(queueUrl),
            TestContext.Current.CancellationToken
        );

        result.Should().Be(messageId);
        request.Should().NotBeNull();
        request.QueueUrl.Should().Be(queueUrl);
        request.MessageBody.Should().Be(message.Body);
        request.MessageAttributes.Should().HaveCount(forceCompression ? 2 : 1);
        request.MessageAttributes["Content-Type"].DataType.Should().Be("String");
        request.MessageAttributes["Content-Type"].StringValue.Should().Be("application/json");

        if (forceCompression)
        {
            request.MessageAttributes["Content-Encoding"].DataType.Should().Be("String");
            request.MessageAttributes["Content-Encoding"].StringValue.Should().Be("gzip+base64");
        }

        await client.Received(1).SendMessageAsync(Arg.Any<SendMessageRequest>(), TestContext.Current.CancellationToken);
    }
}
