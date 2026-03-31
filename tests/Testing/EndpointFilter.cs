namespace Defra.WasteObligations.Testing;

public class EndpointFilter
{
    internal string Filter { get; }

    private EndpointFilter(string filter) => Filter = filter;

    public static EndpointFilter Year(int year) => new($"year={year}");
}
