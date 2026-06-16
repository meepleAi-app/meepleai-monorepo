using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddGameTranslation;

/// <summary>
/// Validator for <see cref="AddGameTranslationCommand"/>. Issue #2339 — sub-PR 1/3.
/// </summary>
/// <remarks>
/// <para>
/// DEC-C2: the game-existence check is deliberately NOT here. <see cref="ISharedGameRepository"/>
/// exposes <c>GetByIdAsync</c>, not <c>ExistsByIdAsync</c>, and validators should not hydrate
/// aggregates. The handler performs the existence check and throws a <c>NotFoundException</c>
/// when the parent row is missing (CLAUDE.md pitfall #2568: 404 not 500).
/// </para>
/// <para>
/// The duplicate-locale check short-circuits the unique constraint
/// <c>uq_active_translation_per_locale</c> with a 409 hint at the validator boundary so
/// the consumer doesn't have to interpret a raw <c>DbUpdateException</c>.
/// </para>
/// </remarks>
public sealed class AddGameTranslationCommandValidator
    : AbstractValidator<AddGameTranslationCommand>
{
    public AddGameTranslationCommandValidator(
        ISharedGameTranslationRepository translationRepository)
    {
        ArgumentNullException.ThrowIfNull(translationRepository);

        RuleFor(c => c.GameId).NotEmpty();

        RuleFor(c => c.ActorUserId).NotEmpty();

        RuleFor(c => c.Locale)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(SharedTranslationValidationRules.BeValidLocale).WithMessage("Invalid ISO 639-1 locale")
            .MustAsync(async (cmd, locale, ct) =>
                !await translationRepository
                    .ExistsActiveAsync(cmd.GameId, NormalizeLocale(locale), ct)
                    .ConfigureAwait(false))
            .WithMessage("Translation for locale '{PropertyValue}' already exists");

        RuleFor(c => c.Title)
            .NotEmpty().WithMessage("Title required")
            .MaximumLength(500);

        RuleFor(c => c.Source)
            .NotEmpty()
            .Must(BeValidSource)
            .WithMessage("Invalid source — must be one of: manual | auto-openrouter | community");
    }

    // BeValidLocale (via Cascade(CascadeMode.Stop)) guarantees this is only called
    // on a TryCreate-accepting string. Issue #2399 — TryCreate consolidates the
    // exception-free parser; if BeValidLocale fired Stop, we never reach the
    // fallback branch below.
    private static string NormalizeLocale(string raw) =>
        Locale.TryCreate(raw, out var locale) ? locale.Value : raw;

    private static bool BeValidSource(string source) =>
        TranslationSourceMapper.TryFromPersistedString(source, out _);
}
