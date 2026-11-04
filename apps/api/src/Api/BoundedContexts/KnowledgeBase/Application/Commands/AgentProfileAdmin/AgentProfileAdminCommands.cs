using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - the commands of the admin agent page
namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentProfileAdmin;

/// <summary>
/// Salva la bozza del profilo dalla pagina «Agente» (#4169 6c), creandola se non c'è. Porta solo le
/// sezioni modificabili — Identità, Modello, Generazione, Prompt — e non ha campi per le altre:
/// Recupero, Fonti e l'assistente in partita restano quelli della bozza, o della versione pubblicata
/// se la bozza non c'è ancora. Così una pagina che non li mostra non può cambiarli per sbaglio.
/// Temperatura, lunghezza e stile delle citazioni sono nullable solo per distinguere «omesso» da
/// «zero»: il gestore rifiuta l'omissione.
/// </summary>
internal sealed record SaveAgentProfileDraftCommand(
    string Name,
    string? Description,
    string? Persona,
    string PrimaryModelId,
    string? FallbackModelId,
    decimal? Temperature,
    int? MaxResponseTokens,
    CitationStyle? CitationStyle,
    IReadOnlyDictionary<string, string> SystemPrompts,
    string FallbackLanguage) : ICommand<AgentProfileAdminDto>;

/// <summary>Pubblica la bozza: i modelli devono avere un prezzo nel catalogo (ADR-095 D5).</summary>
internal sealed record PublishAgentProfileDraftCommand : ICommand<AgentProfileAdminDto>;

/// <summary>Ripubblica una versione archiviata come versione nuova (ADR-095 D2).</summary>
internal sealed record RollbackAgentProfileCommand(int VersionNumber) : ICommand<AgentProfileAdminDto>;
