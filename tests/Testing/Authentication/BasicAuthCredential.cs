using System.Text;

namespace Defra.WasteObligations.Testing.Authentication;

public static class BasicAuthCredential
{
    public static string Default => Create("client_id", "client_secret");

    public static string ForClient(string clientId) => Create(clientId, "client_secret");

    private static string Create(string clientId, string clientSecret) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
}
