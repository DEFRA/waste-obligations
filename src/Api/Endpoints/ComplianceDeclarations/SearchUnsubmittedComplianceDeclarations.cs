using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.ComplianceDeclarations;

public static class SearchUnsubmittedComplianceDeclarations
{
    public const string OperationId = "SearchUnsubmittedComplianceDeclarations";

    public static void MapUnsubmittedComplianceDeclarationsSearch(this IEndpointRouteBuilder app)
    {
        app.MapGet("/compliance-declarations/unsubmitted", Handle)
            .WithName(OperationId)
            .WithTags("Search")
            .WithSummary("Search unsubmitted compliance declarations")
            .WithDescription("Returns eligible organisations without a submitted or accepted compliance declaration")
            .Produces<UnsubmittedOrganisationsPaged>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Read);
    }

    private static async Task<IResult> Handle(
        [AsParameters] UnsubmittedComplianceDeclarationsRequest request,
        [FromServices] IUnsubmittedOrganisationsService service,
        [FromServices] ICurrentComplianceYearProvider currentComplianceYearProvider,
        CancellationToken cancellationToken
    )
    {
        var sort = request.ParsedSort();
        if (sort.Any(x => x.Field is not ComplianceDeclarationSortField.OrganisationName))
        {
            return Results.BadRequest("Only OrganisationName sorting is currently supported");
        }

        if (request.ObligationYear!.Value != currentComplianceYearProvider.GetCurrentComplianceYear())
        {
            return Results.BadRequest("Only the current compliance year is supported");
        }

        var page = request.EffectivePage;
        var pageSize = request.EffectivePageSize;
        var result = await service.Search(
            request.ObligationYear!.Value,
            request.ParsedRegistrationType(),
            request.Search,
            sort,
            page,
            pageSize,
            cancellationToken
        );

        return Results.Ok(
            new UnsubmittedOrganisationsPaged
            {
                UnsubmittedOrganisations = result.Rows.Select(x => new UnsubmittedOrganisation
                {
                    OrganisationId = x.OrganisationId,
                    RegistrationType = x.RegistrationType.ToDto(),
                    OrganisationName = x.Name,
                    OrganisationReferenceNumber = x.ReferenceNumber,
                    RecyclingObligationsMet = x.RecyclingObligationsMet,
                    ObligationCoveragePercentage = x.ObligationCoveragePercentage,
                    ObligationDataState = x.ObligationDataState,
                    ObligationsAsOf = x.ObligationsAsOf,
                }),
                Total = result.Total,
                Page = page,
                PageSize = pageSize,
            }
        );
    }

    private static RegistrationType ToDto(this Data.Entities.RegistrationType registrationType) =>
        registrationType switch
        {
            Data.Entities.RegistrationType.DirectProducer => RegistrationType.DirectProducer,
            Data.Entities.RegistrationType.ComplianceScheme => RegistrationType.ComplianceScheme,
            _ => throw new ArgumentOutOfRangeException(nameof(registrationType)),
        };
}
