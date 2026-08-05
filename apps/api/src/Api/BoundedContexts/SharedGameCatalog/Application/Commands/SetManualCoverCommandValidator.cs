using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
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
            .Must(url => !BggHostDenyList.IsBanned(url))
            .WithMessage("SourceUrl host is banned by ADR-059 §5 (BGG/geekdo assets)");

        RuleFor(x => x.License)
            .NotEmpty().WithMessage("License is required")
            .Must(LicenseValidator.IsWhitelisted)
            .WithMessage("License must be on the whitelist (Public Domain / CC0 / CC-BY / CC-BY-SA)");
    }

    private static bool BeAbsoluteHttps(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
