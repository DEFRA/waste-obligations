using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Amazon.CloudWatch.EMF.Model;

namespace Defra.WasteObligations.Api.Utils.Metrics;

[ExcludeFromCodeCoverage]
public class EmailMetrics : IEmailMetrics
{
    private readonly Histogram<double> _sendDuration;
    private readonly Counter<long> _send;
    private readonly Counter<long> _sendErrors;
    private readonly Counter<long> _sendActive;

    public EmailMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Metrics.MeterName);

        _sendDuration = meter.CreateHistogram<double>(
            Metrics.Names.EmailSendDuration,
            nameof(Unit.MILLISECONDS),
            "Elapsed time spent sending an email"
        );
        _send = meter.CreateCounter<long>(Metrics.Names.EmailSend, nameof(Unit.COUNT), "Count of email send attempts");
        _sendErrors = meter.CreateCounter<long>(
            Metrics.Names.EmailSendErrors,
            nameof(Unit.COUNT),
            "Count of email send failures"
        );
        _sendActive = meter.CreateCounter<long>(
            Metrics.Names.EmailSendActive,
            nameof(Unit.COUNT),
            "Count of email sends in progress"
        );
    }

    public void SendStarted(string templateName, string language)
    {
        var tagList = BuildTags(templateName, language);

        _send.Add(1, tagList);
        _sendActive.Add(1, tagList);
    }

    public void SendCompleted(string templateName, string language, double milliseconds)
    {
        var tagList = BuildTags(templateName, language);

        _sendActive.Add(-1, tagList);
        _sendDuration.Record(milliseconds, tagList);
    }

    public void SendFaulted(string templateName, string language, Exception exception)
    {
        var tagList = BuildTags(templateName, language);
        tagList.Add(Metrics.Tags.ExceptionType, exception.GetType().Name);

        _sendErrors.Add(1, tagList);
    }

    private static TagList BuildTags(string templateName, string language) =>
        new()
        {
            { Metrics.Tags.Service, Process.GetCurrentProcess().ProcessName },
            { Metrics.Tags.TemplateName, templateName },
            { Metrics.Tags.Language, language },
        };
}
