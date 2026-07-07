using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Utils.Metrics;
using Microsoft.Extensions.DependencyInjection;
using ApiMetrics = Defra.WasteObligations.Api.Utils.Metrics.Metrics;

namespace Defra.WasteObligations.Api.Tests.Utils.Metrics;

public class ComplianceDeclarationMetricsTests
{
    [Fact]
    public void Created_ShouldIncrementCreatedCounter()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.ComplianceDeclarationCreated
        );
        var subject = new ComplianceDeclarationMetrics(meterFactory);

        subject.Created();

        var measurements = collector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(1);
        measurements[0].ContainsTags(ApiMetrics.Tags.Service).Should().BeTrue();
    }

    [Fact]
    public void Updated_ShouldIncrementUpdatedCounterWithStatus()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.ComplianceDeclarationUpdated
        );
        var subject = new ComplianceDeclarationMetrics(meterFactory);

        subject.Updated(ComplianceDeclarationStatus.Accepted);

        var measurements = collector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(1);
        measurements[0].Tags[ApiMetrics.Tags.ComplianceDeclarationStatus].Should().Be("Accepted");
    }

    [Fact]
    public void Deleted_ShouldIncrementDeletedCounter()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TestMetricCollector<long>(
            ApiMetrics.MeterName,
            ApiMetrics.Names.ComplianceDeclarationDeleted
        );
        var subject = new ComplianceDeclarationMetrics(meterFactory);

        subject.Deleted();

        var measurements = collector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(1);
        measurements[0].ContainsTags(ApiMetrics.Tags.Service).Should().BeTrue();
    }

    private static IMeterFactory CreateMeterFactory()
    {
        var services = new ServiceCollection();
        services.AddMetrics();

        return services.BuildServiceProvider().GetRequiredService<IMeterFactory>();
    }
}
