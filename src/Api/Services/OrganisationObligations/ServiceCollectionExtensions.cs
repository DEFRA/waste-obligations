using Microsoft.Extensions.DependencyInjection;

namespace Defra.WasteObligations.Api.Services.OrganisationObligations;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrganisationObligationHydration(
        this IServiceCollection services,
        bool addWorker = true
    )
    {
        services
            .AddOptions<OrganisationObligationHydrationOptions>()
            .BindConfiguration(OrganisationObligationHydrationOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                options =>
                    options.LeaseRenewalIntervalSeconds < options.LeaseDurationSeconds
                    && options.RefreshInterval > TimeSpan.Zero
                    && options.InitialRetryDelay > TimeSpan.Zero
                    && options.MaximumRetryDelay >= options.InitialRetryDelay
                    && options.MaximumSummaryStaleness > TimeSpan.Zero,
                "Organisation obligation hydration interval configuration is invalid"
            )
            .ValidateOnStart();
        services.AddTransient<
            IOrganisationObligationHydrationLeaseService,
            OrganisationObligationHydrationLeaseService
        >();
        services.AddTransient<
            IOrganisationObligationHistoricalBackfillLeaseService,
            OrganisationObligationHistoricalBackfillLeaseService
        >();
        services.AddTransient<
            IOrganisationObligationHistoricalBackfillStore,
            OrganisationObligationHistoricalBackfillStore
        >();
        services.AddSingleton<IOrganisationObligationRequestPacer, OrganisationObligationRequestPacer>();
        services.AddSingleton<
            IOrganisationObligationRequestPacingStateStore,
            OrganisationObligationRequestPacingStateStore
        >();
        services.AddTransient<IOrganisationObligationHydrationService, OrganisationObligationHydrationService>();

        if (addWorker)
        {
            services.AddHostedService<OrganisationObligationHydrationWorker>();
            services.AddHostedService<OrganisationObligationHistoricalBackfillWorker>();
        }

        return services;
    }
}
