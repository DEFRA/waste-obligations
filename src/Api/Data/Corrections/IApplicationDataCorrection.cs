namespace Defra.WasteObligations.Api.Data.Corrections;

public interface IApplicationDataCorrection
{
    int Version { get; }
    string Id { get; }
    Task Apply(CancellationToken cancellationToken);
}
