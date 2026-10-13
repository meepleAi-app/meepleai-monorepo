using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Forma jsonb di <see cref="AgentProfileContent"/> nella colonna
/// <c>knowledge_base.agent_profile_versions.content</c> (#4167). È un contratto persistito — la
/// migration di seed la scrive a mano — quindi passa da un documento esplicito e non dal value
/// object: rinominare una proprietà di dominio non deve cambiare cosa c'è scritto nel database.
/// Nomi in camelCase, enum come stringhe.
/// </summary>
internal static class AgentProfileContentJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(AgentProfileContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var document = new Document(
            content.Name,
            content.Description,
            content.Persona,
            content.PrimaryModelId,
            content.FallbackModelId,
            content.Temperature,
            content.MaxResponseTokens,
            content.CitationStyle,
            content.SystemPrompts.ToDictionary(StringComparer.Ordinal),
            content.FallbackLanguage,
            content.CandidatePoolSize,
            content.FinalTopK,
            content.MinScore,
            content.VectorWeight,
            content.KeywordWeight,
            content.RerankerEnabled,
            content.AllowedCategories.ToArray(),
            ToDocument(content.SessionAssistant));
        return JsonSerializer.Serialize(document, Options);
    }

    public static AgentProfileContent Deserialize(string json)
    {
        var d = JsonSerializer.Deserialize<Document>(json, Options)
            ?? throw new JsonException("Agent profile content is null.");
        return AgentProfileContent.Restore(
            d.Name,
            d.Description,
            d.Persona,
            d.PrimaryModelId,
            d.FallbackModelId,
            d.Temperature,
            d.MaxResponseTokens,
            d.CitationStyle,
            d.SystemPrompts,
            d.FallbackLanguage,
            d.CandidatePoolSize,
            d.FinalTopK,
            d.MinScore,
            d.VectorWeight,
            d.KeywordWeight,
            d.RerankerEnabled,
            d.AllowedCategories,
            ToSettings(d.SessionAssistant));
    }

    private static SessionAssistantDocument? ToDocument(SessionAssistantSettings assistant) =>
        assistant.Equals(SessionAssistantSettings.Disabled)
            ? null
            : new SessionAssistantDocument(
                assistant.Enabled,
                assistant.SystemPrompts.ToDictionary(StringComparer.Ordinal),
                assistant.Temperature,
                assistant.MaxResponseTokens,
                assistant.MaxToolIterations,
                assistant.TurnTimeoutSeconds,
                assistant.UseReserveModel);

    private static SessionAssistantSettings ToSettings(SessionAssistantDocument? d) =>
        d is null
            ? SessionAssistantSettings.Disabled
            : SessionAssistantSettings.Restore(
                d.Enabled, d.SystemPrompts, d.Temperature, d.MaxResponseTokens, d.MaxToolIterations, d.TurnTimeoutSeconds,
                d.UseReserveModel);

    /// <summary>I numeri sono nullable: un campo assente vale il default misurato, non zero.</summary>
    private sealed record SessionAssistantDocument(
        bool Enabled,
        Dictionary<string, string>? SystemPrompts,
        decimal? Temperature = null,
        int? MaxResponseTokens = null,
        int? MaxToolIterations = null,
        int? TurnTimeoutSeconds = null,
        bool UseReserveModel = false);

    private sealed record Document(
        string Name,
        string Description,
        string Persona,
        string PrimaryModelId,
        string? FallbackModelId,
        decimal Temperature,
        int MaxResponseTokens,
        CitationStyle CitationStyle,
        Dictionary<string, string> SystemPrompts,
        string FallbackLanguage,
        int CandidatePoolSize,
        int FinalTopK,
        decimal MinScore,
        decimal VectorWeight,
        decimal KeywordWeight,
        bool RerankerEnabled,
        DocumentCategory[] AllowedCategories,
        // Opzionale e in coda: le versioni scritte prima della fetta A2 non hanno la chiave, e una sezione
        // spenta non la scrive, così il jsonb delle versioni esistenti non cambia.
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SessionAssistantDocument? SessionAssistant = null);
}
