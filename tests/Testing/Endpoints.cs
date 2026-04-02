using System.Diagnostics.CodeAnalysis;

// ReSharper disable MemberHidesStaticFromOuterClass

namespace Defra.WasteObligations.Testing;

[SuppressMessage(
    "Critical Code Smell",
    "S3218:Inner class members should not shadow outer class \"static\" or type members"
)]
public static class Endpoints
{
    public static class Health
    {
        public static string Ready() => "health";
    }

    public static class OpenApi
    {
        public const string V1 = "documentation/openapi/v1.json";
    }

    public static class Organisations
    {
        private static string Root => "organisations";

        public static string Read(Guid id) => $"{Root}/{id}";

        public static class Obligations
        {
            private static string Root = "obligations";

            public static string Read(Guid id, EndpointQuery? query = null) =>
                $"{Organisations.Read(id)}/{Root}{query}";
        }
    }
}
