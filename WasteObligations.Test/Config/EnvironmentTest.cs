using Microsoft.AspNetCore.Builder;

namespace WasteObligations.Test.Config;

public class EnvironmentTest
{
    [Fact]
    public void IsNotDevModeByDefault()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        var isDev = WasteObligations.Config.Environment.IsDevMode(builder);
        Assert.False(isDev);
    }
}