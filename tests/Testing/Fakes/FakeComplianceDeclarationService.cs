using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;

namespace Defra.WasteObligations.Testing.Fakes;

public class FakeComplianceDeclarationService : IComplianceDeclarationService
{
    public Func<Guid> CreateNewId = Guid.NewGuid;

    public Task<ComplianceDeclaration> Create(
        ComplianceDeclaration complianceDeclaration,
        CancellationToken cancellationToken
    )
    {
        var utcNow = DateTime.UtcNow;

        return Task.FromResult(
            complianceDeclaration with
            {
                Id = CreateNewId(),
                Version = 1,
                Created = utcNow,
                Updated = utcNow,
            }
        );
    }

    public Task<ComplianceDeclaration?> Read(Guid id, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
