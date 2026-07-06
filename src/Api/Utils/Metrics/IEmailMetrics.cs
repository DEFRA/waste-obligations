namespace Defra.WasteObligations.Api.Utils.Metrics;

public interface IEmailMetrics
{
    void SendStarted(string templateName, string language);

    void SendCompleted(string templateName, string language, double milliseconds);

    void SendFaulted(string templateName, string language, Exception exception);
}
