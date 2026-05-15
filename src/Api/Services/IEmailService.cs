using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services;

public interface IEmailService
{
    Task SendSubmittedEmail(ComplianceDeclaration complianceDeclaration, CancellationToken cancellationToken);
}
