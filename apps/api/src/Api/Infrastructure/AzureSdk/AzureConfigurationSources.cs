using System.Globalization;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Api.Infrastructure.AzureSdk;

/// <summary>
/// Sorgenti <see cref="IConfiguration"/> Azure (spec azure-local §4.2). Girano PRIMA del container DI, quindi non
/// passano dal client <c>AzureSdk</c> pinnato: gli endpoint vengono da configurazione statica (mai da input
/// utente), la stessa condizione con cui il gate di egress esenta OpenRouter o Resend. Con le variabili assenti o
/// vuote non viene aggiunta alcuna sorgente. Le chiavi lette da qui VINCONO sulle variabili d'ambiente
/// (aggiunte dopo); <c>SecretLoader</c> aggiunge solo le chiavi ancora assenti.
/// Con il default (<c>optional: false</c>) un endpoint errato o irraggiungibile blocca l'avvio per il timeout
/// predefinito del provider e poi fallisce rumorosamente su stderr; <c>AZURE_APPCONFIG_STARTUP_TIMEOUT_SECONDS</c>
/// (intero positivo) lo accorcia, valori non validi sono ignorati.
/// </summary>
internal static class AzureConfigurationSources
{
    public const string AppConfigConnectionStringKey = "AZURE_APPCONFIG_CONNECTION_STRING";
    public const string AppConfigEndpointKey = "AZURE_APPCONFIG_ENDPOINT";
    public const string KeyVaultUriKey = "AZURE_KEYVAULT_URI";
    public const string KeyVaultAnonymousKey = "AZURE_KEYVAULT_ANONYMOUS";
    public const string AppConfigStartupTimeoutKey = "AZURE_APPCONFIG_STARTUP_TIMEOUT_SECONDS";
    private const string AnonymousToken = "anonymous";

    public static IConfigurationBuilder AddAzureConfigurationSources(this IConfigurationBuilder builder, IConfiguration settings)
    {
        var appConfigConnectionString = settings[AppConfigConnectionStringKey];
        var appConfigEndpoint = settings[AppConfigEndpointKey];
        var startupTimeoutSeconds = ParseStartupTimeout(settings[AppConfigStartupTimeoutKey]);
        if (!string.IsNullOrWhiteSpace(appConfigConnectionString))
        {
            // HMAC (emulatore o store con chiavi di accesso): funziona anche su http.
            builder.AddAzureAppConfiguration(options => options
                .Connect(appConfigConnectionString.Trim())
                .ApplyStartupTimeout(startupTimeoutSeconds)
                .Select(KeyFilter.Any, LabelFilter.Null));
        }
        else if (!string.IsNullOrWhiteSpace(appConfigEndpoint))
        {
            // Azure vero: identità Entra, endpoint https.
            var endpoint = ParseAbsoluteUri(appConfigEndpoint, AppConfigEndpointKey);
            builder.AddAzureAppConfiguration(options => options
                .Connect(endpoint, new DefaultAzureCredential())
                .ApplyStartupTimeout(startupTimeoutSeconds)
                .Select(KeyFilter.Any, LabelFilter.Null));
        }

        var keyVaultUri = settings[KeyVaultUriKey];
        if (!string.IsNullOrWhiteSpace(keyVaultUri))
        {
            var vault = ParseAbsoluteUri(keyVaultUri, KeyVaultUriKey);
            var clientOptions = new SecretClientOptions { DisableChallengeResourceVerification = true };
            if (AzureSdkHttpClients.AllowInsecureTls(settings))
            {
                // Solo profilo azure-local + opt-in esplicito: certificato self-signed del Key Vault emulator.
                var handler = new SocketsHttpHandler();
#pragma warning disable S4830, CA5359 // Deliberato: solo profilo azure-local + opt-in (AllowInsecureTls)
                handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
#pragma warning restore S4830, CA5359
                clientOptions.Transport = new HttpClientTransport(new HttpClient(handler));
            }

            var anonymous = string.Equals(settings[KeyVaultAnonymousKey], "true", StringComparison.OrdinalIgnoreCase);
            var client = new SecretClient(vault, anonymous ? new StaticTokenCredential(AnonymousToken) : new DefaultAzureCredential(), clientOptions);
            builder.AddAzureKeyVault(client, new EnvironmentStyleKeyVaultSecretManager());
        }

        return builder;
    }

    private static int? ParseStartupTimeout(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds > 0 ? seconds : null;

    private static AzureAppConfigurationOptions ApplyStartupTimeout(this AzureAppConfigurationOptions options, int? seconds) =>
        seconds is { } s ? options.ConfigureStartupOptions(startup => startup.Timeout = TimeSpan.FromSeconds(s)) : options;

    private static Uri ParseAbsoluteUri(string value, string key)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"{key} must be an absolute URL, got '{value}'");
        }

        return uri;
    }
}
