using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Organisations.Obligations;

public static class ReadObligations
{
    public static void MapObligationsRead(this IEndpointRouteBuilder app)
    {
        app.MapGet("/organisations/{organisationId:guid}/obligations", Handle)
            .WithName("ReadOrganisationObligations")
            .WithTags("Obligations")
            .WithSummary("Obligations for an organisation by year")
            .WithDescription("Returns the obligations for an organisation by organisation ID for the specified year")
            .Produces<OrganisationObligations>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(PolicyNames.Read);
    }

    private static async Task<IResult> Handle(
        [FromRoute] Guid organisationId,
        [AsParameters] ReadObligationsRequest request,
        [FromServices] IWasteOrganisationsService wasteOrganisationsService,
        [FromServices] IPrnCommonBackendService prnCommonBackendService,
        [FromServices] ILogger<ReadObligationsLatency> logger,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var latency = new ReadObligationsLatency(TimeProvider.System.GetTimestamp());
        httpContext.Items[ReadObligationsLatency.HttpContextItemKey] = latency;
        var obligationYear = request.ObligationYearValue;
        var downstreamStartingTimestamp = TimeProvider.System.GetTimestamp();
        var organisationTask = Measure(
            () => wasteOrganisationsService.Read(organisationId, cancellationToken),
            duration => latency.WasteOrganisationsDurationMilliseconds = duration
        );
        var obligationsTask = prnCommonBackendService.ReadObligations(
            organisationId,
            obligationYear,
            cancellationToken
        );

        await Task.WhenAll(organisationTask, obligationsTask);
        latency.ParallelDownstreamDurationMilliseconds = TimeProvider
            .System.GetElapsedTime(downstreamStartingTimestamp)
            .TotalMilliseconds;

        var organisation = await organisationTask;
        if (organisation is null)
            return Results.NotFound();

        var obligations = await obligationsTask;
        var responseMappingStartingTimestamp = TimeProvider.System.GetTimestamp();
        var response = new OrganisationObligations { Obligations = obligations.Select(x => x.ToDto()).ToArray() };
        latency.ResponseMappingDurationMilliseconds = TimeProvider
            .System.GetElapsedTime(responseMappingStartingTimestamp)
            .TotalMilliseconds;

        httpContext.Response.OnCompleted(() => LogLatency(httpContext.Response, latency, logger));

        return Results.Ok(response);
    }

    private static async Task<T> Measure<T>(Func<Task<T>> action, Action<double> recordDuration)
    {
        var startingTimestamp = TimeProvider.System.GetTimestamp();

        try
        {
            return await action();
        }
        finally
        {
            recordDuration(TimeProvider.System.GetElapsedTime(startingTimestamp).TotalMilliseconds);
        }
    }

    private static Task LogLatency(
        HttpResponse response,
        ReadObligationsLatency latency,
        ILogger<ReadObligationsLatency> logger
    )
    {
        if (response.StatusCode != StatusCodes.Status200OK)
            return Task.CompletedTask;

        logger.LogInformation(
            "Read organisation obligations latency: {TotalDurationMilliseconds}ms total, "
                + "{WasteOrganisationsDurationMilliseconds}ms Waste Organisations, "
                + "{PrnTokenDurationMilliseconds}ms PRN token, "
                + "{PrnObligationCalculationDurationMilliseconds}ms PRN obligation calculation, "
                + "{PrnCommonBackendDurationMilliseconds}ms PRN Common Backend, "
                + "{ParallelDownstreamDurationMilliseconds}ms parallel downstream, "
                + "{ResponseMappingDurationMilliseconds}ms response mapping",
            TimeProvider.System.GetElapsedTime(latency.StartingTimestamp).TotalMilliseconds,
            latency.WasteOrganisationsDurationMilliseconds,
            latency.PrnTokenDurationMilliseconds,
            latency.PrnObligationCalculationDurationMilliseconds,
            latency.PrnCommonBackendDurationMilliseconds,
            latency.ParallelDownstreamDurationMilliseconds,
            latency.ResponseMappingDurationMilliseconds
        );

        return Task.CompletedTask;
    }
}
