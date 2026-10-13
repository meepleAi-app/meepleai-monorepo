using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4155: il cancello RAG in un punto solo. Gli handler che rispondono a una domanda sulla KB di
/// un gioco ripetevano lo stesso <c>if (!canAccess) throw new ForbiddenException(...)</c>; ora
/// chiamano questo, e il diniego ha sempre lo stesso tipo e lo stesso codice.
/// </summary>
/// <remarks>
/// Un'estensione e non un metodo dell'interfaccia: passa da <see cref="IRagAccessService.CanAccessRagAsync"/>,
/// quindi i doppi dei test che configurano quel metodo continuano a pilotare il cancello.
/// </remarks>
public static class RagAccessGuard
{
    /// <summary>
    /// Lancia <see cref="RagOwnershipRequiredException"/> se l'utente non può interrogare la KB
    /// del gioco.
    /// </summary>
    public static async Task EnsureRagAccessAsync(
        this IRagAccessService ragAccessService,
        Guid userId,
        Guid gameId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ragAccessService);

        if (!await ragAccessService.CanAccessRagAsync(userId, gameId, role, cancellationToken).ConfigureAwait(false))
            throw new RagOwnershipRequiredException(gameId);
    }
}
