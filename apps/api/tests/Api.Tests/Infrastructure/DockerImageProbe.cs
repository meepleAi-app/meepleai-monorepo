using System.Diagnostics;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Accerta la presenza di un'immagine Docker <b>prima</b> di costruire un container.
///
/// <para>🔴 Perché una sonda e non un try/catch attorno a <c>StartAsync</c>. Issue #4076:
/// <c>Performance_P95Latency_WithRealServices</c> non falliva per latenza nonostante il nome —
/// falliva in <b>287 ms</b> perché l'immagine non c'era e Testcontainers provava a scaricarla:</para>
///
/// <code>
/// Docker.DotNet.DockerApiException : status code='NotFound', response='{"message":
/// "pull access denied for infra-unstructured-service, repository does not exist
/// or may require 'docker login': denied: requested access to the resource is denied"}'
/// </code>
///
/// <para>La policy #4021 lo dice in generale: «l'assenza di un servizio si ACCERTA con una sonda,
/// non si deduce da una risposta». Catturare l'eccezione avrebbe funzionato per questo caso e
/// avrebbe <b>nascosto</b> il successivo: un'immagine presente ma un container che non diventa
/// sano è un difetto, non un prerequisito assente, e deve continuare a far fallire il test.</para>
///
/// <para>⚠️ <b>La decisione è separata dall'effetto</b>, e non è pedanteria: un helper che lancia
/// <c>SkipException</c> al posto del chiamante fa saltare anche il test che verifica l'helper, e il
/// verificatore diventa inerte. Qui il metodo <b>ritorna</b> il nome trovato oppure <c>null</c>; è
/// il test a decidere di chiamare <c>Assert.Skip</c>, con il proprio motivo classificato.</para>
///
/// <para><b>Due convenzioni di tag convivono nel repo</b>, e la sonda accetta entrambe perché
/// nessuna delle due è sbagliata:</para>
/// <list type="bullet">
///   <item><c>meepleai-&lt;servizio&gt;:latest</c> — quello che Docker Compose produce
///     (<c>infra/docker-compose.yml</c> porta <c>name: meepleai</c> dal commit iniziale, e Compose
///     nomina le immagini <c>&lt;progetto&gt;-&lt;servizio&gt;</c>).</item>
///   <item><c>infra-&lt;servizio&gt;:latest</c> — quello che le costanti di
///     <see cref="TestcontainersConfiguration"/> dichiarano e che i messaggi diagnostici del
///     fixture chiedono di costruire a mano (<c>docker build -t infra-…</c>). Risale a quando il
///     progetto Compose prendeva il nome dalla cartella <c>infra/</c>.</item>
/// </list>
/// </summary>
internal static class DockerImageProbe
{
    /// <summary>Tempo massimo concesso a <c>docker image inspect</c> per rispondere.</summary>
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Ritorna il primo tag presente localmente fra quelli indicati, oppure <c>null</c> se
    /// nessuno lo è — <b>senza</b> tentare alcun pull e <b>senza</b> lanciare.
    /// </summary>
    /// <remarks>
    /// Usa <c>docker image inspect</c>, che a differenza di <c>docker run</c> non prova a
    /// scaricare: distingue quindi «assente in locale» da «assente nel registry», che è la
    /// distinzione che serve per classificare il motivo del salto.
    /// </remarks>
    public static string? FindFirstPresent(params string[] candidateTags)
    {
        ArgumentNullException.ThrowIfNull(candidateTags);

        foreach (var tag in candidateTags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            if (IsPresentLocally(tag))
            {
                return tag;
            }
        }

        return null;
    }

    private static bool IsPresentLocally(string tag)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    // `--format {{.Id}}` tiene l'output a una riga: qui interessa solo l'esito.
                    Arguments = $"image inspect --format {{{{.Id}}}} {tag}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();

            if (!process.WaitForExit((int)ProbeTimeout.TotalMilliseconds))
            {
                TryKill(process);
                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // `docker` non è nel PATH, oppure il processo non è avviabile. Non è un'immagine
            // assente: è Docker assente. In entrambi i casi il container non partirebbe, e il
            // chiamante deve trattarlo come prerequisito mancante — ma il motivo del salto che
            // scriverà dovrà dirlo, perché «immagine assente» e «docker assente» si correggono
            // in modi diversi.
            return false;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            // Il processo è già terminato fra WaitForExit e Kill: non c'è niente da fare.
        }
    }
}
