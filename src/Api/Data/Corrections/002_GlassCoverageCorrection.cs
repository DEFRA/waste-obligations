using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Data.Corrections;

public class GlassCoverageCorrection(
    IDbContext dbContext,
    IComplianceDeclarationService declarations,
    IOptions<ApplicationDataCorrectionOptions> options,
    ILogger<GlassCoverageCorrection> logger
) : IApplicationDataCorrection
{
    public int Version => 2;
    public string Id => "compliance-declaration-glass-coverage";

    public Task Apply(CancellationToken cancellationToken) =>
        ComplianceDeclarationCoverageCorrection.ApplyCoverageCorrection(
            dbContext,
            declarations,
            options,
            logger,
            Id,
            ObligationCoveragePercentageCalculator.CalculateFromObligations,
            cancellationToken
        );
}
