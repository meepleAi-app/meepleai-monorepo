namespace Api.Services.Pdf;

/// <summary>
/// Parser minimale della connection string di Azure Storage: serve SOLO per ricavare account e chiave con cui
/// firmare le SAS senza passare da <c>BlobUriBuilder</c>, che con un host come <c>azurite</c> (né IP né
/// localhost) scambia l'account nel path per il container e produce una firma invalida.
/// </summary>
internal static class AzureStorageConnectionString
{
    public const string DevelopmentStorageAccount = "devstoreaccount1";
    public const string DevelopmentStorageKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    public static bool TryParse(string? connectionString, out string accountName, out string accountKey)
    {
        accountName = string.Empty;
        accountKey = string.Empty;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        var pairs = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Split('=', 2))
            .Where(kv => kv.Length == 2)
            .ToDictionary(kv => kv[0], kv => kv[1], StringComparer.OrdinalIgnoreCase);

        if (pairs.TryGetValue("UseDevelopmentStorage", out var dev) && string.Equals(dev, "true", StringComparison.OrdinalIgnoreCase))
        {
            accountName = DevelopmentStorageAccount;
            accountKey = DevelopmentStorageKey;
            return true;
        }

        if (pairs.TryGetValue("AccountName", out var name) && pairs.TryGetValue("AccountKey", out var key)
            && !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(key))
        {
            accountName = name;
            accountKey = key;
            return true;
        }

        return false;
    }
}
