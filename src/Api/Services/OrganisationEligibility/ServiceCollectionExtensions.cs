namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrganisationEligibility(this IServiceCollection services)
    {
        services
            .AddOptions<OrganisationEligibilityOptions>()
            .BindConfiguration(OrganisationEligibilityOptions.SectionName);
        services.AddTransient<OrganisationReferenceCacheService>();
        services.AddTransient<IOrganisationEligibilityRefreshService, OrganisationEligibilityRefreshService>();

        return services;
    }
}
