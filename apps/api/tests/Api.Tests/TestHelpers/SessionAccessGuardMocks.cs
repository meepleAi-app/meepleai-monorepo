using Api.BoundedContexts.SessionTracking.Domain.Services;
using Moq;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Helper per i test unitari degli handler di SessionTracking. Issue #3756.
///
/// <para>
/// Quegli handler ricevono ora un <see cref="ISessionAccessGuard"/> e lo invocano in testa a
/// <c>Handle</c>. I test unitari preesistenti verificano la <b>logica di dominio</b> — mazzo, chat,
/// media — non l'autorizzazione, quindi ricevono un guard permissivo e continuano a misurare cio'
/// che misuravano.
/// </para>
///
/// <para>
/// 🔴 Il guard vero e' coperto dove ha senso coprirlo: <c>SessionToolsAndDeckIdorIntegrationTests</c>
/// esercita i dieci endpoint via HTTP con un chiamante che non e' ne' owner ne' partecipante e
/// pretende 403. Un mock permissivo qui non indebolisce quella verifica.
/// </para>
///
/// <para>
/// La <c>Setup</c> esplicita con <c>Task.CompletedTask</c> non e' ridondante: senza, un mock loose
/// su un metodo che ritorna <c>Task</c> puo' restituire <c>null</c>, e l'<c>await</c> nell'handler
/// diventa una NullReferenceException che si legge come un difetto dell'handler.
/// </para>
/// </summary>
internal static class SessionAccessGuardMocks
{
    /// <summary>Guard che lascia passare qualunque chiamante.</summary>
    public static ISessionAccessGuard Permissive()
    {
        var mock = new Mock<ISessionAccessGuard>();
        mock.Setup(g => g.EnsureOwnerOrParticipantAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock.Object;
    }
}
