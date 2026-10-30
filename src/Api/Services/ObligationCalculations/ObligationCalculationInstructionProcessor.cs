using Defra.WasteObligations.Api.Data.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public class ObligationCalculationInstructionProcessor(
    IMongoDatabase database,
    ILogger<ObligationCalculationInstructionProcessor> logger
) : IObligationCalculationInstructionProcessor
{
    private readonly IMongoCollection<ObligationCalculationInstruction> _instructions =
        database.GetCollection<ObligationCalculationInstruction>(ObligationCalculationInstruction.CollectionName);

    public async Task<bool> ProcessNext(CancellationToken cancellationToken)
    {
        var instruction = await _instructions.FindOneAndUpdateAsync(
            x => x.Status == ObligationCalculationInstructionStatus.Pending,
            Builders<ObligationCalculationInstruction>.Update.Set(
                x => x.Status,
                ObligationCalculationInstructionStatus.Completed
            ),
            new FindOneAndUpdateOptions<ObligationCalculationInstruction>
            {
                Sort = Builders<ObligationCalculationInstruction>.Sort.Ascending(x => x.CreatedAt).Ascending(x => x.Id),
                ReturnDocument = ReturnDocument.After,
            },
            cancellationToken
        );

        if (instruction is null)
            return false;

        logger.LogInformation(
            "Obligation calculation instruction {InstructionId} completed by placeholder processor for obligation year {ObligationYear} and submitter {SubmitterId}; no calculation performed",
            instruction.Id,
            instruction.ObligationYear,
            instruction.SubmitterId
        );

        return true;
    }
}
