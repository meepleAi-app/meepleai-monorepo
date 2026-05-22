using Api.BoundedContexts.SecurityAudit.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.AdminCategories;

/// <summary>
/// Create a new shared-games category (admin). Issue #1440 — Phase 2 BE.
/// </summary>
internal sealed record CreateGameCategoryCommand(
    string Name,
    string Slug,
    string? Emoji,
    string? Color,
    Guid ActorUserId) : IRequest<GameCategoryDto>;

internal sealed class CreateGameCategoryCommandValidator : AbstractValidator<CreateGameCategoryCommand>
{
    public CreateGameCategoryCommandValidator()
    {
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

        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithMessage("ActorUserId is required");
    }
}

internal sealed class CreateGameCategoryCommandHandler : IRequestHandler<CreateGameCategoryCommand, GameCategoryDto>
{
    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly IAuditLogger _auditLogger;
    private readonly TimeProvider _clock;

    public CreateGameCategoryCommandHandler(
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

    public async Task<GameCategoryDto> Handle(CreateGameCategoryCommand cmd, CancellationToken cancellationToken)
    {
        // Uniqueness pre-check — duplicate DB constraint still protects but
        // we want a clean 409 rather than a 500 surfaced from EF.
        var nameTaken = await _context.GameCategories
            .AnyAsync(c => c.Name == cmd.Name, cancellationToken).ConfigureAwait(false);
        if (nameTaken)
        {
            throw new ConflictException($"Category with name '{cmd.Name}' already exists");
        }

        var slugTaken = await _context.GameCategories
            .AnyAsync(c => c.Slug == cmd.Slug, cancellationToken).ConfigureAwait(false);
        if (slugTaken)
        {
            throw new ConflictException($"Category with slug '{cmd.Slug}' already exists");
        }

        var entity = new GameCategoryEntity
        {
            Id = Guid.NewGuid(),
            Name = cmd.Name,
            Slug = cmd.Slug,
            Emoji = cmd.Emoji,
            Color = cmd.Color,
            CreatedAt = _clock.GetUtcNow().UtcDateTime,
        };

        _context.GameCategories.Add(entity);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Invalidate the public categories cache so the new entry shows up
        // immediately on filter dropdowns (no 24h stale read).
        await _cache.RemoveAsync("game-categories", cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            eventType: AuditEventType.CategoryCreated,
            actorUserId: cmd.ActorUserId,
            metadata: $"{{\"id\":\"{entity.Id}\",\"name\":\"{entity.Name}\",\"slug\":\"{entity.Slug}\"}}",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new GameCategoryDto(entity.Id, entity.Name, entity.Slug, entity.Emoji, entity.Color, 0);
    }
}
