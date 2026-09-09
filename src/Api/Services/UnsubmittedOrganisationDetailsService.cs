using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using MongoDB.Driver;
using PrnObligation = Defra.WasteObligations.Api.Services.PrnCommonBackend.Obligation;
using WasteOrganisation = Defra.WasteObligations.Api.Services.WasteOrganisations.Organisation;

namespace Defra.WasteObligations.Api.Services;

public class UnsubmittedOrganisationDetailsService(
    IDbContext dbContext,
    IWasteOrganisationsService wasteOrganisationsService,
    IPrnCommonBackendService prnCommonBackendService
) : IUnsubmittedOrganisationDetailsService
{
    public async Task<UnsubmittedOrganisationDetails?> Get(
        Guid organisationId,
        bool includeLiveData,
        CancellationToken cancellationToken
    )
    {
        var snapshotTask = dbContext
            .OrganisationEligibilitySnapshots.Find(x =>
                x.Id == Data.Entities.OrganisationEligibilitySnapshot.SnapshotId
            )
            .SingleOrDefaultAsync(cancellationToken);
        var eligibilityTask = dbContext
            .OrganisationComplianceDeclarationEligibilities.Find(x => x.OrganisationId == organisationId)
            .SortByDescending(x => x.RefreshedAt)
            .ThenByDescending(x => x.ObligationYear)
            .ThenBy(x => x.RegistrationType)
            .ThenBy(x => x.Generation)
            .ToListAsync(cancellationToken);
        var summariesTask = dbContext
            .OrganisationObligationSummaries.Find(x => x.OrganisationId == organisationId)
            .SortByDescending(x => x.ObligationYear)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(snapshotTask, eligibilityTask, summariesTask);

        var eligibility = await eligibilityTask;
        var summaries = await summariesTask;
        var liveData = includeLiveData ? await ReadLiveData(organisationId, cancellationToken) : null;
        if (eligibility.Count == 0 && summaries.Count == 0 && liveData?.Organisation is null)
            return null;

        var snapshot = await snapshotTask;

        return new UnsubmittedOrganisationDetails
        {
            OrganisationId = organisationId,
            EligibilitySnapshot = snapshot,
            Eligibility = [.. eligibility],
            ObligationSummaries = [.. summaries],
            LiveData = liveData,
        };
    }

    private async Task<UnsubmittedOrganisationLiveData> ReadLiveData(
        Guid organisationId,
        CancellationToken cancellationToken
    )
    {
        var organisation = await wasteOrganisationsService.Read(organisationId, cancellationToken);
        if (organisation is null)
            return new UnsubmittedOrganisationLiveData { Organisation = null, ObligationResults = [] };

        var obligationResults = new List<UnsubmittedOrganisationLiveObligationResult>();
        foreach (var obligationYear in organisation.Registrations.Select(x => x.RegistrationYear).Distinct().Order())
        {
            var obligations = (
                await prnCommonBackendService.ReadObligations(organisationId, obligationYear, cancellationToken)
            ).ToArray();
            var metrics = OrganisationObligationSummaryMapper.Map(organisationId, obligationYear, obligations);
            obligationResults.Add(
                new UnsubmittedOrganisationLiveObligationResult
                {
                    ObligationYear = obligationYear,
                    RawObligations = [.. obligations.Select(ToDto)],
                    UnsubmittedCalculation = ToDto(metrics),
                }
            );
        }

        return new UnsubmittedOrganisationLiveData
        {
            Organisation = ToDto(organisation),
            ObligationResults = [.. obligationResults],
        };
    }

    private static UnsubmittedOrganisationLiveOrganisation ToDto(WasteOrganisation organisation) =>
        new()
        {
            Id = organisation.Id,
            Name = organisation.Name,
            TradingName = organisation.TradingName,
            Country = organisation.BusinessCountry,
            CompaniesHouseNumber = organisation.CompaniesHouseNumber,
            Address = new Dtos.Address
            {
                AddressLine1 = organisation.Address.AddressLine1,
                AddressLine2 = organisation.Address.AddressLine2,
                Town = organisation.Address.Town,
                County = organisation.Address.County,
                Postcode = organisation.Address.Postcode,
                Country = organisation.Address.Country,
            },
            Registrations =
            [
                .. organisation.Registrations.Select(x => new UnsubmittedOrganisationLiveRegistration
                {
                    Status = x.Status,
                    Type = x.Type,
                    RegistrationYear = x.RegistrationYear,
                    Created = x.Created,
                    Updated = x.Updated,
                }),
            ],
        };

    private static UnsubmittedOrganisationRawObligation ToDto(PrnObligation obligation) =>
        new()
        {
            OrganisationId = obligation.OrganisationId,
            MaterialName = obligation.MaterialName,
            Tonnage = obligation.Tonnage,
            MaterialTarget = obligation.MaterialTarget,
            ObligationToMeet = obligation.ObligationToMeet,
            TonnageAwaitingAcceptance = obligation.TonnageAwaitingAcceptance,
            TonnageAccepted = obligation.TonnageAccepted,
            TonnageOutstanding = obligation.TonnageOutstanding,
            Status = obligation.Status,
        };

    private static UnsubmittedOrganisationLiveObligationCalculation ToDto(OrganisationObligationMetrics metrics) =>
        new()
        {
            ObligationCount = metrics.ObligationCount,
            TotalAcceptedTonnage = metrics.TotalAcceptedTonnage,
            TotalObligatedTonnage = metrics.TotalObligatedTonnage,
            RecyclingObligationsMet = metrics.RecyclingObligationsMet,
            ObligationCoveragePercentage = metrics.ObligationCoveragePercentage,
            SourceFingerprint = metrics.SourceFingerprint,
        };
}
