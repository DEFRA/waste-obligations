using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Organisations.Prns;

public static class SearchPrns
{
    public const string OperationId = "SearchOrganisationPrns";

    public static void MapPrnsSearch(this IEndpointRouteBuilder app)
    {
        app.MapGet("/organisations/{organisationId:guid}/prns", Handle)
            .WithName(OperationId)
            .WithTags("PRNs")
            .WithSummary("Search organisation PRNs")
            .WithDescription("Returns a paged list of PRNs for an organisation")
            .Produces<PrnsPaged>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Read);
    }

    private static async Task<IResult> Handle(
        [FromRoute] Guid organisationId,
        [AsParameters] SearchOrganisationPrnsRequest request,
        [FromServices] IWasteOrganisationsService wasteOrganisationsService,
        [FromServices] IPrnCommonBackendService prnCommonBackendService,
        CancellationToken cancellationToken
    )
    {
        var page = request.EffectivePage;
        var pageSize = request.EffectivePageSize;
        var organisationTask = wasteOrganisationsService.Read(organisationId, cancellationToken);
        var prnsTask = prnCommonBackendService.SearchPrns(
            organisationId,
            new PrnSearchRequest
            {
                Page = page,
                PageSize = pageSize,
                Search = request.Search,
                FilterBy = ToFilterBy(request.ParsedStatus(), request.ParsedMaterial()),
                SortBy = ToSortBy(request.ParsedSort()),
            },
            cancellationToken
        );

        await Task.WhenAll(organisationTask, prnsTask);

        var organisation = await organisationTask;
        var searchResponse = await prnsTask;

        if (organisation is null)
            return Results.NotFound();

        var prns = searchResponse.Items.Select(x => x.ToDto()).ToList();
        if (prns.Any(x => x.Recipient.OrganisationId != organisationId))
            return Results.NotFound();

        return Results.Ok(
            new PrnsPaged
            {
                Prns = prns,
                Total = searchResponse.TotalItems,
                Page = page,
                PageSize = pageSize,
            }
        );
    }

    // material filtering is only supported by the common backend combined with the
    // AwaitingAcceptance status filter - SearchOrganisationPrnsRequest rejects any other
    // combination before this is reached, so material is only ever non-null here alongside
    // status == AwaitingAcceptance.
    private static string? ToFilterBy(OrganisationPrnStatus? status, OrganisationPrnMaterial? material) =>
        material switch
        {
            OrganisationPrnMaterial.Aluminium => "awaiting-aluminium",
            OrganisationPrnMaterial.Glass => "awaiting-glassother",
            OrganisationPrnMaterial.GlassRemelt => "awaiting-glassremelt",
            OrganisationPrnMaterial.Paper => "awaiting-paperfiber",
            OrganisationPrnMaterial.Plastic => "awaiting-plastic",
            OrganisationPrnMaterial.Steel => "awaiting-steel",
            OrganisationPrnMaterial.Wood => "awaiting-wood",
            null => status switch
            {
                null => null,
                OrganisationPrnStatus.AwaitingAcceptance => "awaiting-all",
                OrganisationPrnStatus.Accepted => "accepted-all",
                OrganisationPrnStatus.Rejected => "rejected-all",
                OrganisationPrnStatus.Cancelled => "cancelled-all",
                _ => throw new ArgumentOutOfRangeException(nameof(status)),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(material)),
        };

    private static string? ToSortBy(OrganisationPrnSort? sort) =>
        sort switch
        {
            null => null,
            OrganisationPrnSort.IssuedAtDescending => "date-issued-desc",
            OrganisationPrnSort.IssuedAtAscending => "date-issued-asc",
            OrganisationPrnSort.TonnageDescending => "tonnage-desc",
            OrganisationPrnSort.TonnageAscending => "tonnage-asc",
            OrganisationPrnSort.IssuerDescending => "issued-by-desc",
            OrganisationPrnSort.IssuerAscending => "issued-by-asc",
            OrganisationPrnSort.DecemberWasteDescending => "december-waste-desc",
            OrganisationPrnSort.MaterialDescending => "material-desc",
            OrganisationPrnSort.MaterialAscending => "material-asc",
            _ => throw new ArgumentOutOfRangeException(nameof(sort)),
        };
}
