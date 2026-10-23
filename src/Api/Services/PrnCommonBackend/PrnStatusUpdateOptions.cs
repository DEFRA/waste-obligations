namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

// How a bulk status update is applied: the status each PRN must still be in (when given),
// and the message for the 409 if the common backend rejects the update.
public sealed record PrnStatusUpdateOptions(string? ExpectedStatus, string ConflictMessage);
