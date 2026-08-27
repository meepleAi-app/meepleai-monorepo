using System.Text;
using System.Text.Json;

using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.LiveSessions;

/// <summary>
/// Validates <see cref="UpdateLiveGameStateCommand"/> (#3025 L1). The state is opaque at L1
/// (no per-game shape check — that is L2); only guard non-empty ids + a UTF-8 byte size cap.
/// </summary>
internal sealed class UpdateLiveGameStateCommandValidator : AbstractValidator<UpdateLiveGameStateCommand>
{
    private const int MaxStateBytes = 256 * 1024;

    public UpdateLiveGameStateCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.RequestedByUserId).NotEmpty();
        // GetRawText() is a UTF-16 .NET string; count UTF-8 BYTES for an accurate wire-size cap.
        // #3847 — con "state" assente arriva un JsonElement di default, e GetRawText() su un
        // ValueKind.Undefined solleva InvalidOperationException: il client riceveva 500 invece di
        // sapere che il campo manca.
        RuleFor(x => x.State)
            .Must(s => s.ValueKind != JsonValueKind.Undefined)
            .WithMessage("Game state is required.")
            .Must(s => Encoding.UTF8.GetByteCount(s.GetRawText()) <= MaxStateBytes)
            .WithMessage($"Game state exceeds the {MaxStateBytes} byte limit.");
    }
}
