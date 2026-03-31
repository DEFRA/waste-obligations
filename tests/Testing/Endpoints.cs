using System.Diagnostics.CodeAnalysis;

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

    public static class Example
    {
        private static string Root => "example";

        public static string Get(Guid id, bool? badRequest = null) =>
            $"{Root}/{id}{(badRequest is not null ? $"?badRequest={badRequest.Value}" : "")}";

        public static string Put(Guid id) => Get(id);
    }

    public static class Organisations
    {
        private static string Root => "organisations";

        public static string Get(Guid id) => $"{Root}/{id}";

        public static class Obligations
        {
            private static string Root = "obligations";

            public static string Get(Guid id, EndpointQuery? query = null) => $"{Organisations.Get(id)}/{Root}{query}";
        }
    }
}
