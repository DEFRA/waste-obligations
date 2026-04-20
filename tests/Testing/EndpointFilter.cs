namespace Defra.WasteObligations.Testing;

public class EndpointFilter
{
    internal string Filter { get; }

    private EndpointFilter(string filter) => Filter = filter;

    public static EndpointFilter ObligationYear(int obligationYear) => new($"obligationYear={obligationYear}");

    public static EndpointFilter Include(string? type) => new($"include={type}");
}
