namespace Defra.WasteObligations.Testing;

public class EndpointQuery
{
    private readonly EndpointFilter[] _filters = [];

    public static EndpointQuery New => new();

    private EndpointQuery() { }

    private EndpointQuery(EndpointFilter[] filters) => _filters = filters;

    public EndpointQuery Where(EndpointFilter filter) => new([.. _filters, filter]);

    public override string ToString() =>
        _filters.Length == 0
            ? string.Empty
            : "?" + string.Join("&", _filters.Where(x => !string.IsNullOrWhiteSpace(x.Filter)).Select(x => x.Filter));
}
