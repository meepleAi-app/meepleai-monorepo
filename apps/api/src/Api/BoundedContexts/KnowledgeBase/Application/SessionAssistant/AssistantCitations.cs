using System.Text.Json;
using Api.Models;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Le citazioni di una risposta dell'assistente nel messaggio salvato (#4411). Si salva la forma già filtrata
/// per tier (<see cref="CitationDto"/>, dopo <c>FinalizeAsync</c>): un ricaricamento mostra esattamente ciò che
/// il turno ha mostrato, e niente di letterale per il tier Protected.
/// </summary>
internal static class AssistantCitations
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Write(IReadOnlyList<CitationDto> citations) => JsonSerializer.Serialize(citations, Json);

    /// <summary>Null per un messaggio senza citazioni, e per un JSON che non si legge: il pannello si carica lo stesso.</summary>
    public static IReadOnlyList<CitationDto>? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<CitationDto>>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
