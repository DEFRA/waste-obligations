using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Testing.Fixtures.Dtos;

public static class NotificationFixture
{
    public static IReadOnlyDictionary<string, string> DirectProducerCancellationParameters(
        string regulatorWelsh = "Regulator"
    ) =>
        new Dictionary<string, string>
        {
            ["certOrStatement"] = "certificate",
            ["certOrStatement_cy"] = "tystysgrif",
            ["regulator_cy"] = regulatorWelsh,
        };

    public static IReadOnlyDictionary<string, string> ComplianceSchemeCancellationParameters(
        string regulatorWelsh = "Regulator"
    ) =>
        new Dictionary<string, string>
        {
            ["certOrStatement"] = "statement",
            ["certOrStatement_cy"] = "datganiad",
            ["regulator_cy"] = regulatorWelsh,
        };

    public static NotificationRequest DirectProducerCancellation(string regulatorWelsh = "Regulator") =>
        WithParameters(DirectProducerCancellationParameters(regulatorWelsh));

    public static NotificationRequest ComplianceSchemeCancellation(string regulatorWelsh = "Regulator") =>
        WithParameters(ComplianceSchemeCancellationParameters(regulatorWelsh));

    public static NotificationRequest WithParameters(IReadOnlyDictionary<string, string> parameters) =>
        new() { Parameters = parameters };
}
