using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace Defra.WasteObligations.Tools.AnalyticsMessages;

public static class LocalQueuePublisher
{
    public static async Task<string> Send(EncodedMessage message, Uri queueUrl)
    {
        const int timeoutSeconds = 10;

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        var config = new AmazonSQSConfig
        {
            ServiceURL = queueUrl.GetLeftPart(UriPartial.Authority),
            AuthenticationRegion = "eu-west-2",
            MaxErrorRetry = 0,
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
        };
        using var client = new AmazonSQSClient(new BasicAWSCredentials("test", "test"), config);

        return await Send(client, message, queueUrl, cancellation.Token);
    }

    public static async Task<string> Send(
        IAmazonSQS client,
        EncodedMessage message,
        Uri queueUrl,
        CancellationToken cancellationToken
    )
    {
        var response = await client.SendMessageAsync(
            new SendMessageRequest
            {
                QueueUrl = queueUrl.AbsoluteUri,
                MessageBody = message.Body,
                MessageAttributes = message.MessageAttributes.ToDictionary(
                    x => x.Key,
                    x => new MessageAttributeValue { DataType = x.Value.DataType, StringValue = x.Value.StringValue }
                ),
            },
            cancellationToken
        );

        return response.MessageId;
    }
}
