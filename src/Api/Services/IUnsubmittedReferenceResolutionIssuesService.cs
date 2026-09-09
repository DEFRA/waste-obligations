using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Services;

public interface IUnsubmittedReferenceResolutionIssuesService
{
    Task<UnsubmittedReferenceResolutionIssues> Get(CancellationToken cancellationToken);
}
