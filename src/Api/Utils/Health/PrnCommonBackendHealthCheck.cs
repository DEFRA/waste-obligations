using System.Diagnostics.CodeAnalysis;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;

namespace Defra.WasteObligations.Api.Utils.Health;

[ExcludeFromCodeCoverage]
public class PrnCommonBackendHealthCheck(IServiceProvider serviceProvider)
    : OAuth2DownstreamHealthCheck<PrnCommonBackendOptions>(
        serviceProvider,
        PrnCommonBackendOptions.SectionName,
        "admin/health",
        static (options, httpClient) => options.Configure(httpClient)
    );
