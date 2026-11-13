using System.ComponentModel.DataAnnotations;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Api.Services.Scheduling;
using Defra.WasteObligations.Api.Utils.Logging;
using Defra.WasteObligations.AuditEvents.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Dtos = Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public static class AdminObligationCalculationEndpoints
{
    public static void MapAdminObligationCalculationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("obligation-calculations", Handle).ExcludeFromDescription();
    }

    private static async Task<IResult> Handle(
        [FromQuery] [Range(Dtos.ObligationYear.Minimum, Dtos.ObligationYear.Maximum)] int obligationYear,
        [FromQuery] Guid? submitterId,
        [FromServices] IObligationCalculationInstructionService instructions,
        [FromServices] IOptionsMonitor<ScheduledJobProcessingOptions> processingOptions,
        [FromServices] TraceIdReader traceIdReader,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken
    )
    {
        if (!processingOptions.Get("ObligationCalculation").Enabled)
            return Results.Conflict();

        using var correlation = CorrelationContext.Begin(
            loggerFactory.CreateLogger(typeof(AdminObligationCalculationEndpoints)),
            traceIdReader.Read()
        );
        var instruction = await instructions.Create(obligationYear, submitterId, cancellationToken);

        return new EntityResult<Defra.WasteObligations.Api.Data.Entities.ObligationCalculationInstruction>(instruction);
    }
}
