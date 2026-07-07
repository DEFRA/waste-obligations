using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Amazon.CloudWatch.EMF.Model;
using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Utils.Metrics;

[ExcludeFromCodeCoverage]
public class ComplianceDeclarationMetrics : IComplianceDeclarationMetrics
{
    private readonly Counter<long> _created;
    private readonly Counter<long> _updated;
    private readonly Counter<long> _deleted;

    public ComplianceDeclarationMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Metrics.MeterName);

        _created = meter.CreateCounter<long>(
            Metrics.Names.ComplianceDeclarationCreated,
            nameof(Unit.COUNT),
            "Count of compliance declarations created"
        );
        _updated = meter.CreateCounter<long>(
            Metrics.Names.ComplianceDeclarationUpdated,
            nameof(Unit.COUNT),
            "Count of compliance declarations updated"
        );
        _deleted = meter.CreateCounter<long>(
            Metrics.Names.ComplianceDeclarationDeleted,
            nameof(Unit.COUNT),
            "Count of compliance declarations deleted"
        );
    }

    public void Created()
    {
        _created.Add(1, BuildTags());
    }

    public void Updated(ComplianceDeclarationStatus status)
    {
        var tagList = BuildTags();
        tagList.Add(Metrics.Tags.ComplianceDeclarationStatus, status.ToString());

        _updated.Add(1, tagList);
    }

    public void Deleted()
    {
        _deleted.Add(1, BuildTags());
    }

    private static TagList BuildTags() => new() { { Metrics.Tags.Service, Process.GetCurrentProcess().ProcessName } };
}
