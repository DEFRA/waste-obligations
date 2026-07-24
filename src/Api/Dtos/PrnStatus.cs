namespace Defra.WasteObligations.Api.Dtos;

public static class PrnStatus
{
    public const string AwaitingAcceptance = nameof(AwaitingAcceptance);
    public const string Accepted = nameof(Accepted);
    public const string Rejected = nameof(Rejected);
    public const string AwaitingCancellation = nameof(AwaitingCancellation);
    public const string Cancelled = nameof(Cancelled);
}
