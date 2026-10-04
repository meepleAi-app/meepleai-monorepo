using Microsoft.Extensions.Configuration;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Sorgente di configurazione che permette di cambiare <c>ConnectionStrings:DefaultConnection</c>
/// su un host già costruito. Issue #4050.
/// </summary>
/// <remarks>
/// <para>
/// 🔴 <b>Perché funziona, verificato e non assunto.</b>
/// <c>IntegrationWebApplicationFactory</c> registra il DbContext con l'overload che riceve il
/// service provider:
/// </para>
/// <code>
/// services.AddDbContext&lt;MeepleAiDbContext&gt;((serviceProvider, options) =&gt;
/// {
///     var configuration = serviceProvider.GetRequiredService&lt;IConfiguration&gt;();
///     var connStr = configuration.GetConnectionString("DefaultConnection") ...
///     options.UseNpgsql(connStr, o =&gt; o.UseVector());
/// });
/// </code>
/// <para>
/// Quell'overload rende <c>DbContextOptions</c> <b>scoped</b>, quindi il lambda rigira a ogni scope
/// e <b>rilegge</b> la configurazione. La connessione non è quindi congelata nell'host al momento
/// della costruzione: cambiarla qui la cambia per lo scope successivo. È ciò che rende possibile
/// pagare l'host una volta sola e dare a ogni test un database suo.
/// </para>
/// <para>
/// La sorgente va aggiunta <b>per ultima</b> al <c>IConfigurationBuilder</c>: l'ultimo provider
/// vince, e deve sovrascrivere la stringa di connessione del dizionario in memoria che la factory
/// compone.
/// </para>
/// <para>
/// 🔴 <b>Limite dichiarato.</b> Questo non rende l'host riusabile per QUALUNQUE cambio di
/// configurazione: solo per le chiavi che il consumatore rilegge a ogni scope. Una chiave letta una
/// volta all'avvio (per esempio in un <c>IOptions</c> singleton, o in un
/// <c>IStartupFilter</c>) resta al valore del momento della costruzione, e cambiarla qui non ha
/// effetto. Vale per la connessione perché il DbContext la rilegge; non assumerlo per altro senza
/// verificare il consumatore.
/// </para>
/// </remarks>
internal sealed class MutableConnectionStringSource : IConfigurationSource
{
    private readonly MutableConnectionStringProvider _provider = new();

    /// <summary>Il provider, per poter cambiare il valore dopo la costruzione dell'host.</summary>
    public MutableConnectionStringProvider Provider => _provider;

    public IConfigurationProvider Build(IConfigurationBuilder builder) => _provider;
}

/// <summary>
/// Il provider di <see cref="MutableConnectionStringSource"/>.
/// </summary>
internal sealed class MutableConnectionStringProvider : ConfigurationProvider
{
    internal const string Key = "ConnectionStrings:DefaultConnection";

    /// <summary>
    /// Punta la configurazione a un altro database. Da chiamare PRIMA di creare lo scope che userà
    /// il DbContext.
    /// </summary>
    public void SetConnectionString(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        Data[Key] = connectionString;

        // Notifica i change token. Non serve a `GetConnectionString`, che legge i provider al
        // momento della chiamata, ma serve a chiunque si sia registrato su `IOptionsMonitor` o
        // `IConfiguration.GetReloadToken()`: senza, resterebbe sul valore precedente senza dirlo.
        OnReload();
    }

    /// <summary>Il valore corrente, o <c>null</c> se non è stato ancora impostato.</summary>
    public string? Current => Data.TryGetValue(Key, out var v) ? v : null;
}
