namespace Defra.WasteObligations.Api.Services.Scheduling;

public sealed record ScheduledJobWork<TWork>(string Id, TWork Payload);
