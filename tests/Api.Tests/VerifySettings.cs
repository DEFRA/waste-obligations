using System.Runtime.CompilerServices;

namespace Defra.WasteObligations.Api.Tests;

public static class VerifySettings
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifierSettings.UseStrictJson();
        VerifierSettings.DontIgnoreEmptyCollections();
        VerifierSettings.ScrubMember("traceId");
    }
}
