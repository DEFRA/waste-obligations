namespace Defra.WasteObligations.Api.Services.WastePackaging;

public interface IWastePackagingService
{
    Task<PackagingResponse> Read(int packagingYear, CancellationToken cancellationToken, string? traceId = null);
}
