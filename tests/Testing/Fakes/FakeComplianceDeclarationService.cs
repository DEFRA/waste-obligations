using AutoFixture;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using ComplianceDeclaration = Defra.WasteObligations.Api.Data.Entities.ComplianceDeclaration;

namespace Defra.WasteObligations.Testing.Fakes;

public class FakeComplianceDeclarationService : IComplianceDeclarationService
{
    public Func<Guid> CreateNewId = Guid.NewGuid;
    public Func<DateTimeOffset> UtcNow = () => DateTimeOffset.UtcNow;
    public bool Throws = false;

    private static readonly DateTime s_start = new(2026, 4, 26, 14, 0, 0, DateTimeKind.Utc);

    public static readonly Guid ComplianceDeclarationId = new("3bd56644-9b24-4cd1-98f1-cc69f3b16bc1");

    public static readonly Guid NonMatchingOrganisationComplianceDeclarationId = new(
        "14956bb4-2f09-4d90-a63b-7e1a4bbe174e"
    );

    private static readonly Dictionary<Guid, List<ComplianceDeclaration>> s_complianceDeclarations = new()
    {
        {
            FakeWasteOrganisationsService.OrganisationId,
            [
                ComplianceDeclarationFixture
                    .DirectProducer(FakeWasteOrganisationsService.OrganisationId)
                    .With(x => x.Id, ComplianceDeclarationId)
                    .With(x => x.Created, s_start)
                    .With(x => x.Updated, s_start)
                    .Create(),
                ComplianceDeclarationFixture
                    .DirectProducer(FakeWasteOrganisationsService.OrganisationId)
                    .With(x => x.Created, s_start.AddDays(1))
                    .With(x => x.Updated, s_start.AddDays(1))
                    .Create(),
                ComplianceDeclarationFixture
                    .DirectProducer(FakeWasteOrganisationsService.OrganisationId)
                    .With(x => x.ObligationYear, FakeWasteOrganisationsService.Year - 1)
                    .With(x => x.Created, s_start.AddYears(-1))
                    .With(x => x.Updated, s_start.AddYears(-1))
                    .Create(),
                ComplianceDeclarationFixture
                    .DirectProducer()
                    .With(x => x.Id, NonMatchingOrganisationComplianceDeclarationId)
                    .With(x => x.Created, s_start)
                    .With(x => x.Updated, s_start)
                    .Create(),
            ]
        },
    };

    public Task<ComplianceDeclaration> Create(
        ComplianceDeclaration complianceDeclaration,
        CancellationToken cancellationToken
    )
    {
        if (Throws)
            throw new InvalidOperationException("The operation failed");

        var utcNow = UtcNow().UtcDateTime;

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
        return Task.FromResult(s_complianceDeclarations.SelectMany(x => x.Value).FirstOrDefault(x => x.Id == id));
    }

    public Task<IEnumerable<ComplianceDeclaration>> Read(
        Guid organisationId,
        int obligationYear,
        CancellationToken cancellationToken
    )
    {
        if (s_complianceDeclarations.TryGetValue(organisationId, out var complianceDeclarations))
            return Task.FromResult<IEnumerable<ComplianceDeclaration>>(
                complianceDeclarations.Where(x => x.ObligationYear == obligationYear).ToList()
            );

        return Task.FromResult(Enumerable.Empty<ComplianceDeclaration>());
    }
}
