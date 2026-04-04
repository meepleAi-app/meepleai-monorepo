using System.Text;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Handles <see cref="SessionSaveRequestedEvent"/> by generating an AI-powered summary
/// of the last session messages. The summary is then dispatched via
/// <see cref="AgentSummaryGeneratedEvent"/> so it can be stored on the PauseSnapshot.
///
/// This handler is fire-and-forget: failure is logged but does not crash the flow.
/// Session save/pause already completed before this runs.
///
/// Game Night Improvvisata — E4: Save/Resume — async agent recap.
/// </summary>
internal sealed class GenerateAgentSummaryHandler : INotificationHandler<SessionSaveRequestedEvent>
{
    private const int MaxMessages = 50;

    private readonly IAgentDefinitionRepository _agentRepo;
    private readonly ILlmService _llmService;
    private readonly IPublisher _publisher;
    private readonly ILogger<GenerateAgentSummaryHandler> _logger;

    public GenerateAgentSummaryHandler(
        IAgentDefinitionRepository agentRepo,
        ILlmService llmService,
        IPublisher publisher,
        ILogger<GenerateAgentSummaryHandler> logger)
    {
        _agentRepo = agentRepo ?? throw new ArgumentNullException(nameof(agentRepo));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SessionSaveRequestedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            // 1. Load the agent definition (to get the agent's name/context for the prompt)
            var agentDefinition = await _agentRepo
                .GetByIdAsync(notification.AgentDefinitionId, cancellationToken)
                .ConfigureAwait(false);

            // Use a generic agent name if the definition is not found
            var agentName = agentDefinition?.Name ?? "AI Assistant";

            // 2. Take last N messages (cap at MaxMessages)
            var cappedMessages = notification.LastMessages
                .TakeLast(MaxMessages)
                .ToList();

            if (cappedMessages.Count == 0)
            {
                _logger.LogDebug(
                    "GenerateAgentSummary: No messages for SessionId={SessionId}. Skipping summary.",
                    notification.LiveGameSessionId);
                return;
            }

            // 3. Build summary prompt
            var (systemPrompt, userPrompt) = BuildSummaryPrompt(agentName, cappedMessages);

            // 4. Call LLM to generate summary
            var result = await _llmService
                .GenerateCompletionAsync(systemPrompt, userPrompt, RequestSource.Manual, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                _logger.LogError(
                    "GenerateAgentSummary: LLM call failed for SessionId={SessionId}, SnapshotId={SnapshotId}. Error: {Error}",
                    notification.LiveGameSessionId, notification.PauseSnapshotId, result.ErrorMessage);
                return;
            }

            var summary = result.Response?.Trim();
            if (string.IsNullOrWhiteSpace(summary))
            {
                _logger.LogWarning(
                    "GenerateAgentSummary: LLM returned empty summary for SessionId={SessionId}.",
                    notification.LiveGameSessionId);
                return;
            }

            _logger.LogInformation(
                "GenerateAgentSummary: Summary generated for SnapshotId={SnapshotId} ({Length} chars).",
                notification.PauseSnapshotId, summary.Length);

            // 5. Publish AgentSummaryGeneratedEvent so UpdateSnapshotSummaryHandler can persist it
            await _publisher
                .Publish(new AgentSummaryGeneratedEvent(notification.PauseSnapshotId, summary), cancellationToken)
                .ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            // Summary is optional — log but do NOT re-throw.
            // The pause/snapshot has already been committed successfully.
            _logger.LogError(
                ex,
                "GenerateAgentSummary: Unexpected failure for SessionId={SessionId}, SnapshotId={SnapshotId}. " +
                "Session is paused correctly; summary will be unavailable.",
                notification.LiveGameSessionId,
                notification.PauseSnapshotId);
        }
#pragma warning restore CA1031
    }

    // ── Prompt Building ──────────────────────────────────────────────────────

    private static (string SystemPrompt, string UserPrompt) BuildSummaryPrompt(
        string agentName,
        IReadOnlyList<string> messages)
    {
        var systemPrompt =
            $"Sei {agentName}, l'assistente AI per una sessione di gioco da tavolo. " +
            "Il tuo compito è creare un breve riassunto di contesto per consentire ai giocatori " +
            "di riprendere facilmente la sessione dopo una pausa. " +
            "Rispondi in italiano in 3-5 frasi concise. " +
            "Includi: a) a che punto era la partita, b) eventuali decisioni importanti, " +
            "c) l'atmosfera generale della sessione.";

        var userPromptBuilder = new StringBuilder();
        userPromptBuilder.AppendLine("Ecco gli ultimi messaggi della sessione di gioco:");
        userPromptBuilder.AppendLine();

        foreach (var message in messages)
        {
            userPromptBuilder.AppendLine(message);
        }

        userPromptBuilder.AppendLine();
        userPromptBuilder.AppendLine(
            "Scrivi un breve riassunto (3-5 frasi) che aiuti i giocatori a ricordare " +
            "dove si erano fermati. Inizia con: \"Quando avete messo in pausa la partita...\"");

        return (systemPrompt, userPromptBuilder.ToString());
    }
}
