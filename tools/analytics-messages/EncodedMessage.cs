namespace Defra.WasteObligations.Tools.AnalyticsMessages;

public sealed record EncodedMessage(string Body, Dictionary<string, MessageAttribute> MessageAttributes);
