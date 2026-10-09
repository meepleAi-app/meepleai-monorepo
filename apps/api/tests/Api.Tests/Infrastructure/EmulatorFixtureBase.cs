using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Base delle fixture per CLASSE degli emulatori Azure (spec azure-local §6): un container per classe, perché
/// <c>methodTimeout</c> copre <c>InitializeAsync</c> e un emulatore per metodo scadrebbe in timeout. L'avvio
/// non lancia: registra il motivo e i test saltano con la classe giusta.
/// </summary>
public abstract class EmulatorFixtureBase : IAsyncLifetime
{
    public string? UnavailableReason { get; private set; }

    protected abstract string EmulatorName { get; }

    protected abstract Task StartAsync();

    protected abstract Task StopAsync();

    public async ValueTask InitializeAsync()
    {
        if (!DockerDaemonProbe.IsAvailable())
        {
            UnavailableReason = "PREVISTO: Docker non disponibile. In locale: cd infra && make dev-azure-local (avvia anche " + EmulatorName + ")";
            return;
        }

        try
        {
            await StartAsync();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            UnavailableReason = $"GUASTO: {EmulatorName} non avviato con Docker presente: {ex.GetType().Name}: {ex.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (UnavailableReason is null)
        {
            await StopAsync();
        }
    }

    public void SkipIfUnavailable()
    {
        if (UnavailableReason is not null)
        {
            Assert.Skip(UnavailableReason);
        }
    }
}
