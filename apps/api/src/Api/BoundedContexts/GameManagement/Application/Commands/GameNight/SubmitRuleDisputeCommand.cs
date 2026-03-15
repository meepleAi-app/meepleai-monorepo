using System.Text;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Services;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNight;

/// <summary>
/// Command for submitting a rule dispute during a live game session.
/// The handler queries the LLM with a structured arbitration prompt, parses the
/// verdict, stores the <see cref="RuleDisputeEntry"/> on the session, and
/// broadcasts a <see cref="DisputeResolvedEvent"/>.
/// Game Night Improvvisata — E3: Arbitro mode.
/// </summary>
public sealed record SubmitRuleDisputeCommand(
    Guid SessionId,
    Guid CallerUserId,
    string Description,
    string RaisedByPlayerName) : IRequest<RuleDisputeResponse>;

/// <summary>
/// Response returned after the AI arbitro resolves a rule dispute.
/// </summary>
public sealed record RuleDisputeResponse(
    Guid Id,
    string Verdict,
    List<string> RuleReferences,
    string? Note);

/// <summary>
/// Validates <see cref="SubmitRuleDisputeCommand"/> inputs.
/// </summary>
public sealed class SubmitRuleDisputeCommandValidator : AbstractValidator<SubmitRuleDisputeCommand>
{
    public SubmitRuleDisputeCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.CallerUserId)
            .NotEmpty()
            .WithMessage("Caller user ID is required");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Dispute description is required")
            .MaximumLength(1000)
            .WithMessage("Dispute description cannot exceed 1000 characters");

        RuleFor(x => x.RaisedByPlayerName)
            .NotEmpty()
            .WithMessage("Player name is required")
            .MaximumLength(100)
            .WithMessage("Player name cannot exceed 100 characters");
    }
}

