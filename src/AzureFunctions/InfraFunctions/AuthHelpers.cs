using Microsoft.Azure.KeyVault;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Threading.Tasks;

namespace InfraFunctions
{
    internal static class AuthHelpers
    {
        private const string KeyVaultUrl = "https://roslyninfra.vault.azure.net:443";

        public static async Task<string> GetSecret(string secretName)
        {
            var kv = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(GetAccessToken));
            var secret = await kv.GetSecretAsync(KeyVaultUrl, secretName);
            return secret.Value;
        }

        private static async Task<string> GetAccessToken(string authority, string resource, string scope)
        {
            var ctx = new AuthenticationContext(authority);
            var clientId = Environment.GetVariable("ClientId");
            var clientSecret = Environment.GetVariable("ClientSecret");
            var clientCred = new ClientCredential(clientId, clientSecret);

            var authResult = await ctx.AcquireTokenAsync(resource, clientCred);
            return authResult.AccessToken;
        }
    }
}
