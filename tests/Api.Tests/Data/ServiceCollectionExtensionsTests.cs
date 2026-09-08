using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
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
                    ["MongoMigrations:LeaseDurationSeconds"] = "10",
                    ["MongoMigrations:LeaseRenewalIntervalSeconds"] = "6",
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
}
