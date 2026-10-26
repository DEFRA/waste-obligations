using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Utils.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.Data.Corrections;

public class ComplianceDeclarationCoverageCorrection(
    IDbContext dbContext,
    IComplianceDeclarationService declarations,
    IOptions<ApplicationDataCorrectionOptions> options
) : IApplicationDataCorrection
{
    public int Version => 1;
    public string Id => "compliance-declaration-individual-material-coverage";

    public async Task Apply(CancellationToken cancellationToken)
    {
        var lastId = ObjectId.Empty;
        while (true)
        {
            var batch = await dbContext
                .ComplianceDeclarations.Find(
                    Builders<ComplianceDeclaration>.Filter.Gt(x => x.Id, lastId)
                        & Builders<ComplianceDeclaration>.Filter.Eq(
                            x => x.SchemaVersion,
                            ComplianceDeclaration.SchemaVersionValue
                        )
                )
                .SortBy(x => x.Id)
                .Limit(options.Value.BatchSize)
                .ToListAsync(cancellationToken);
            if (batch.Count == 0)
            {
                return;
            }

            foreach (var current in batch)
            {
                try
                {
                    var percentage = ObligationCoveragePercentageCalculator.CalculateFromObligations(
                        current.Obligations
                    );
                    if (current.ObligationCoveragePercentage != percentage)
                    {
                        await declarations.Update(
                            current,
                            current with
                            {
                                ObligationCoveragePercentage = percentage,
                            },
                            cancellationToken,
                            actor: AuditActors.Service
                        );
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    throw new InvalidOperationException(
                        $"Application data correction '{Id}' failed for compliance declaration '{current.Id}'.",
                        exception
                    );
                }
                lastId = current.Id;
            }
        }
    }
}
