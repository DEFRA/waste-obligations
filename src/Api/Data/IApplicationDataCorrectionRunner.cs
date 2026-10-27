namespace Defra.WasteObligations.Api.Data;

public interface IApplicationDataCorrectionRunner
{
    Task Run(CancellationToken cancellationToken);
}
