using System.Diagnostics.CodeAnalysis;
using System.Net;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Defra.WasteObligations.Api.Utils.Health;

[ExcludeFromCodeCoverage]
public class SnsHealthCheck(IAmazonSimpleNotificationService simpleNotificationService, string topicArn) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await simpleNotificationService.GetTopicAttributesAsync(
                new GetTopicAttributesRequest { TopicArn = topicArn },
                cancellationToken
            );

            if (response.HttpStatusCode is not HttpStatusCode.OK)
                throw new InvalidOperationException($"Unexpected HTTP status code: {response.HttpStatusCode}");

            return HealthCheckResult.Healthy($"Connected to AWS topic: {topicArn}");
        }
        catch (Exception exception)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                exception: new Exception($"Failed to connect to AWS topic: {topicArn}", exception)
            );
        }
    }
}
