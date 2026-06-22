using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertChannels;

/// <summary>
/// Probes a channel's transport with a benign "connection test" payload
/// (Issue #1840 SP5 F4-C7). Used by the Canali drawer's "Test Connection"
/// button. The probe records its outcome on the AlertChannel aggregate
/// (LastTestedAt / LastTestStatus) so the UI can show a status pill.
///
/// <para>The command does NOT take a webhook URL — the URL is read from the
/// stored AlertChannel config so admins can verify the actual saved
/// configuration without leaking secrets back through the request body.</para>
/// </summary>
internal sealed record TestAlertChannelConnectionCommand(string Type)
    : IRequest<TestAlertChannelConnectionResult>;

/// <summary>Wire response surfaced to the admin UI.</summary>
internal sealed record TestAlertChannelConnectionResult(
    bool Success,
    string Message,
    int? StatusCode,
    DateTime TestedAt);

internal sealed class TestAlertChannelConnectionCommandValidator
    : AbstractValidator<TestAlertChannelConnectionCommand>
{
    private static readonly string[] AllowedTypes = { "email", "slack" };

    public TestAlertChannelConnectionCommandValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(t => AllowedTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Type must be one of: email, slack");
    }
}
