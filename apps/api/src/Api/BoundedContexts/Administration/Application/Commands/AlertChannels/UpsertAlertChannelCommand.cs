using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertChannels;

/// <summary>
/// Creates or updates an alert channel configuration (Issue #1840 SP5 F4-C7).
///
/// <para>The channel <see cref="Type"/> is part of the URL
/// (PUT /admin/alert-channels/{type}) and acts as the natural key. The
/// <see cref="RowVersion"/> field carries the Postgres xmin token; pass
/// an empty string for first-time creation. Returning a stale token on update
/// surfaces as a 409 ConflictException.</para>
/// </summary>
internal sealed record UpsertAlertChannelCommand(
    string Type,
    string ConfigJson,
    bool IsEnabled,
    string? RowVersion,
    string UpdatedBy) : IRequest<AlertChannelUpsertResult>;

/// <summary>Result returned to the endpoint after a successful upsert.</summary>
internal sealed record AlertChannelUpsertResult(string Type, DateTime UpdatedAt, string RowVersion);

internal sealed class UpsertAlertChannelCommandValidator : AbstractValidator<UpsertAlertChannelCommand>
{
    private static readonly string[] AllowedTypes = { "email", "slack" };

    public UpsertAlertChannelCommandValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(t => AllowedTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Type must be one of: email, slack");

        RuleFor(x => x.ConfigJson)
            .NotEmpty()
            .Must(c => c.AsSpan().TrimStart().Length > 0 && c.AsSpan().TrimStart()[0] == '{')
            .WithMessage("ConfigJson must be a JSON object");

        RuleFor(x => x.UpdatedBy)
            .NotEmpty()
            .MaximumLength(200);
    }
}
