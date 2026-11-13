using System.Diagnostics;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.AuditEvents.Logging;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Services.ObligationCalculations;

public class ObligationCalculationInstructionService(IMongoDatabase database, TimeProvider timeProvider)
    : IObligationCalculationInstructionService
{
    private const int DuplicateKeyErrorCode = 11000;
    private const int MaximumCreationAttempts = 5;

    private readonly IMongoCollection<ObligationCalculationInstruction> _instructions =
        database.GetCollection<ObligationCalculationInstruction>(ObligationCalculationInstruction.CollectionName);

    public async Task<ObligationCalculationInstruction> Create(
        IClientSessionHandle session,
        int obligationYear,
        Guid? submitterId,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(session);

        var instruction = new ObligationCalculationInstruction
        {
            ObligationYear = obligationYear,
            SubmitterId = submitterId,
            CorrelationId = CorrelationContext.Current ?? ActivityTraceId.CreateRandom().ToString(),
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
            .SetOnInsert(x => x.CorrelationId, instruction.CorrelationId)
            .SetOnInsert(x => x.Status, instruction.Status)
            .SetOnInsert(x => x.FailureCount, instruction.FailureCount)
            .SetOnInsert(x => x.CreatedAt, instruction.CreatedAt);

        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                var options = new FindOneAndUpdateOptions<ObligationCalculationInstruction>
                {
                    IsUpsert = true,
                    ReturnDocument = ReturnDocument.After,
                };

                return await _instructions.FindOneAndUpdateAsync(session, filter, update, options, cancellationToken);
            }
            catch (MongoCommandException exception)
                when (!session.IsInTransaction
                    && exception.Code == DuplicateKeyErrorCode
                    && attempt < MaximumCreationAttempts
                )
            {
                // Another host inserted first. Retry to return its active instruction.
            }
        }
    }
}
