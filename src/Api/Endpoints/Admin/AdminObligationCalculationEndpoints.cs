using System.ComponentModel.DataAnnotations;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Microsoft.AspNetCore.Mvc;
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
        CancellationToken cancellationToken
    )
    {
        var instruction = await instructions.Create(obligationYear, submitterId, cancellationToken);

        return new EntityResult<Defra.WasteObligations.Api.Data.Entities.ObligationCalculationInstruction>(instruction);
    }
}
