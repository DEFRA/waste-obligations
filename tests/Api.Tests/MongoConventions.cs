using System.Runtime.CompilerServices;
using Defra.WasteObligations.Api.Data;

namespace Defra.WasteObligations.Api.Tests;

internal static class MongoConventions
{
    [ModuleInitializer]
    internal static void Initialize() => ServiceCollectionExtensions.RegisterConventions();
}
