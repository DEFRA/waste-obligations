using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Testing.Fixtures.Dtos;

public static class NotificationFixture
{
    public static IReadOnlyDictionary<string, string> DirectProducerCancellationParameters(
        string environmentalRegulatorWelsh = "Regulator"
    ) =>
        new Dictionary<string, string>
        {
            ["certOrStatement"] = "certificate",
            ["certOrStatement_cy"] = "tystysgrif",
            ["environmentalRegulator_cy"] = environmentalRegulatorWelsh,
        };

    public static IReadOnlyDictionary<string, string> ComplianceSchemeCancellationParameters(
        string environmentalRegulatorWelsh = "Regulator"
    ) =>
        new Dictionary<string, string>
        {
            ["certOrStatement"] = "statement",
            ["certOrStatement_cy"] = "datganiad",
            ["environmentalRegulator_cy"] = environmentalRegulatorWelsh,
        };

    public static NotificationRequest DirectProducerCancellation(string environmentalRegulatorWelsh = "Regulator") =>
        WithParameters(DirectProducerCancellationParameters(environmentalRegulatorWelsh));

    public static NotificationRequest ComplianceSchemeCancellation(string environmentalRegulatorWelsh = "Regulator") =>
        WithParameters(ComplianceSchemeCancellationParameters(environmentalRegulatorWelsh));

    public static NotificationRequest WithParameters(IReadOnlyDictionary<string, string> parameters) =>
        new() { Parameters = parameters };
}
