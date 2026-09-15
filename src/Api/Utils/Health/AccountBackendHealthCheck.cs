using System.Diagnostics.CodeAnalysis;
using Defra.WasteObligations.Api.Services.AccountBackend;

namespace Defra.WasteObligations.Api.Utils.Health;

[ExcludeFromCodeCoverage]
public class AccountBackendHealthCheck(IServiceProvider serviceProvider)
    : OAuth2DownstreamHealthCheck<AccountBackendOptions>(
        serviceProvider,
        AccountBackendOptions.SectionName,
        "admin/health",
        static (options, httpClient) => options.Configure(httpClient)
    );