/// <summary>
/// Handles <see cref="SubmitRuleDisputeCommand"/>.
/// Orchestrates the Arbitro AI pipeline:
///   1. Load LiveGameSession from the in-memory repository.
///   2. Verify session is InProgress.
///   3. Build an arbitration prompt (Italian, structured output).
///   4. Call <see cref="ILlmService"/> with the prompt.
///   5. Parse VERDETTO / REGOLA / NOTA sections from the response.
///   6. Persist <see cref="RuleDisputeEntry"/> to the session and DB.
///   7. Publish <see cref="DisputeResolvedEvent"/> for SignalR broadcast.
/// </summary>
internal sealed class SubmitRuleDisputeCommandHandler
    : IRequestHandler<SubmitRuleDisputeCommand, RuleDisputeResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly char[] RuleRefSeparators = { ';', '\n', '|' };

    private readonly ILiveSessionRepository _sessionRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILlmService _llmService;
    private readonly IPublisher _publisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SubmitRuleDisputeCommandHandler> _logger;

    public SubmitRuleDisputeCommandHandler(
        ILiveSessionRepository sessionRepository,
        MeepleAiDbContext dbContext,
        ILlmService llmService,
        IPublisher publisher,
        TimeProvider timeProvider,
        ILogger<SubmitRuleDisputeCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RuleDisputeResponse> Handle(
        SubmitRuleDisputeCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Load LiveGameSession — throws NotFoundException if missing
        var session = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", request.SessionId.ToString());

        // 2. Authorization: caller must be the host or an active participant
        var isHost = session.CreatedByUserId == request.CallerUserId;
        var isParticipant = session.Players.Any(p => p.UserId == request.CallerUserId && p.IsActive);
        if (!isHost && !isParticipant)
        {
            throw new ForbiddenException(
                "You must be a participant or host of this session to submit a rule dispute.");
        }

        // 3. Verify session is InProgress
        if (session.Status != LiveSessionStatus.InProgress)
        {
            throw new ConflictException(
                $"Cannot submit a rule dispute for session in {session.Status} status. " +
                "Session must be InProgress.");
        }

        _logger.LogInformation(
            "SubmitRuleDisputeCommand: SessionId={SessionId}, Player={Player}, Description={Description}",
            request.SessionId, request.RaisedByPlayerName, request.Description);

        // 3. Build the arbitration prompt
        var (systemPrompt, userPrompt) = BuildArbitrationPrompt(session, request);

        // 4. Query the LLM
        var llmResult = await _llmService
            .GenerateCompletionAsync(systemPrompt, userPrompt, RequestSource.Manual, cancellationToken)
            .ConfigureAwait(false);

        if (!llmResult.Success)
        {
            _logger.LogError(
                "LLM arbitration failed for SessionId={SessionId}: {Error}",
                request.SessionId, llmResult.ErrorMessage);

            throw new InvalidOperationException(
                $"AI arbitration failed: {llmResult.ErrorMessage}");
        }

        _logger.LogDebug(
            "Arbitro LLM response for SessionId={SessionId}: {Response}",
            request.SessionId, llmResult.Response);

        // 5. Parse the structured response
        var (verdict, ruleReferences, note) = ParseArbitrationResponse(llmResult.Response);

        // 6. Create and attach the RuleDisputeEntry
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var entry = new RuleDisputeEntry(
            id: Guid.NewGuid(),
            description: request.Description,
            verdict: verdict,
            ruleReferences: ruleReferences,
            raisedByPlayerName: request.RaisedByPlayerName,
            timestamp: now);

        session.AddDispute(entry);

        // 7. Persist disputes JSONB on the DB entity (in-memory repo holds the live state;
        //    the DB row is updated so sessions can be restored after restart).
        await PersistDisputesToDbAsync(session, cancellationToken).ConfigureAwait(false);

        // 8. Update the in-memory repository
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        // 9. Publish DisputeResolvedEvent for SignalR broadcast
        await _publisher
            .Publish(new DisputeResolvedEvent(session.Id, entry), cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Rule dispute resolved: SessionId={SessionId}, DisputeId={DisputeId}, Verdict={Verdict}",
            session.Id, entry.Id, verdict);

        return new RuleDisputeResponse(
            Id: entry.Id,
            Verdict: entry.Verdict,
            RuleReferences: entry.RuleReferences,
            Note: note);
    }

    // ── Prompt Building ────────────────────────────────────────────────────────

    private static (string SystemPrompt, string UserPrompt) BuildArbitrationPrompt(
        LiveGameSession session,
        SubmitRuleDisputeCommand request)
    {
        var systemPrompt =
            "Sei l'ARBITRO ufficiale di una partita da tavolo. " +
            "Il tuo compito è emettere verdetti chiari, precisi e imparziali sulle dispute di regole. " +
            "Rispondi SEMPRE in italiano nel formato strutturato richiesto. " +
            "Basa i tuoi verdetti esclusivamente sul regolamento del gioco.";

        var playerNames = session.Players.Count > 0
            ? string.Join(", ", session.Players.Where(p => p.IsActive).Select(p => p.DisplayName))
            : "N/D";

        var previousDisputesSummary = BuildPreviousDisputesSummary(session.Disputes);

        var userPromptBuilder = new StringBuilder();
        userPromptBuilder.AppendLine("MODALITÀ ARBITRO. Emetti un VERDETTO chiaro su questa disputa.");
        userPromptBuilder.AppendLine();
        userPromptBuilder.Append("Disputa: \"").Append(request.Description).AppendLine("\"");
        userPromptBuilder
            .Append("Contesto: Sessione \"").Append(session.GameName)
            .Append("\" con giocatori: ").Append(playerNames)
            .Append(". Turno: ").Append(session.CurrentTurnIndex).AppendLine(".");

        if (!string.IsNullOrEmpty(previousDisputesSummary))
        {
            userPromptBuilder.Append("Verdetti precedenti: ").AppendLine(previousDisputesSummary);
        }

        userPromptBuilder.AppendLine();
        userPromptBuilder.AppendLine("Rispondi in formato strutturato:");
        userPromptBuilder.AppendLine("VERDETTO: [Chi ha ragione e perché — 1-2 frasi]");
        userPromptBuilder.AppendLine("REGOLA: [Citazione esatta dal regolamento con pagina/sezione]");
        userPromptBuilder.AppendLine("NOTA: [Se ambigua, spiega perché e suggerisci come gestirla]");

        return (systemPrompt, userPromptBuilder.ToString());
    }

    private static string BuildPreviousDisputesSummary(IReadOnlyList<RuleDisputeEntry> disputes)
    {
        if (disputes.Count == 0)
            return string.Empty;

        // Only include the last 3 disputes to keep the prompt concise
        var recent = disputes.TakeLast(3).ToList();
        var parts = recent.Select((d, i) =>
            $"{i + 1}. \"{d.Description}\" → {d.Verdict}");

        return string.Join("; ", parts);
    }

    // ── Response Parsing ───────────────────────────────────────────────────────

    internal static (string Verdict, List<string> RuleReferences, string? Note) ParseArbitrationResponse(
        string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return ("Nessun verdetto disponibile.", new List<string>(), null);

        var lines = response
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .ToList();

        var verdict = ExtractSection(lines, "VERDETTO:");
        var regola = ExtractSection(lines, "REGOLA:");
        var nota = ExtractSection(lines, "NOTA:");

        // If parsing fails entirely, use the full response as the verdict
        if (string.IsNullOrWhiteSpace(verdict))
        {
            verdict = response.Trim();
        }

        // Parse REGOLA into a list of rule references (split by semicolons or newlines)
        var ruleReferences = ParseRuleReferences(regola);

        return (verdict, ruleReferences, string.IsNullOrWhiteSpace(nota) ? null : nota);
    }

    private static string ExtractSection(List<string> lines, string prefix)
    {
        var parts = new List<string>();
        var capturing = false;

        foreach (var line in lines)
        {
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                capturing = true;
                var remainder = line[prefix.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(remainder))
                    parts.Add(remainder);
                continue;
            }

            if (capturing)
            {
                // Stop at the next recognised section header
                if (line.StartsWith("VERDETTO:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("REGOLA:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("NOTA:", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                parts.Add(line);
            }
        }

        return string.Join(" ", parts).Trim();
    }

    private static List<string> ParseRuleReferences(string regola)
    {
        if (string.IsNullOrWhiteSpace(regola))
            return new List<string>();

        // Split by common separators to produce multiple references
        var references = regola
            .Split(RuleRefSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim())
            .Where(r => r.Length > 0)
            .ToList();

        return references.Count > 0 ? references : new List<string> { regola.Trim() };
    }

    // ── DB Persistence ─────────────────────────────────────────────────────────

    private async Task PersistDisputesToDbAsync(
        LiveGameSession session,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.LiveGameSessions
            .FirstOrDefaultAsync(e => e.Id == session.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            _logger.LogWarning(
                "LiveGameSessionEntity not found in DB for SessionId={SessionId}. " +
                "DisputesJson will not be persisted to database.",
                session.Id);
            return;
        }

        entity.DisputesJson = JsonSerializer.Serialize(session.Disputes, JsonOptions);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
