using System;
using System.Text.RegularExpressions;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Il vincolo sui nomi dei database generati dalle fixture di test.
/// </summary>
/// <remarks>
/// <para>
/// 🔴 Postgres tronca ogni identificatore a <b>63 byte</b>, in silenzio. Il troncamento non rompe
/// quello che si pensa: rompe una cosa sola, e nel modo peggiore.
/// </para>
/// <para>
/// <c>CREATE DATABASE x TEMPLATE "&lt;64 caratteri&gt;"</c> <b>funziona</b>, perché Postgres tronca
/// anche il riferimento e i due nomi tronchi coincidono. Ma una query che cerca lo stesso database
/// in <c>pg_stat_activity WHERE datname = @db</c> fa un confronto fra <b>stringhe</b>: la stringa
/// da 64 non corrisponde al <c>datname</c> da 63, la query non trova nessuno, e il
/// <c>pg_terminate_backend</c> che ne dipende non termina niente.
/// </para>
/// <para>
/// Misurato in #4050 su un prefisso da 26 caratteri: il template veniva creato come <c>…_tmp</c>
/// invece di <c>…_tmpl</c>, la diagnostica della fixture riportava «(nessuno)» fra i detentori del
/// template, e tutti i tentativi di clone morivano su <c>55006: source database is being accessed
/// by other users</c>. La fixture accusava una coda di chiusura inesistente invece del proprio nome
/// troncato, e si portava dietro ogni test della classe.
/// </para>
/// <para>
/// Da qui la validazione: il nome lo si rifiuta quando viene composto, non lo si scopre tronco
/// quando una query smette di trovarlo.
/// </para>
/// </remarks>
internal static class TestDatabaseName
{
    /// <summary>Il limite di Postgres per un identificatore, in byte.</summary>
    internal const int MaxIdentifierLength = 63;

    /// <summary>La parte che le fixture aggiungono sempre: <c>_</c> + 32 cifre esadecimali.</summary>
    internal const int GuidPartLength = 33;

    private static readonly Regex AllowedCharacters =
        new("^[a-z0-9_]+$", RegexOptions.None, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Valida <paramref name="databasePrefix"/> per una fixture che poi aggiunge al nome un GUID e
    /// un suffisso di <paramref name="longestSuffixLength"/> caratteri.
    /// </summary>
    /// <param name="databasePrefix">Il prefisso scelto dalla fixture concreta.</param>
    /// <param name="longestSuffixLength">
    /// La lunghezza del suffisso più lungo che la fixture apporrà (<c>0</c> se non ne appone).
    /// Conta il PIÙ LUNGO, non quello tipico: un <c>_t100</c> che tronca collide con <c>_t10</c>,
    /// e due test finirebbero sullo stesso database senza che nulla lo dica.
    /// </param>
    /// <param name="parameterName">Il nome del parametro, per l'eccezione.</param>
    /// <exception cref="ArgumentException">
    /// Se il prefisso contiene caratteri fuori da <c>[a-z0-9_]</c> — finisce in una
    /// <c>CREATE DATABASE</c> interpolata — o se i nomi generati supererebbero i 63 byte.
    /// </exception>
    internal static void ValidatePrefix(
        string databasePrefix, int longestSuffixLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePrefix, parameterName);

        if (!AllowedCharacters.IsMatch(databasePrefix))
        {
            throw new ArgumentException(
                "il prefisso del database deve contenere solo [a-z0-9_]: finisce in una " +
                "CREATE DATABASE interpolata",
                parameterName);
        }

        var budget = MaxIdentifierLength - GuidPartLength - longestSuffixLength;
        if (databasePrefix.Length > budget)
        {
            throw new ArgumentException(
                $"il prefisso del database non può superare {budget} caratteri (ricevuti " +
                $"{databasePrefix.Length}: «{databasePrefix}»). Oltre quella soglia i nomi generati " +
                $"passano i {MaxIdentifierLength} byte e Postgres li tronca in silenzio: la " +
                "CREATE DATABASE continua a funzionare, ma le query che cercano quel database per " +
                "NOME in pg_stat_activity smettono di trovarlo, e con loro la terminazione dei " +
                "backend da cui dipende il clone",
                parameterName);
        }
    }
}
