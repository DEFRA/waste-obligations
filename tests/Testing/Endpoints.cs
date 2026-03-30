namespace Defra.WasteObligations.Testing;

public static class Endpoints
{
    public static class Health
    {
        public static string Ready() => "health";
    }

    public static class OpenApi
    {
        public const string V1 = "openapi/v1.json";
    }

    public static class Example
    {
        private static string Root => "example";

        public static string Get(Guid id, bool? badRequest = null) =>
            $"{Root}/{id}{(badRequest is not null ? $"?badRequest={badRequest.Value}" : "")}";

        public static string Put(Guid id) => Get(id);
    }
}
