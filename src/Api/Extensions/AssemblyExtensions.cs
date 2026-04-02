using System.Reflection;

namespace Defra.WasteObligations.Api.Extensions;

public static class AssemblyExtensions
{
    /// <summary>
    /// See the Api project file for OpenApi prefixed settings.
    /// </summary>
    /// <param name="assembly"></param>
    /// <returns></returns>
    public static bool IsProjectBuildGeneratingOpenApi(this Assembly? assembly) =>
        assembly?.GetName().Name == "GetDocument.Insider";
}
