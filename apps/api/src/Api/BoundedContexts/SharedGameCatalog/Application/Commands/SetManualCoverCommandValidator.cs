using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Observability;
using Api.SharedKernel.Infrastructure.Http;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Boundary validation for <see cref="SetManualCoverCommand"/> — a 4xx client error (422 via the
/// FluentValidation pipeline), not a 500, on bad input.
/// <para>
/// The license MUST be on the DEC-3c whitelist (Public Domain / CC0 / CC-BY / CC-BY-SA). This is the
/// primary copyright gate for the arbitrary-URL manual path (epic #3470 Slice 3a / ADR-059): it
/// rejects "All Rights Reserved" / BGG / CC-BY-NC covers BEFORE anything is downloaded or stored, so
/// the manual path cannot launder around the catalog's redistribution posture.
/// </para>
/// </summary>
internal sealed class SetManualCoverCommandValidator : AbstractValidator<SetManualCoverCommand>
{
    public SetManualCoverCommandValidator()
    {
        RuleFor(x => x.GameId).NotEqual(Guid.Empty).WithMessage("GameId is required");
        RuleFor(x => x.AdminId).NotEqual(Guid.Empty).WithMessage("AdminId is required");

        RuleFor(x => x.SourceUrl)
            .NotEmpty().WithMessage("SourceUrl is required")
            .Must(BeAbsoluteHttps).WithMessage("SourceUrl must be an absolute HTTPS URL")
            .Must(url => !IsBannedAndRecorded(url))
            .WithMessage("SourceUrl host is banned by ADR-059 §5 (BGG/geekdo assets)");

        RuleFor(x => x.License)
            .NotEmpty().WithMessage("License is required")
            .Must(LicenseValidator.IsWhitelisted)
            .WithMessage("License must be on the whitelist (Public Domain / CC0 / CC-BY / CC-BY-SA)");
    }

    /// <summary>
    /// #3583 — registra l'hit sulla deny-list ADR-059 §5 prima di far fallire la regola, così un
    /// tentativo ripetuto di laundering attorno al ban BGG è visibile su
    /// <c>meepleai_egress_blocked_total</c>. Il predicato conserva la semantica originale:
    /// ritorna true quando l'URL è bandito (quindi la regola <c>Must(!…)</c> fallisce).
    /// <para>
    /// PRECONDIZIONE per la correttezza del conteggio: questo predicato viene valutato ESATTAMENTE
    /// una volta per richiesta. Oggi è vero perché (a) il cascade rule-level è il default
    /// <c>Continue</c> e la catena su SourceUrl esegue questo Must una sola volta, e (b) il validator
    /// è invocato solo dal <c>ValidationBehavior</c> di MediatR, una volta per comando. È una
    /// garanzia EMERGENTE, non protetta da un test: un secondo
    /// <c>IValidator&lt;SetManualCoverCommand&gt;</c> registrato, o un endpoint filter che validi
    /// prima di MediatR, farebbero raddoppiare il counter in silenzio.
    /// </para>
    /// </summary>
    private static bool IsBannedAndRecorded(string? url)
    {
        if (!BggHostDenyList.IsBanned(url))
        {
            return false;
        }

        MeepleAiMetrics.RecordEgressBlocked(
            MeepleAiMetrics.EgressSinks.Manual, MeepleAiMetrics.EgressBlockReasons.DenylistHit);
        return true;
    }

    private static bool BeAbsoluteHttps(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
