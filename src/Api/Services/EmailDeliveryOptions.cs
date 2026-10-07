using System.Text.Json;

namespace Defra.WasteObligations.Api.Services;

public sealed record EmailDeliveryOptions
{
    public const string SectionName = "EmailDelivery";

    public string? EmailDeliveryCutoverUtc { get; init; }

    public bool TryReadCutover(out DateTimeOffset? cutover)
    {
        cutover = null;
        if (EmailDeliveryCutoverUtc is null)
            return true;

        if (
            !(
                EmailDeliveryCutoverUtc.EndsWith('Z')
                || EmailDeliveryCutoverUtc.EndsWith("+00:00", StringComparison.Ordinal)
                || EmailDeliveryCutoverUtc.EndsWith("-00:00", StringComparison.Ordinal)
            )
            || !JsonSerializer.SerializeToElement(EmailDeliveryCutoverUtc).TryGetDateTimeOffset(out var timestamp)
            || timestamp.Offset != TimeSpan.Zero
        )
            return false;

        cutover = new DateTimeOffset(timestamp.Ticks - timestamp.Ticks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);

        return true;
    }
}
