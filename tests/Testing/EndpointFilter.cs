using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Extensions;

namespace Defra.WasteObligations.Testing;

public class EndpointFilter
{
    internal string Filter { get; }

    private EndpointFilter(string filter) => Filter = filter;

    public static EndpointFilter ObligationYear(int obligationYear) => new($"obligationYear={obligationYear}");

    public static EndpointFilter Status(ComplianceDeclarationStatus[] status) =>
        Status(string.Join(",", status.Select(x => x.ToJsonValue())));

    public static EndpointFilter Status(string status) => new($"status={status}");

    public static EndpointFilter OrganisationName(string organisationName) =>
        new($"organisationName={Uri.EscapeDataString(organisationName)}");

    public static EndpointFilter Page(int page) => new($"page={page}");

    public static EndpointFilter PageSize(int pageSize) => new($"pageSize={pageSize}");
}
