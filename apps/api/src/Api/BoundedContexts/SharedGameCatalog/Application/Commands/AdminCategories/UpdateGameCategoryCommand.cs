using Api.BoundedContexts.SecurityAudit.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.AdminCategories;

/// <summary>
/// Update an existing shared-games category (admin). Issue #1440 — Phase 2 BE.
/// </summary>
internal sealed record UpdateGameCategoryCommand(
    Guid Id,
    string Name,
    string Slug,
    string? Emoji,
    string? Color,
    Guid ActorUserId) : IRequest<GameCategoryDto>;

internal sealed class UpdateGameCategoryCommandValidator : AbstractValidator<UpdateGameCategoryCommand>
{
    public UpdateGameCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(50).WithMessage("Name cannot exceed 50 characters");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required")
            .MaximumLength(100).WithMessage("Slug cannot exceed 100 characters")
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$").WithMessage("Slug must be lowercase alphanumeric with hyphens");

        RuleFor(x => x.Emoji)
            .MaximumLength(16).WithMessage("Emoji cannot exceed 16 characters")
            .When(x => !string.IsNullOrEmpty(x.Emoji));

        RuleFor(x => x.Color)
            .Matches("^#[0-9a-fA-F]{6}$").WithMessage("Color must be a 6-digit hex value like #RRGGBB")
            .When(x => !string.IsNullOrEmpty(x.Color));

        RuleFor(x => x.ActorUserId).NotEmpty();
    }
}

internal sealed class UpdateGameCategoryCommandHandler : IRequestHandler<UpdateGameCategoryCommand, GameCategoryDto>
{
    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly IAuditLogger _auditLogger;
    private readonly TimeProvider _clock;

    public UpdateGameCategoryCommandHandler(
        MeepleAiDbContext context,
        HybridCache cache,
        IAuditLogger auditLogger,
        TimeProvider clock)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<GameCategoryDto> Handle(UpdateGameCategoryCommand cmd, CancellationToken cancellationToken)
    {
        // Include SharedGames so we can derive the post-update gameCount
        // from the in-memory navigation collection — saves a round-trip vs
        // a second projection query after SaveChangesAsync.
        var entity = await _context.GameCategories
            .Include(c => c.SharedGames)
            .FirstOrDefaultAsync(c => c.Id == cmd.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new NotFoundException("Category", cmd.Id.ToString());
        }

        // Unique checks — exclude self.
        var nameTaken = await _context.GameCategories
            .AnyAsync(c => c.Id != cmd.Id && c.Name == cmd.Name, cancellationToken).ConfigureAwait(false);
        if (nameTaken)
        {
            throw new ConflictException($"Category with name '{cmd.Name}' already exists");
        }

        var slugTaken = await _context.GameCategories
            .AnyAsync(c => c.Id != cmd.Id && c.Slug == cmd.Slug, cancellationToken).ConfigureAwait(false);
        if (slugTaken)
        {
            throw new ConflictException($"Category with slug '{cmd.Slug}' already exists");
        }

        entity.Name = cmd.Name;
        entity.Slug = cmd.Slug;
        entity.Emoji = cmd.Emoji;
        entity.Color = cmd.Color;
        entity.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        entity.UpdatedBy = cmd.ActorUserId;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _cache.RemoveAsync("game-categories", cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            eventType: AuditEventType.CategoryUpdated,
            actorUserId: cmd.ActorUserId,
            metadata: $"{{\"id\":\"{entity.Id}\",\"name\":\"{entity.Name}\"}}",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new GameCategoryDto(
            entity.Id,
            entity.Name,
            entity.Slug,
            entity.Emoji,
            entity.Color,
            entity.SharedGames.Count);
    }
}
