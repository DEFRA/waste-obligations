using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

internal readonly record struct OrganisationReferenceCacheKey(Guid OrganisationId, RegistrationType RegistrationType);
