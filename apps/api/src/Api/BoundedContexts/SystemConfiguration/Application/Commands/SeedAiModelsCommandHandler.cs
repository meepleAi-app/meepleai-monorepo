using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Handler for SeedAiModelsCommand.
/// Populates default AI model configurations (OpenRouter, DeepSeek, Ollama).
/// </summary>
/// <remarks>
/// <para>
/// #4164 (ADR-095): il catalogo è l'unico listino, quindi il seed deve poterlo aggiornare anche
/// quando è già popolato — prima girava solo su una tabella vuota, e un modello aggiunto dopo il
/// primo avvio non arrivava mai in staging. Ora, per ogni modello del seed:
/// </para>
/// <list type="bullet">
/// <item><description>se il catalogo non lo ha, lo inserisce;</description></item>
/// <item><description>se lo ha, ne aggiorna <b>solo il prezzo</b>, e solo se la verifica del seed
/// è più recente di quella registrata (<see cref="AiModelConfiguration.ApplyVerifiedPricing"/>).
/// Abilitazione, priorità e modello primario restano quelli scelti dall'admin.</description></item>
/// </list>
/// <para>
/// I modelli nuovi entrano disattivati: l'ordine di fallback dei provider si costruisce dalle
/// righe attive, quindi una riga attiva in più cambierebbe l'instradamento. Il prezzo vale anche
/// per un modello disattivato.
/// </para>
/// <para>
/// #4179: l'unica eccezione a «la scelta resta all'admin» sono i modelli dismessi dal provider
/// (<see cref="RetiredModels"/>). Non sono una scelta: una chiamata a un modello che il provider
/// non elenca più non può riuscire. Il seed li disattiva, ne toglie il ruolo di primario e lo passa
/// al sostituto. La riga resta, con il suo prezzo, per le righe del registro costi che la citano.
/// </para>
/// </remarks>
internal sealed class SeedAiModelsCommandHandler : ICommandHandler<SeedAiModelsCommand>
{
    /// <summary>Giorno della verifica alla fonte dei prezzi qui sotto.</summary>
    internal static readonly DateOnly PricesVerifiedOn = new(2026, 10, 10);

    internal const string OpenRouterPriceSource = "https://openrouter.ai/api/v1/models";
    internal const string DeepSeekPriceSource = "https://api-docs.deepseek.com/quick_start/pricing";

    /// <summary>
    /// I modelli del seed. Le righe gratuite non portano una data di verifica: costano zero per
    /// costruzione (modelli in casa) e non si aggiornano.
    /// </summary>
    internal static IReadOnlyList<SeedModel> Models { get; } =
    [
        // OpenRouter, prezzo del modello nel listino (i provider a valle variano). #4179: è il
        // primario di OpenRouter al posto della variante «:free», sparita dal listino.
        new("meta-llama/llama-3.3-70b-instruct", "Llama 3.3 70B Instruct", "OpenRouter",
            Priority: 2, MaxTokens: 4096,
            new ModelPricing(0.22m, 0.50m, cacheReadPricePerMillion: 0.11m,
                verifiedOn: PricesVerifiedOn, source: OpenRouterPriceSource),
            IsActive: true, IsPrimary: true),

        // OpenRouter non pubblica un prezzo della cache per questo modello.
        new("deepseek/deepseek-chat", "DeepSeek Chat", "OpenRouter",
            Priority: 4, MaxTokens: 4096,
            new ModelPricing(0.32m, 0.89m,
                verifiedOn: PricesVerifiedOn, source: OpenRouterPriceSource),
            IsActive: true, IsPrimary: false),

        new("llama3:8b", "Llama 3 8B (Local)", "Ollama",
            Priority: 5, MaxTokens: 2048, ModelPricing.Free, IsActive: false, IsPrimary: false),

        new("mistral", "Mistral (Local)", "Ollama",
            Priority: 6, MaxTokens: 2048, ModelPricing.Free, IsActive: false, IsPrimary: false),

        // API diretta di DeepSeek, nome dismesso il 2026-07-24 (changelog del 2026-04-24). Dal
        // #4179 nessun percorso lo chiede più, ma risponde ancora e punta alla linea Flash: la riga
        // resta per prezzare le chiamate già registrate. Prezzo di deepseek-flash, fascia di punta,
        // il limite superiore fra le due fasce orarie.
        new("deepseek-chat", "DeepSeek Chat (direct API)", "DeepSeek",
            Priority: 7, MaxTokens: 4096,
            new ModelPricing(0.30m, 1.20m, cacheReadPricePerMillion: 0.006m,
                verifiedOn: PricesVerifiedOn, source: DeepSeekPriceSource),
            IsActive: false, IsPrimary: false),

        // #4164 fetta 1b: è il nome che DeepSeek restituisce nei chunk dello streaming quando gli
        // si chiede «deepseek-chat», e il client in streaming prezza quel nome. Misurato il
        // 2026-10-11 sullo stack locale: senza questa riga la risposta in streaming finiva nel
        // registro come Unpriced, a $0. Stesso listino di deepseek-chat. Dal #4179 è anche il nome
        // che chiedono le chiamate instradate (strategia Balanced) e l'estrattore; resta disattivato
        // perché la scelta del modello DeepSeek viene dalla mappa della strategia, non dal catalogo.
        new("deepseek-flash", "DeepSeek Flash (direct API)", "DeepSeek",
            Priority: 8, MaxTokens: 4096,
            new ModelPricing(0.30m, 1.20m, cacheReadPricePerMillion: 0.006m,
                verifiedOn: PricesVerifiedOn, source: DeepSeekPriceSource),
            IsActive: false, IsPrimary: false)
    ];

