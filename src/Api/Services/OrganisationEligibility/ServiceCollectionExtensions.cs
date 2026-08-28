namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrganisationEligibility(
        this IServiceCollection services,
        bool addRefreshWorker = true
    )
    {
        services
            .AddOptions<OrganisationEligibilityOptions>()
            .BindConfiguration(OrganisationEligibilityOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                options => options.RefreshLeaseRenewalIntervalSeconds < options.RefreshLeaseDurationSeconds,
                "Refresh lease renewal interval must be less than the refresh lease duration"
            )
            .ValidateOnStart();
        services.AddTransient<OrganisationReferenceResolver>();
        services.AddTransient<IOrganisationEligibilityRefreshService, OrganisationEligibilityRefreshService>();
        services.AddTransient<
            IOrganisationEligibilityRefreshLeaseService,
            OrganisationEligibilityRefreshLeaseService
        >();

        if (addRefreshWorker)
        {
            services.AddHostedService<OrganisationEligibilityRefreshWorker>();
        }

        return services;
    }
}
