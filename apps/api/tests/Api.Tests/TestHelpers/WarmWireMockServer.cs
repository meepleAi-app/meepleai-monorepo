using System.Diagnostics;
using System.Globalization;
using WireMock.Server;
using Xunit;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Starts a <see cref="WireMockServer"/> that has already answered one request (#4298).
/// <para>
/// On the first request it receives in a process, WireMock.Net looks for its optional plugins by
/// loading every assembly in the output directory. Measured on this suite on 2026-10-13: 150
/// assemblies loaded inside that one request — 1.3 to 2.2 s on an idle developer machine, against
/// 33 ms for a bare Kestrel in the same process, and more than 5 s on a CI runner that has never
/// read those files. It is paid once per process, by whichever test talks to WireMock first.
/// </para>
/// <para>
/// That test loses any real deadline the code under test puts on the call. The provider probe
/// gives up after 5 s, so <c>Probe_Provider401_Returns200WithUnauthorized</c> — first of its class
/// by xUnit's ordering — got <c>timeout</c> instead of <c>unauthorized</c> on most runs of the
/// Games shard, while every later test of the class passed.
/// </para>
/// <para>
/// The exchange made here is not a longer wait in front of the same race: it happens before the
/// code under test starts its clock. The request log is cleared afterwards, so a test that counts
/// the requests its server received still starts from zero.
/// </para>
/// <para>
/// <c>WireMockWarmStartArchitectureTests</c> rejects a test that starts WireMock any other way.
/// </para>
/// </summary>
internal static class WarmWireMockServer
{
    private static int _firstExchangeReported;

    public static async Task<WireMockServer> StartAsync()
    {
        var server = WireMockServer.Start();
        try
        {
            var started = Stopwatch.GetTimestamp();

            // HttpClient's default 100 s bounds the wait: if WireMock never answers, the failure
            // names this exchange instead of surfacing as an assertion of the test that follows.
            using var client = new HttpClient();

            // No stub is registered yet, so this is answered with a 404 — any answer will do, the
            // point is that one full exchange has completed.
            using var response = await client
                .GetAsync(new Uri($"{server.Url}/warm-up"))
                .ConfigureAwait(false);

            if (Interlocked.Exchange(ref _firstExchangeReported, 1) == 0)
            {
                // Once per process, on the fixture-timing channel: the cost this class exists for
                // is not part of anything a test asserts on, so the job log is where it shows.
                TestContext.Current?.SendDiagnosticMessage(string.Create(
                    CultureInfo.InvariantCulture,
                    $"wiremock-first-exchange {Stopwatch.GetElapsedTime(started).TotalSeconds:F1}s"));
            }

            server.ResetLogEntries();
            return server;
        }
        catch
        {
            server.Dispose();
            throw;
        }
    }
}
