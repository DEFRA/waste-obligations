using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public class ObligationCalculationInstructionService(
    IMongoDatabase database,
    TimeProvider timeProvider,
    MongoMigrationCompletion migrations
) : IObligationCalculationInstructionService
{
    private const int DuplicateKeyErrorCode = 11000;

    private readonly IMongoCollection<ObligationCalculationInstruction> _instructions =
        database.GetCollection<ObligationCalculationInstruction>(ObligationCalculationInstruction.CollectionName);

    public async Task<ObligationCalculationInstruction> Create(
        int obligationYear,
        Guid? submitterId,
        CancellationToken cancellationToken
    )
    {
        await migrations.Wait(cancellationToken);
        var instruction = new ObligationCalculationInstruction
        {
            ObligationYear = obligationYear,
            SubmitterId = submitterId,
            CreatedAt = timeProvider.GetUtcNowWithoutMicroseconds(),
        };
        var filter = Builders<ObligationCalculationInstruction>.Filter.And(
            Builders<ObligationCalculationInstruction>.Filter.Eq(x => x.ObligationYear, obligationYear),
            Builders<ObligationCalculationInstruction>.Filter.Eq(x => x.SubmitterId, submitterId),
            Builders<ObligationCalculationInstruction>.Filter.In(
                x => x.Status,
                [ObligationCalculationInstructionStatus.Pending, ObligationCalculationInstructionStatus.Running]
            )
        );
        var update = Builders<ObligationCalculationInstruction>
            .Update.SetOnInsert(x => x.Id, instruction.Id)
            .SetOnInsert(x => x.ObligationYear, instruction.ObligationYear)
            .SetOnInsert(x => x.SubmitterId, instruction.SubmitterId)
            .SetOnInsert(x => x.Status, instruction.Status)
            .SetOnInsert(x => x.CreatedAt, instruction.CreatedAt);

        while (true)
        {
            try
            {
                return await _instructions.FindOneAndUpdateAsync(
                    filter,
                    update,
                    new FindOneAndUpdateOptions<ObligationCalculationInstruction>
                    {
                        IsUpsert = true,
                        ReturnDocument = ReturnDocument.After,
                    },
                    cancellationToken
                );
            }
            catch (Exception exception)
                when (exception is MongoCommandException { Code: DuplicateKeyErrorCode }
                    || exception is MongoWriteException { WriteError.Code: DuplicateKeyErrorCode }
                )
            {
                // Another host inserted first. Retry to return its active instruction.
            }
        }
    }
}
