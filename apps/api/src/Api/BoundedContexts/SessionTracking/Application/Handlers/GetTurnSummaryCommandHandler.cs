using System.Globalization;
using System.Text;
using System.Text.Json;
using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

internal class GetTurnSummaryCommandHandler : IRequestHandler<GetTurnSummaryCommand, TurnSummaryResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionEventRepository _sessionEventRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILlmService _llmService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private const string SystemPrompt =
        "You are a board game session assistant. Your job is to summarize what happened " +
        "during a game session based on the timeline of events.\n\n" +
        "Guidelines:\n" +
        "- Write a concise, engaging summary in 2-4 paragraphs.\n" +
        "- Highlight key moments: notable dice rolls, score changes, card draws, and player notes.\n" +
        "- Use natural language, not bullet points.\n" +
        "- Mention participants by name when relevant.\n" +
        "- Keep the tone friendly and informative.\n" +
        "- If phase/turn information is available, organize the summary chronologically.\n" +
        "- Do not make up events that are not in the data.";

    public GetTurnSummaryCommandHandler(
        ISessionRepository sessionRepository,
        ISessionEventRepository sessionEventRepository,
        IUnitOfWork unitOfWork,
        ILlmService llmService)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _sessionEventRepository = sessionEventRepository ?? throw new ArgumentNullException(nameof(sessionEventRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
    }

    public async Task<TurnSummaryResult> Handle(GetTurnSummaryCommand request, CancellationToken cancellationToken)
    {
        _ = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(string.Format(CultureInfo.InvariantCulture, "Session {0} not found", request.SessionId));

        var events = await FetchEventsAsync(request, cancellationToken).ConfigureAwait(false);

        var eventList = events.ToList();
        if (eventList.Count == 0)
        {
            throw new ConflictException("No events found for the specified range. Cannot generate a summary.");
        }

        var groupedEvents = GroupEventsByType(eventList);
        var userPrompt = BuildUserPrompt(groupedEvents, eventList.Count);

        var llmResult = await _llmService.GenerateCompletionAsync(
            SystemPrompt,
            userPrompt,
            RequestSource.AgentTask,
            cancellationToken).ConfigureAwait(false);

        if (!llmResult.Success)
        {
            throw new ConflictException(string.Format(CultureInfo.InvariantCulture, "AI summary generation failed: {0}", llmResult.ErrorMessage));
        }

        var summaryText = llmResult.Response;

        var summaryPayload = JsonSerializer.Serialize(new
        {
            summary = summaryText,
            eventsAnalyzed = eventList.Count,
            fromTimestamp = eventList.Min(e => e.Timestamp),
            toTimestamp = eventList.Max(e => e.Timestamp),
            eventTypeBreakdown = groupedEvents.ToDictionary(
                g => g.Key, g => g.Count(), StringComparer.Ordinal)
        }, JsonOptions);

        var summaryEvent = SessionEvent.Create(
            request.SessionId,
            "turn_summary",
            summaryPayload,
            request.RequesterId,
            "ai");

        await _sessionEventRepository.AddAsync(summaryEvent, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TurnSummaryResult(
            summaryEvent.Id,
            summaryText,
            eventList.Count,
            summaryEvent.Timestamp);
    }

    private async Task<IEnumerable<SessionEvent>> FetchEventsAsync(
        GetTurnSummaryCommand request, CancellationToken cancellationToken)
    {
        if (request.LastNEvents.HasValue)
        {
            return await _sessionEventRepository.GetBySessionIdAsync(
                request.SessionId, eventType: null, limit: request.LastNEvents.Value,
                offset: 0, ct: cancellationToken).ConfigureAwait(false);
        }

        var allEvents = await _sessionEventRepository.GetBySessionIdAsync(
            request.SessionId, eventType: null, limit: 500,
            offset: 0, ct: cancellationToken).ConfigureAwait(false);

        var eventList = allEvents.ToList();

        if (!request.FromPhase.HasValue && !request.ToPhase.HasValue)
            return eventList;

        return eventList.Where(e => IsEventInPhaseRange(e, request.FromPhase, request.ToPhase));
    }

    private static bool IsEventInPhaseRange(SessionEvent evt, int? fromPhase, int? toPhase)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(evt.Payload) || string.Equals(evt.Payload, "{}", StringComparison.Ordinal))
                return true;

            using var doc = JsonDocument.Parse(evt.Payload);
            if (doc.RootElement.TryGetProperty("phase", out var phaseElement) &&
                phaseElement.TryGetInt32(out var phase))
            {
                if (fromPhase.HasValue && phase < fromPhase.Value) return false;
                if (toPhase.HasValue && phase > toPhase.Value) return false;
            }
            return true;
        }
        catch (JsonException) { return true; }
    }

    private static ILookup<string, SessionEvent> GroupEventsByType(List<SessionEvent> events)
        => events.ToLookup(e => e.EventType, StringComparer.Ordinal);

    private static string BuildUserPrompt(ILookup<string, SessionEvent> groupedEvents, int totalCount)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"Please summarize the following {totalCount} session events:");
        sb.AppendLine();
        sb.AppendLine();

        foreach (var group in groupedEvents.OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            sb.Append(CultureInfo.InvariantCulture, $"## {FormatEventType(group.Key)} ({group.Count()} events)");
            sb.AppendLine();
            foreach (var evt in group.OrderBy(e => e.Timestamp))
            {
                sb.Append(CultureInfo.InvariantCulture, $"- [{evt.Timestamp:HH:mm:ss}] {evt.Payload}");
                sb.AppendLine();
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string FormatEventType(string eventType) => eventType switch
    {
        "dice_roll" => "Dice Rolls",
        "card_draw" => "Card Draws",
        "note_added" => "Notes",
        "score_update" => "Score Updates",
        "phase_change" => "Phase Changes",
        "player_joined" => "Player Joins",
        "player_left" => "Player Departures",
        "timer_action" => "Timer Actions",
        "turn_summary" => "Previous Summaries",
        _ => eventType.Replace('_', ' ').ToUpperInvariant()
    };
}
