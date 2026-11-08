using System.Collections.Generic;

namespace Api.Services.LlmClients;

/// <summary>
/// #4381: un client il cui modello può ragionare prima di rispondere, e che sa chiedergli di non farlo.
/// </summary>
/// <remarks>
/// DeepSeek ha il ragionamento («thinking») attivo per default. In quella modalità i token di
/// ragionamento arrivano in un campo a parte, contano in <c>max_tokens</c> e si pagano come output,
/// e la temperatura è ignorata. Nessun consumatore legge il ragionamento: un ragionamento lungo
/// lascia una risposta pagata e vuota, con <c>finish_reason = length</c>.
/// Il gateway usa questi metodi per le risposte della pipeline (ADR-095 D3, passo 6), dove la
/// temperatura viene dal profilo pubblicato. Le altre chiamate restano sul default del modello.
/// </remarks>
internal interface ILlmReasoningSwitch
{
    /// <summary>Come <see cref="ILlmClient.GenerateCompletionAsync(string, string, string, double, int, CancellationToken)"/>, senza ragionamento.</summary>
    Task<LlmCompletionResult> GenerateCompletionWithoutReasoningAsync(
        string model,
        string systemPrompt,
        string userPrompt,
        double temperature,
        int maxTokens,
        CancellationToken ct = default);

    /// <summary>Come <see cref="ILlmClient.GenerateCompletionStreamAsync(string, string, string, double, int, CancellationToken)"/>, senza ragionamento.</summary>
    IAsyncEnumerable<StreamChunk> GenerateCompletionStreamWithoutReasoningAsync(
        string model,
        string systemPrompt,
        string userPrompt,
        double temperature,
        int maxTokens,
        CancellationToken ct = default);
}