    /// <summary>
    /// #4179: i modelli che il provider non elenca più, ciascuno con il suo sostituto. Verificati
    /// sul listino di OpenRouter il <see cref="PricesVerifiedOn"/>.
    /// </summary>
    internal static IReadOnlyList<RetiredModel> RetiredModels { get; } =
    [
        new("meta-llama/llama-3.3-70b-instruct:free", ReplacedBy: "meta-llama/llama-3.3-70b-instruct"),
        new("google/gemini-2.0-flash-exp:free", ReplacedBy: "meta-llama/llama-3.3-70b-instruct")
    ];

    private readonly IAiModelConfigurationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SeedAiModelsCommandHandler> _logger;

    public SeedAiModelsCommandHandler(
        IAiModelConfigurationRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<SeedAiModelsCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SeedAiModelsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var existing = (await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(m => m.ModelId, StringComparer.Ordinal);

        var added = new List<AiModelConfiguration>();
        // Un modello può cambiare due volte nello stesso giro (sostituto promosso a primario e
        // riprezzato): il repository attacca un'istanza nuova a ogni UpdateAsync, e una seconda
        // istanza con la stessa chiave fa lanciare EF. Si aggiorna una volta sola, alla fine.
        var changed = new HashSet<AiModelConfiguration>(ReferenceEqualityComparer.Instance);
        var repriced = 0;
        var retired = Retire(existing, changed);

        foreach (var seed in Models)
        {
            if (!existing.TryGetValue(seed.ModelId, out var model))
            {
                added.Add(AiModelConfiguration.Create(
                    modelId: seed.ModelId,
                    displayName: seed.DisplayName,
                    provider: seed.Provider,
                    priority: seed.Priority,
                    settings: new ModelSettings(seed.MaxTokens, temperature: 0.7m, seed.Pricing),
                    isActive: seed.IsActive,
                    isPrimary: seed.IsPrimary));
                continue;
            }

            if (seed.Pricing.VerifiedOn is not null && model.ApplyVerifiedPricing(seed.Pricing))
            {
                changed.Add(model);
                repriced++;
            }
        }

        foreach (var model in changed)
        {
            await _repository.UpdateAsync(model, cancellationToken).ConfigureAwait(false);
        }

        if (added.Count == 0 && repriced == 0 && retired.Count == 0)
        {
            _logger.LogInformation("AI model catalog already up to date. Skipping seed.");
            return;
        }

        if (added.Count > 0)
        {
            await _repository.AddRangeAsync(added, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "AI model catalog seeded: {Added} models added ({AddedIds}), {Repriced} prices updated to the {VerifiedOn} verification, {Retired} retired models deactivated ({RetiredIds})",
            added.Count, string.Join(", ", added.Select(m => m.ModelId)), repriced, PricesVerifiedOn,
            retired.Count, string.Join(", ", retired));
    }

    /// <summary>
    /// Disattiva i modelli dismessi ancora attivi o primari. Se uno era il primario del suo provider,
    /// il ruolo passa al sostituto, quando questo esiste, è attivo ed è dello stesso provider: un
    /// sostituto che l'admin ha disattivato resta una sua scelta.
    /// </summary>
    /// <param name="existing">Il catalogo, per id del modello.</param>
    /// <param name="changed">Raccoglie i modelli modificati, da salvare una volta sola.</param>
    /// <returns>Gli id dei modelli dismessi disattivati.</returns>
    private static List<string> Retire(
        IReadOnlyDictionary<string, AiModelConfiguration> existing,
        HashSet<AiModelConfiguration> changed)
    {
        var retired = new List<string>();
        foreach (var retiredModel in RetiredModels)
        {
            if (!existing.TryGetValue(retiredModel.ModelId, out var model) || (!model.IsActive && !model.IsPrimary))
            {
                continue;
            }

            if (model.IsPrimary
                && existing.TryGetValue(retiredModel.ReplacedBy, out var replacement)
                && string.Equals(replacement.Provider, model.Provider, StringComparison.OrdinalIgnoreCase)
                && replacement.IsActive
                && !replacement.IsPrimary)
            {
                replacement.SetPrimary(true);
                changed.Add(replacement);
            }

            model.SetActive(false);
            model.SetPrimary(false);
            changed.Add(model);
            retired.Add(model.ModelId);
        }

        return retired;
    }

    internal sealed record SeedModel(
        string ModelId,
        string DisplayName,
        string Provider,
        int Priority,
        int MaxTokens,
        ModelPricing Pricing,
        bool IsActive,
        bool IsPrimary);

    internal sealed record RetiredModel(string ModelId, string ReplacedBy);
}
