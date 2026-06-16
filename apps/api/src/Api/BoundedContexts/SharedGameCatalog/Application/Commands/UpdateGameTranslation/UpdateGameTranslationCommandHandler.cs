using Api.BoundedContexts.SharedGameCatalog.Application.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.UpdateGameTranslation;

/// <summary>
/// Handler for <see cref="UpdateGameTranslationCommand"/>. Issue #2339 — sub-PR 1/3.
/// </summary>
/// <remarks>
/// <para>
/// Per DEC-C2 the translation-existence check lives here (not in the validator) — we
/// load via <see cref="ISharedGameTranslationRepository.GetByGameIdAndLocaleAsync"/>
/// and surface a <see cref="TranslationNotFoundException"/> when missing
/// (CLAUDE.md pitfall #2568: 404 not 500).
/// </para>
/// <para>
/// Per DEC-C4 the client-supplied <c>xmin</c> is pushed onto the aggregate via
/// <see cref="Domain.Entities.SharedGameTranslation.SetXmin"/>
/// before <c>SaveChangesAsync</c>. EF compares the stored value against the row's
/// current <c>xmin</c> and raises
/// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/> on
/// mismatch — left to bubble so the global middleware can map it to 409 with
/// <c>X-Warning-Code: concurrent-edit</c>.
/// </para>
/// </remarks>
internal sealed class UpdateGameTranslationCommandHandler
    : IRequestHandler<UpdateGameTranslationCommand, Unit>
{
    private readonly ISharedGameTranslationRepository _translationRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public UpdateGameTranslationCommandHandler(
        ISharedGameTranslationRepository translationRepo,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(translationRepo);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        _translationRepo = translationRepo;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Unit> Handle(UpdateGameTranslationCommand cmd, CancellationToken cancellationToken)
    {
        var locale = Locale.Create(cmd.Locale);

        var existing = await _translationRepo
            .GetByGameIdAndLocaleAsync(cmd.GameId, locale.Value, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            throw new TranslationNotFoundException(cmd.GameId, locale.Value);
        }

        var now = _clock.GetUtcNow();
        // Domain methods enforce: not-deleted invariant + max-length validation.
        existing.UpdateTitle(cmd.Title, cmd.ActorUserId, now);
        existing.UpdateDescription(cmd.Description, cmd.ActorUserId, now);

        // DEC-C4: push client-supplied xmin so EF can run the concurrency check.
        existing.SetXmin(cmd.Xmin);

        await _translationRepo.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        // DbUpdateConcurrencyException bubbles up — caught by global exception middleware
        // and surfaced as 409 with X-Warning-Code: concurrent-edit.
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
