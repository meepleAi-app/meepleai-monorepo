using Api.BoundedContexts.SecurityAudit.Application.Services;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.AdminCategories;

/// <summary>
/// Delete a shared-games category (admin). Forbidden when the category has
/// any linked SharedGame — AC-4 in #1440 explicitly rejects cascade so we
/// surface a 409 Conflict and let the operator detag games first.
/// </summary>
internal sealed record DeleteGameCategoryCommand(Guid Id, Guid ActorUserId) : IRequest;

internal sealed class DeleteGameCategoryCommandValidator : AbstractValidator<DeleteGameCategoryCommand>
{
    public DeleteGameCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
    }
}

internal sealed class DeleteGameCategoryCommandHandler : IRequestHandler<DeleteGameCategoryCommand>
{
    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly IAuditLogger _auditLogger;

    public DeleteGameCategoryCommandHandler(
        MeepleAiDbContext context,
        HybridCache cache,
        IAuditLogger auditLogger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task Handle(DeleteGameCategoryCommand cmd, CancellationToken cancellationToken)
    {
        var entity = await _context.GameCategories
            .Include(c => c.SharedGames)
            .FirstOrDefaultAsync(c => c.Id == cmd.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new NotFoundException("Category", cmd.Id.ToString());
        }

        if (entity.SharedGames.Count > 0)
        {
            throw new ConflictException(
                $"Cannot delete category '{entity.Name}' — {entity.SharedGames.Count} linked games. Detag them first.");
        }

        var name = entity.Name;
        _context.GameCategories.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _cache.RemoveAsync("game-categories", cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            eventType: AuditEventType.CategoryDeleted,
            actorUserId: cmd.ActorUserId,
            metadata: $"{{\"id\":\"{cmd.Id}\",\"name\":\"{name}\"}}",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
