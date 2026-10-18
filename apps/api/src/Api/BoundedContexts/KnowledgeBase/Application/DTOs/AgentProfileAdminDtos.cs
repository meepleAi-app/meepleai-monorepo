using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Enums;

#pragma warning disable MA0048 // File name must match type name - the DTOs of the admin agent page
namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Il profilo dell'agente per la pagina admin «Agente» (ADR-095 D8, #4169 6c): la versione pubblicata,
/// la bozza se c'è e la storia delle versioni, dalla più recente.
/// </summary>
internal sealed record AgentProfileAdminDto(
    AgentProfileVersionDto Published,
    AgentProfileVersionDto? Draft,
    IReadOnlyList<AgentProfileVersionSummaryDto> Versions)
{
    public static AgentProfileAdminDto From(AgentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new AgentProfileAdminDto(
            AgentProfileVersionDto.From(profile.Published),
            profile.Draft is { } draft ? AgentProfileVersionDto.From(draft) : null,
            profile.Versions
                .OrderByDescending(v => v.VersionNumber)
                .Select(AgentProfileVersionSummaryDto.From)
                .ToList());
    }
}

/// <summary>Una versione con il suo contenuto.</summary>
internal sealed record AgentProfileVersionDto(
    Guid Id,
    int VersionNumber,
    AgentProfileVersionStatus Status,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    DateTime? ArchivedAt,
    AgentProfileContentDto Content)
{
    public static AgentProfileVersionDto From(AgentProfileVersion v) =>
        new(v.Id, v.VersionNumber, v.Status, v.CreatedAt, v.PublishedAt, v.ArchivedAt, AgentProfileContentDto.From(v.Content));
}

/// <summary>Una riga della storia: numero, stato e date, senza il contenuto.</summary>
internal sealed record AgentProfileVersionSummaryDto(
    Guid Id,
    int VersionNumber,
    AgentProfileVersionStatus Status,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    DateTime? ArchivedAt)
{
    public static AgentProfileVersionSummaryDto From(AgentProfileVersion v) =>
        new(v.Id, v.VersionNumber, v.Status, v.CreatedAt, v.PublishedAt, v.ArchivedAt);
}

/// <summary>
/// Il contenuto di una versione, sezione per sezione (ADR-095 D1). La pagina modifica Identità, Modello,
/// Generazione e Prompt; Recupero e Fonti restano in sola lettura fino alla fetta 7 (#4170), e così
/// l'assistente in partita, il cui pilota aspetta #4270.
/// </summary>
internal sealed record AgentProfileContentDto(
    string Name,
    string Description,
    string Persona,
    string PrimaryModelId,
    string? FallbackModelId,
    decimal Temperature,
    int MaxResponseTokens,
    CitationStyle CitationStyle,
    IReadOnlyDictionary<string, string> SystemPrompts,
    string FallbackLanguage,
    int CandidatePoolSize,
    int FinalTopK,
    decimal MinScore,
    decimal VectorWeight,
    decimal KeywordWeight,
    bool RerankerEnabled,
    IReadOnlyList<DocumentCategory> AllowedCategories,
    AgentProfileSessionAssistantDto SessionAssistant)
{
    public static AgentProfileContentDto From(AgentProfileContent c) =>
        new(
            c.Name,
            c.Description,
            c.Persona,
            c.PrimaryModelId,
            c.FallbackModelId,
            c.Temperature,
            c.MaxResponseTokens,
            c.CitationStyle,
            c.SystemPrompts,
            c.FallbackLanguage,
            c.CandidatePoolSize,
            c.FinalTopK,
            c.MinScore,
            c.VectorWeight,
            c.KeywordWeight,
            c.RerankerEnabled,
            c.AllowedCategories,
            AgentProfileSessionAssistantDto.From(c.SessionAssistant));
}

/// <summary>La sezione «Assistente in partita», in sola lettura: se è accesa, su quale modello, in che lingue.</summary>
internal sealed record AgentProfileSessionAssistantDto(
    bool Enabled,
    bool UseReserveModel,
    IReadOnlyList<string> PromptLanguages,
    decimal Temperature,
    int MaxResponseTokens)
{
    public static AgentProfileSessionAssistantDto From(SessionAssistantSettings s) =>
        new(s.Enabled, s.UseReserveModel, s.SystemPrompts.Keys.Order(StringComparer.Ordinal).ToList(), s.Temperature, s.MaxResponseTokens);
}
