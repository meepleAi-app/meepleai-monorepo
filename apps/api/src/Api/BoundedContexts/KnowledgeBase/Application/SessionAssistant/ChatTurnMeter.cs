using System.Collections.Immutable;
using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// I conti di un turno dell'assistente: quante chiamate al modello, quanti token, quanto costo. Un turno
/// con strumenti fa più chiamate; il costo del turno è la loro somma (ADR-097).
/// </summary>
internal sealed class ChatTurnMeter
{
    private readonly Lock _lock = new();

    // Immutabile: la proprietà ToolCalls restituisce l'istanza senza copiarla (S2365) e resta sicura
    // fra thread, perché ogni RecordToolCall sostituisce il riferimento invece di modificare la lista.
    private ImmutableList<string> _toolCalls = ImmutableList<string>.Empty;

    public int RoundTrips { get; private set; }
    public int PromptTokens { get; private set; }
    public int CompletionTokens { get; private set; }
    public decimal CostUsd { get; private set; }

    /// <summary>Chiamate a strumenti con argomenti che non si legano ai parametri (spec §7.1, «argomenti non validi»).</summary>
    public int InvalidArguments { get; private set; }

    /// <summary>Gli strumenti chiamati nel turno, in ordine; restano anche se il ciclo si ferma per un errore.</summary>
    public IReadOnlyList<string> ToolCalls => Volatile.Read(ref _toolCalls);

    internal void Record(LlmCompletionResult result)
    {
        lock (_lock)
        {
            RoundTrips++;
            PromptTokens += result.Usage.PromptTokens;
            CompletionTokens += result.Usage.CompletionTokens;
            CostUsd += result.Cost.TotalCost;
        }
    }

    internal void RecordToolCall(string name)
    {
        lock (_lock)
        {
            Volatile.Write(ref _toolCalls, _toolCalls.Add(name));
        }
    }

    internal void RecordInvalidArguments()
    {
        lock (_lock)
        {
            InvalidArguments++;
        }
    }
}
