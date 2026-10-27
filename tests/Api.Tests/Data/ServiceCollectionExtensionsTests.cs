using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Corrections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.Tests.Data;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDbContext_WhenLeaseRenewalIntervalIsMoreThanHalfTheLeaseDuration_ShouldFailValidation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["MongoMigrations:LeaseDurationSeconds"] = "30",
                    ["MongoMigrations:LeaseRenewalIntervalSeconds"] = "16",
                    ["MongoMigrations:LeaseOperationTimeoutSeconds"] = "1",
                }
            )
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext(configuration, validateConfigOnly: true);
        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<MongoMigrationOptions>>();

        var action = () => _ = options.Value;

        action.Should().Throw<OptionsValidationException>();
    }

    [Theory]
    [InlineData(MongoMigrationOptions.SectionName)]
    [InlineData(ApplicationDataCorrectionOptions.SectionName)]
    public void AddDbContext_WhenLeaseOperationCanOutlastRenewalSlack_ShouldFailValidation(string section)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{section}:LeaseDurationSeconds"] = "30",
                    [$"{section}:LeaseRenewalIntervalSeconds"] = "15",
                    [$"{section}:LeaseOperationTimeoutSeconds"] = "15",
                }
            )
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext(configuration, validateConfigOnly: true);
        using var provider = services.BuildServiceProvider();
        Action action =
            section == MongoMigrationOptions.SectionName
                ? () => _ = provider.GetRequiredService<IOptions<MongoMigrationOptions>>().Value
                : () => _ = provider.GetRequiredService<IOptions<ApplicationDataCorrectionOptions>>().Value;

        action.Should().Throw<OptionsValidationException>();
    }
}
