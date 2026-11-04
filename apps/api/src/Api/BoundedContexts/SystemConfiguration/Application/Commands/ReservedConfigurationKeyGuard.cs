using Api.Middleware.Exceptions;
using Api.SharedKernel.Constants;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// La difesa dell'API admin per le chiavi riservate (#4612): vedi <see cref="ReservedConfigurationKeys"/>.
/// Gli handler che scrivono una configurazione scelta dal chiamante la chiamano dopo aver letto la riga, così
/// una riga che non esiste continua a rispondere come prima.
/// </summary>
/// <remarks>
/// 403 e non 409: il rifiuto non dipende dallo stato della riga, vale sempre. Il messaggio nomina solo la
/// costante, mai il testo ricevuto.
/// </remarks>
internal static class ReservedConfigurationKeyGuard
{
    public static void EnsureNotReserved(string key)
    {
        if (ReservedConfigurationKeys.IsReserved(key))
        {
            throw new ForbiddenException(
                $"The configuration key '{ReservedConfigurationKeys.BootstrapAdmin}' is reserved: it records that the " +
                "first admin was claimed, and it cannot be created, changed, rolled back or deleted through the admin API.");
        }
    }
}
