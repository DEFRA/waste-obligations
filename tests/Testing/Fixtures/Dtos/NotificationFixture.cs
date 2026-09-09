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
            ["certOrStatementBullet"] =
                "acquire enough packaging waste recycling notes (PRNs) or packaging waste export recycling notes (PERNs) to meet your recycling obligations",
            ["certOrStatementBullet_cy"] =
                "ennill digon o nodau ailgylchu gwastraff pecynnu (PRNs) neu nodau ailgylchu allforio gwastraff pecynnu (PERNs) i fodloni eich rhwymedigaethau ailgylchu",
            ["certOrStatementBullet2"] =
                "acquire enough packaging waste recycling notes (PRNs) or packaging waste export recycling notes (PERNs) to meet your revised recycling obligations",
            ["certOrStatementBullet2_cy"] =
                "ennill digon o nodau ailgylchu gwastraff pecynnu (PRNs) neu nodau ailgylchu allforio gwastraff pecynnu (PERNs) i fodloni eich rhwymedigaethau ailgylchu wedi'u hadolygu",
            ["regulator_cy"] = regulatorWelsh,
        };

    public static IReadOnlyDictionary<string, string> ComplianceSchemeCancellationParameters(
        string regulatorWelsh = "Regulator"
    ) =>
        new Dictionary<string, string>
        {
            ["certOrStatement"] = "statement",
            ["certOrStatement_cy"] = "datganiad",
            ["certOrStatementBullet"] = "meet your recycling obligations",
            ["certOrStatementBullet_cy"] = "bodloni eich rhwymedigaethau ailgylchu",
            ["certOrStatementBullet2"] = "meet your revised recycling obligations",
            ["certOrStatementBullet2_cy"] = "bodloni eich rhwymedigaethau ailgylchu wedi'u hadolygu",
            ["regulator_cy"] = regulatorWelsh,
        };

    public static NotificationRequest DirectProducerCancellation(string regulatorWelsh = "Regulator") =>
        WithParameters(DirectProducerCancellationParameters(regulatorWelsh));

    public static NotificationRequest ComplianceSchemeCancellation(string regulatorWelsh = "Regulator") =>
        WithParameters(ComplianceSchemeCancellationParameters(regulatorWelsh));

    public static NotificationRequest WithParameters(IReadOnlyDictionary<string, string> parameters) =>
        new() { Parameters = parameters };
}
