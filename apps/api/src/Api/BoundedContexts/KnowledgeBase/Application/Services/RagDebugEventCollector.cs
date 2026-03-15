using Api.Models;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Collects debug events emitted during RAG prompt assembly.
/// Created per-request by calling handlers to gather enhancement diagnostics.
/// </summary>
internal interface IRagDebugEventCollector
{
    void Add(StreamingEventType type, object? data);
}

internal sealed class RagDebugEventCollector : IRagDebugEventCollector
{
    private readonly List<(StreamingEventType Type, object? Data)> _events = [];

    public void Add(StreamingEventType type, object? data)
        => _events.Add((type, data));

    public IReadOnlyList<(StreamingEventType Type, object? Data)> Events => _events;
}
