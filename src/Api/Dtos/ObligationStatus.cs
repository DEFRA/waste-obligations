namespace Defra.WasteObligations.Api.Dtos;

public static class ObligationStatus
{
    public const string NoDataYet = nameof(NoDataYet);
    public const string Met = nameof(Met);
    public const string NotMet = nameof(NotMet);

    public static readonly string[] All = [NoDataYet, Met, NotMet];
}
