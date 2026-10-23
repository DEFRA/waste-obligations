namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public static class PrnStatusConflict
{
    // The common backend applies a status update to every PRN or to none, so a
    // conflict always means nothing was changed.
    public const string Message = "One or more PRNs are not in the status this update requires. No PRNs were updated.";
}
