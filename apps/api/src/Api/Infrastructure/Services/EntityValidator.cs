using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.Services;

/// <summary>
/// Implementation of entity existence validation across bounded contexts.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
internal class EntityValidator : IEntityValidator
{
    private readonly MeepleAiDbContext _context;

    public EntityValidator(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task ValidateEntityExistsAsync(
        EntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        var exists = entityType switch
        {
            EntityType.Game => await _context.Games
                .AnyAsync(g => g.Id == entityId, cancellationToken)
                .ConfigureAwait(false),

            // Phase 2 scope: Validate existing entities
            EntityType.Player => true,  // NOTE: Player entity not yet implemented, validation deferred
            EntityType.Event => true,   // NOTE: Event entity not yet implemented, validation deferred
            EntityType.Session => true, // NOTE: Session entity not yet implemented, validation deferred
            EntityType.Agent => true,   // NOTE: Agent entity not yet implemented, validation deferred
            EntityType.Document => await _context.PdfDocuments
                .AnyAsync(d => d.Id == entityId, cancellationToken)
                .ConfigureAwait(false),
            EntityType.ChatSession => await _context.ChatSessions
                .AnyAsync(c => c.Id == entityId, cancellationToken)
                .ConfigureAwait(false),

            _ => throw new NotSupportedException($"EntityType {entityType} is not supported")
        };

        if (!exists)
        {
            throw new NotFoundException($"{entityType} with ID {entityId} not found");
        }
    }
}
