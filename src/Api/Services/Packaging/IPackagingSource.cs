namespace Defra.WasteObligations.Api.Services.Packaging;

public interface IPackagingSource
{
    Task<PackagingResponse> GetFullYear(int packagingYear, CancellationToken cancellationToken);
}
