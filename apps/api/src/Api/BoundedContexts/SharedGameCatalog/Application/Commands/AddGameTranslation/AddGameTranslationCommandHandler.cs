using Api.BoundedContexts.SharedGameCatalog.Application.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddGameTranslation;

/// <summary>
/// Handler for <see cref="AddGameTranslationCommand"/>. Issue #2339 — sub-PR 1/3.
/// </summary>
/// <remarks>
/// <para>
/// Per DEC-C2 the game-existence check lives here (not in the validator) — we use
/// <see cref="ISharedGameRepository.GetByIdAsync"/> and surface a
/// <see cref="NotFoundException"/> when the parent row is missing. This satisfies
/// CLAUDE.md pitfall #2568 (404 not 500 on missing FK).
/// </para>
/// <para>
/// The unique partial index <c>uq_active_translation_per_locale</c> still protects the
/// invariant in the face of concurrent writers: when the validator's
/// <c>ExistsActiveAsync</c> race is lost, <c>SaveChangesAsync</c> throws a
/// <see cref="DbUpdateException"/> referencing the constraint name and we rethrow as
/// <see cref="TranslationAlreadyExistsException"/> (409).
/// </para>
/// </remarks>
internal sealed class AddGameTranslationCommandHandler
    : IRequestHandler<AddGameTranslationCommand, Guid>
{
    private const string UniqueIndexName = "uq_active_translation_per_locale";

    private readonly ISharedGameTranslationRepository _translationRepo;
    private readonly ISharedGameRepository _gameRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public AddGameTranslationCommandHandler(
        ISharedGameTranslationRepository translationRepo,
        ISharedGameRepository gameRepo,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(translationRepo);
        ArgumentNullException.ThrowIfNull(gameRepo);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        _translationRepo = translationRepo;
        _gameRepo = gameRepo;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Guid> Handle(AddGameTranslationCommand cmd, CancellationToken cancellationToken)
    {
        // DEC-C2: parent-row existence check is here, not in the validator
        // (CLAUDE.md pitfall #2568: 404 not 500).
        var parent = await _gameRepo
            .GetByIdAsync(cmd.GameId, cancellationToken)
            .ConfigureAwait(false);
        if (parent is null)
        {
            throw new NotFoundException("SharedGame", cmd.GameId.ToString());
        }

        var locale = Locale.Create(cmd.Locale);
        if (!TranslationSourceMapper.TryFromPersistedString(cmd.Source, out var source))
        {
            // Validator already rejects this, but the handler is defensive — pre-condition
            // failures here are a programmer error (caller bypassed the pipeline).
            throw new ArgumentException(
                $"Unknown source '{cmd.Source}'", nameof(cmd));
        }

        var now = _clock.GetUtcNow();
        var translation = SharedGameTranslation.Create(
            sharedGameId: cmd.GameId,
            locale: locale,
            title: cmd.Title,
            description: cmd.Description,
            source: source,
            createdBy: cmd.ActorUserId,
            now: now);

        await _translationRepo.AddAsync(translation, cancellationToken).ConfigureAwait(false);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueLocaleViolation(ex))
        {
            throw new TranslationAlreadyExistsException(cmd.GameId, locale.Value);
        }

        return translation.Id;
    }

    private static bool IsUniqueLocaleViolation(DbUpdateException ex)
    {
        // Npgsql surfaces the index name in the outer message; check both the outer
        // exception and the inner (Postgres) one to be tolerant of provider quirks.
        if (ex.Message.Contains(UniqueIndexName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ex.InnerException is { } inner
            && inner.Message.Contains(UniqueIndexName, StringComparison.OrdinalIgnoreCase);
    }
}
