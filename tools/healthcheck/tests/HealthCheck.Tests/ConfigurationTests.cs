using AwesomeAssertions;
using Xunit;

[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace Defra.WasteObligations.HealthCheck.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void DotEnvOverridesJsonUsingStandardConfigurationKeys()
    {
        using var files = new ConfigurationFiles();
        File.WriteAllText(
            files.EnvPath,
            """
            TimeoutSeconds=2
            Services__waste-obligations-frontend__ApiKey__Value='key$with#characters'
            Services__waste-obligations__OAuth__ClientId=client-id
            Services__waste-obligations__OAuth__ClientSecret='client-secret'
            """
        );
        var configuration = HealthCheckConfiguration.Load(files.Path);
        configuration.TimeoutSeconds.Should().Be(2);
        configuration.Services["waste-obligations-frontend"].ApiKey.Should().NotBeNull();
        configuration.Services["waste-obligations-frontend"].ApiKey!.Value.Should().Be("key$with#characters");
        configuration.Services["waste-obligations"].OAuth.Should().NotBeNull();
        configuration.Services["waste-obligations"].OAuth!.ClientId.Should().Be("client-id");
        configuration.Services["waste-obligations"].OAuth!.ClientSecret.Should().Be("client-secret");
    }

    [Fact]
    public void ProcessEnvironmentOverridesDotEnvAndJson()
    {
        const string key = "Services__waste-obligations-frontend__ApiKey__Value";
        var previous = Environment.GetEnvironmentVariable(key);
        using var files = new ConfigurationFiles();
        try
        {
            File.WriteAllText(files.EnvPath, $"{key}=file-value\n");
            Environment.SetEnvironmentVariable(key, "process-value");
            var configuration = HealthCheckConfiguration.Load(files.Path);
            configuration.Services["waste-obligations-frontend"].ApiKey.Should().NotBeNull();
            configuration.Services["waste-obligations-frontend"].ApiKey!.Value.Should().Be("process-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
        }
    }

    [Fact]
    public void LoadingDifferentFilesDoesNotLeakCredentialsIntoProcessEnvironment()
    {
        const string key = "Services__waste-obligations-frontend__ApiKey__Value";
        var previous = Environment.GetEnvironmentVariable(key);
        using var first = new ConfigurationFiles();
        using var second = new ConfigurationFiles();
        try
        {
            Environment.SetEnvironmentVariable(key, null);
            File.WriteAllText(first.EnvPath, $"{key}=first-file\n");
            HealthCheckConfiguration
                .Load(first.Path)
                .Services["waste-obligations-frontend"]
                .ApiKey!.Value.Should()
                .Be("first-file");
            HealthCheckConfiguration
                .Load(second.Path)
                .Services["waste-obligations-frontend"]
                .ApiKey!.Value.Should()
                .Be("json-key");
            Environment.GetEnvironmentVariable(key).Should().BeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
        }
    }

    [Fact]
    public async Task CommandLoadsEnvBesideSelectedConfiguration()
    {
        using var files = new ConfigurationFiles();
        await File.WriteAllTextAsync(
            files.Path,
            """
            {"Services":{"waste-obligations-frontend":{"Url":"https://frontend.test/health/all","ApiKey":{"Value":""}}}}
            """,
            TestContext.Current.CancellationToken
        );
        await File.WriteAllTextAsync(
            files.EnvPath,
            "Services__waste-obligations-frontend__ApiKey__Value=test-key\n",
            TestContext.Current.CancellationToken
        );
        using var handler = new HeaderHandler();
        using var client = new HttpClient(handler);
        using var output = new StringWriter();
        using var error = new StringWriter();
        (await HealthCheckCommand.Run([files.Path], client, output, error, CancellationToken.None)).Should().Be(0);
        output.ToString().Should().Contain("PASS waste-obligations-frontend").And.NotContain("test-key");
        error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task MalformedDotEnvReturnsConfigurationErrorWithoutExposingSecrets()
    {
        using var files = new ConfigurationFiles();
        await File.WriteAllTextAsync(
            files.EnvPath,
            "Services__waste-obligations-frontend__ApiKey__Value='unterminated-secret",
            TestContext.Current.CancellationToken
        );
        using var handler = new HeaderHandler();
        using var client = new HttpClient(handler);
        using var output = new StringWriter();
        using var error = new StringWriter();
        (await HealthCheckCommand.Run([files.Path], client, output, error, CancellationToken.None)).Should().Be(2);
        output.ToString().Should().BeEmpty();
        error.ToString().Should().Contain("Invalid configuration").And.NotContain("unterminated-secret");
    }

    private sealed class ConfigurationFiles : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("health-cli-");
        public string Path => System.IO.Path.Combine(_directory.FullName, "appsettings.json");
        public string EnvPath => System.IO.Path.Combine(_directory.FullName, ".env");

        public ConfigurationFiles() =>
            File.WriteAllText(
                Path,
                """
                {
                  "Services":{
                    "waste-obligations-frontend":{"Url":"https://frontend.test/health/all","ApiKey":{"Value":"json-key"}},
                    "waste-obligations":{"Url":"https://backend.test/health/all","OAuth":{
                      "TokenUrl":"https://identity.test/token","ClientId":"json-id","ClientSecret":"json-secret"
                    }}
                  }
                }
                """
            );

        public void Dispose() => _directory.Delete(true);
    }

    private sealed class HeaderHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            request.Headers.GetValues("X-Health-Check-Token").Should().ContainSingle().Which.Should().Be("test-key");

            return Task.FromResult(
                new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"status\":\"Healthy\"}"),
                }
            );
        }
    }
}
