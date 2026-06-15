using Api.BoundedContexts.SharedGameCatalog.Application.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.DeleteGameTranslation;

/// <summary>
/// Validator for <see cref="DeleteGameTranslationCommand"/>. Issue #2339 — sub-PR 1/3.
/// </summary>
/// <remarks>
/// <para>
/// Symmetric to <c>UpdateGameTranslationCommandValidator</c> minus the title checks
/// (soft-delete carries no payload beyond GameId/Locale/Xmin/ActorUserId).
/// </para>
/// <para>
/// Per DEC-C2 the translation-existence check is NOT here — the handler loads via
/// <c>GetByGameIdAndLocaleAsync</c> and throws <c>TranslationNotFoundException</c>
/// when missing, mapped to 404 by the global exception middleware
/// (CLAUDE.md pitfall #2568).
/// </para>
/// </remarks>
public sealed class DeleteGameTranslationCommandValidator
    : AbstractValidator<DeleteGameTranslationCommand>
{
    public DeleteGameTranslationCommandValidator()
    {
        RuleFor(c => c.GameId).NotEmpty();
        RuleFor(c => c.ActorUserId).NotEmpty();

        RuleFor(c => c.Locale)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(BeValidLocale).WithMessage("Invalid ISO 639-1 locale");

        RuleFor(c => c.Xmin)
            .GreaterThan(0u)
            .WithMessage("Xmin required for optimistic concurrency check");
    }

    private static bool BeValidLocale(string raw)
    {
        try
        {
            Locale.Create(raw);
            return true;
        }
        catch (InvalidLocaleException)
        {
            return false;
        }
    }
}
