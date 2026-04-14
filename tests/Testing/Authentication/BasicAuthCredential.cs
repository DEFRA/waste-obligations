using System.Text;

namespace Defra.WasteObligations.Testing.Authentication;

public static class BasicAuthCredential
{
    /// <summary>
    /// Uses client_id and client_secret.
    /// </summary>
    public static string Default => Create("client_id", "client_secret");

    private static string Create(string clientId, string clientSecret) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
}
