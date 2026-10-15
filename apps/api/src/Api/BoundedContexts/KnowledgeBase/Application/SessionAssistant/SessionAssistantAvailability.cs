using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;

#pragma warning disable MA0048 // File name must match type name - the availability rule and its result
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// L'esito della regola di abilitazione: la versione pubblicata del profilo, la lingua del turno e il prompt
/// di sistema. Se l'assistente è spento, <c>Code</c> dice perché e <c>Problem</c> lo spiega.
/// </summary>
/// <remarks>
/// La lingua del turno è quella del prompt usato (#4397): la chiesta se la sezione ha un prompt per lei,
/// altrimenti quella di ripiego del profilo. Il contesto statico e i testi che il turno scrive la seguono,
/// così il modello non riceve un prompt in una lingua e il contesto in un'altra.
/// </remarks>
internal sealed record AssistantAvailabilityResult(
    PublishedAgentProfile Profile, string Language, string? SystemPrompt, string? Code, string? Problem)
{
    public bool Enabled => Code is null;
}

/// <summary>
/// Se l'assistente risponde, secondo la versione pubblicata del profilo (spec 2026-10-11 §6.2, passo 1).
/// La usano il turno, che rifiuta con 409, e la lettura del pannello (#4372), che lo dice prima della
/// prima domanda: con due copie della regola il pannello potrebbe dire «disponibile» a un turno che
/// risponde 409.
/// </summary>
/// <remarks>Lo stato della sessione è un controllo a parte: dipende dalla sessione, non dal profilo.</remarks>
internal sealed class SessionAssistantAvailability
{
    public const string DisabledCode = "assistant_disabled";

    private readonly IPublishedAgentProfileReader _profiles;
    private readonly IToolCallingModelCatalog _catalog;

    public SessionAssistantAvailability(IPublishedAgentProfileReader profiles, IToolCallingModelCatalog catalog)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <summary>
    /// Valuta la regola per la lingua chiesta. Senza lingua vale quella di ripiego del profilo; senza un
    /// prompt nella lingua chiesta si usa quello di ripiego.
    /// </summary>
    public async Task<AssistantAvailabilityResult> CheckAsync(string? language, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetAsync(cancellationToken).ConfigureAwait(false);
        var content = profile.Content;
        var assistant = content.SessionAssistant;
        var resolved = string.IsNullOrWhiteSpace(language) ? content.FallbackLanguage : language.Trim();
        var prompt = assistant.PromptFor(resolved);
        if (prompt is null && assistant.PromptFor(content.FallbackLanguage) is { } fallbackPrompt)
        {
            prompt = fallbackPrompt;
            resolved = content.FallbackLanguage;
        }

        if (!assistant.Enabled || prompt is null)
        {
            return new AssistantAvailabilityResult(
                profile, resolved, prompt, DisabledCode, "The session assistant is not enabled in the published agent profile.");
        }

        // La pubblicazione applica la stessa regola, ma una migrazione di dati la scavalca e il catalogo
        // può cambiare dopo: si ricontrolla a ogni lettura.
        var toolCallingModels = await _catalog.GetToolCallingModelIdsAsync(cancellationToken).ConfigureAwait(false);
        if (content.SessionAssistantModelProblem(toolCallingModels) is { } problem)
        {
            return new AssistantAvailabilityResult(profile, resolved, prompt, AgentProfile.AssistantModelWithoutToolsCode, problem);
        }

        return new AssistantAvailabilityResult(profile, resolved, prompt, Code: null, Problem: null);
    }
}
