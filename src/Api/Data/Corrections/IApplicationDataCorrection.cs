namespace Defra.WasteObligations.Api.Data.Corrections;

public interface IApplicationDataCorrection
{
    string Id { get; }

    Task Apply(CancellationToken cancellationToken);

    Task Revert(CancellationToken cancellationToken);
}
