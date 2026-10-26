namespace Defra.WasteObligations.Api.Data.Corrections;

public interface IApplicationDataCorrectionRunner
{
    Task Run(CancellationToken cancellationToken);
}
