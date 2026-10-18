# Assistente in partita — Piano 1 (fette A0 + A1): chat con strumenti nel gateway, facciata M.E.AI, spike

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Stato (2026-10-12): eseguito.** A0 è consegnata con #4247 (issue #4239), A1 con la PR di #4240. L'esito dello spike è nella spec, §7.1: con il prompt v1 nessun modello superava tutte le trappole; #4256 lo ha risolto con il prompt v2, che supera il gate. Le caselle qui sotto restano come le ha scritte il piano: decisioni prese in corso d'opera e rilievi rinviati sono nel corpo delle due PR.

**Goal:** far passare dal gateway LLM una chat con strumenti (tool calling) con le stesse garanzie di oggi — tetto, degrado, fallback, circuit breaker, redazione PII, costo — esporla come `IChatClient` di `Microsoft.Extensions.AI`, e misurare con uno spike se i modelli disponibili chiamano gli strumenti dell'assistente in partita in modo affidabile.

**Architecture:** `ILlmService.ChatAsync` e `ILlmClient.ChatAsync` aggiungono messaggi e strumenti al gateway; `HybridLlmService` estrae il ciclo dei tentativi di `GenerateAnswerAsync` e lo riusa per la chat. Sopra il gateway, `GatewayChatClient : IChatClient` traduce i tipi M.E.AI, e una factory compone `FunctionInvokingChatClient → OpenTelemetryChatClient → GatewayChatClient`. Gli strumenti dell'assistente leggono da porte (`ISessionAssistantReader`) e scrivono solo proposte (`IAssistantProposalSink`); un endpoint admin esegue un set di scenari con lettori finti attraverso il gateway vero.

**Tech Stack:** .NET 9, ASP.NET Minimal APIs + MediatR, `Microsoft.Extensions.AI` **10.6.0**, xUnit v3 + Moq + FluentAssertions, Node (script dello spike).

**Spec:** [`docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md`](../specs/2026-10-11-session-assistant-agent-design.md) (§3, §4, §5.1, §7.1, §8 righe A0 e A1). Le fette A2-A7 hanno piani propri, scritti sul codice prodotto da questo.

## Global Constraints

- Percorsi: `API` = `apps/api/src/Api`, `TESTS` = `apps/api/tests/Api.Tests`.
- Pacchetto: `Microsoft.Extensions.AI` versione **10.6.0**, perché dipende dalle `Microsoft.Extensions.*` **10.0.8** già referenziate; una versione più alta fa scattare NU1605 contro `System.Text.Json` 10.0.8. Nessun pacchetto `Microsoft.Extensions.AI.OpenAI`: l'HTTP resta nei client esistenti.
- Ogni chiamata HTTP a un provider resta in `API/Services/LlmClients/` (`LlmProviderEgressArchitectureTests`). `GatewayChatClient` dipende solo da `ILlmService`.
- `RequestSource.SessionAssistant` è un valore **in coda** all'enum (il nome è persistito come stringa in `llm_cost_logs`) e va classificato `Answer` in `LlmBudgetClassification.For`.
- Gli strumenti non ricevono `IMediator`, `ISender` né tipi di GameManagement (spec §3.2).
- Gli strumenti di proposta **non scrivono la partita**: chiamano solo `IAssistantProposalSink`.
- Nomi degli strumenti, esatti: `stato_partita`, `cerca_regole`, `proponi_punteggio`, `proponi_turno`. Parametri di `proponi_punteggio`: `giocatore`, `punti`, `dimensione?`, `round?`.
- Round derivato: `CurrentTurnIndex / giocatori attivi + 1` (spec §5.1). Punti = **incremento** sulla cella (giocatore, round, dimensione).
- `FunctionInvokingChatClient.IncludeDetailedErrors = false`.
- Nessuna cache di risposte nella catena dell'assistente.
- Commit: `feat|fix|test|docs|refactor(scope): descrizione (#issue)`, intestazione entro 72 caratteri (hook `commit-msg`), corpo che finisce con `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Mai `--no-verify`. Il pre-commit esegue il typecheck del frontend: in un worktree serve `cd apps/web && pnpm install --offline --prefer-offline` una volta, e il commit va lanciato in background (dura minuti).
- Prima di ogni `dotnet test` locale: nessun `testhost` vivo (`tasklist | grep testhost` → `taskkill //PID <pid> //F`). Riportare sempre il **numero** di test eseguiti, non solo l'esito.
- Il build ha `TreatWarningsAsErrors` con gli analyzer Meziantou e Sonar (misurato dalla review del piano): `System.Threading.Lock` per i lock (MA0158); `StringComparer`/`StringComparison` espliciti su dizionari e confronti di stringhe (MA0002, MA0006); `RegexOptions.ExplicitCapture` (MA0023); un'eccezione `internal` porta `#pragma warning disable S3871` come `ProviderProbeService.cs`; un `catch (Exception)` porta `#pragma warning disable CA1031` con il commento `SERVICE BOUNDARY`.
- Gli argomenti non validi di uno strumento falliscono nel binding di M.E.AI **prima** dello strumento: il `FunctionInvoker` della factory li traduce in `AssistantToolError("argomenti_non_validi")` e li conta. Senza, il modello riceve «Error: Function failed.» e dopo tre errori consecutivi il ciclo lancia.
- I risultati degli strumenti si serializzano compatti (`WriteIndented = false`): il JSON indentato gonfia i token misurati dallo spike.

## Review Focus

1. **Una risposta del provider con `content: null` e `tool_calls`** (il caso normale del tool calling): il client non deve trattarla come «risposta vuota» né fallire. → test in Task 2 (`ChatAsync_ToolCallsWithNullContent_ReturnsSuccessWithCalls`).
2. **Argomenti di uno strumento non validi** (JSON rotto o parametro obbligatorio mancante, frequente con i modelli piccoli): né `GatewayChatClient` né il ciclo devono lanciare; il modello riceve `argomenti_non_validi` e può riprovare, e il turno conta l'errore. → test in Task 5 (`GetResponse_InvalidArgumentsJson_DoesNotThrow`, `Turn_InvalidToolArguments_BecomeATypedErrorForTheModel`).
3. **Il tetto che scatta a metà ciclo**: il giro rifiutato non deve arrivare al client, e il ciclo deve fermarsi con il codice d'errore, non ripetere. → test in Task 5 (`Turn_CapReachedOnSecondRoundTrip_StopsWithoutCallingTheClientAgain`).
4. **Punti negativi** («togli 2 punti a Marco»): sono un incremento valido, non un errore. → test in Task 6 (`ProposeScore_NegativePoints_IsAValidDelta`).
5. **Un nome di giocatore scritto in minuscolo, solo il nome di battesimo, o vuoto** («marco», «Marco» fra «Marco Rossi» e «Marco Bianchi», «»): deve risolversi come lo risolve oggi `IPlayerNameResolutionService` (che non normalizza gli accenti), e un nome vuoto è un errore tipizzato, non un'eccezione. → test in Task 6 (`ProposeScore_UsesTheMatcherNotAnExactComparison`, `ProposeScore_EmptyName_IsATypedError`, `AssistantPlayerMatcherTests`).

---

## Preparazione (una volta, prima del Task 1)

- [ ] **P0: Spec e piano su `main-dev`.** L'ADR-097 (Task 1) e l'esito dello spike (Task 9) puntano alla spec: deve stare su `main-dev` prima di ramificare A0, altrimenti il link è rotto (`docs-linkcheck.yml` controlla i documenti cambiati). Dal worktree `D:/Repositories/wt-session-assistant-spec`, branch `feature/session-assistant-spec`: committa questo piano (`docs(ai): piano 1 dell'assistente in partita`), push, PR verso `main-dev` con spec e piano, merge, branch eliminato.

- [ ] **P1: Issue.** Crea l'epic e due issue, una per fetta. Nessun conteggio volatile nei corpi.

```bash
gh issue create --title "epic: assistente in partita — un agente con strumenti" --label "epic" --body-file - <<'EOF'
Spec: docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md
Piano 1 (A0 + A1): docs/for-developers/plans/2026-10-12-session-assistant-plan-1-gateway-and-spike.md
Fette: A0 gateway con strumenti · A1 spike · A2 proposte e profilo · A3 ciclo ed endpoint · A4 pannello · A5 cerca_regole · A6 gate · A7 ritiri.
Collegato all'epic ADR-095 #4163.
EOF
gh issue create --title "feat(ai): A0 — chat con strumenti nel gateway e facciata M.E.AI" --body-file - <<'EOF'
Spec §3 e §8 (riga A0). Rosso prima: caratterizzazione di GenerateAnswerAsync invariata; un turno da tre giri produce tre ammissioni al tetto e tre righe di costo; con il tetto raggiunto il giro non arriva al client.
EOF
gh issue create --title "feat(ai): A1 — strumenti, esecutore di valutazione e spike dell'assistente" --body-file - <<'EOF'
Spec §5.1 e §7.1. Uscita dello spike: modello consigliato, giri, scadenza e soglie, scritti nella spec.
EOF
```

Annota i numeri: `<EPIC>`, `<A0>`, `<A1>`. Se l'etichetta `epic` non esiste, togli `--label`.

- [ ] **P2: Branch di A0**, dal genitore aggiornato (regola di igiene di CLAUDE.md):

```bash
git -C D:/Repositories/meepleai-monorepo-main fetch origin main-dev
git -C D:/Repositories/meepleai-monorepo-main worktree add -b feature/issue-<A0>-gateway-chat-tools D:/Repositories/wt-assistant-a0 origin/main-dev
git -C D:/Repositories/wt-assistant-a0 config branch.feature/issue-<A0>-gateway-chat-tools.parent main-dev
cd D:/Repositories/wt-assistant-a0/apps/web && pnpm install --offline --prefer-offline
```

- [ ] **P3: Coordinamento.** Un'altra sessione lavora sulla fetta 5 di ADR-095 e tocca `HybridLlmService`. Prima del Task 4, controlla le PR aperte su quel file:

```bash
gh pr list --state open --json number,title,files \
  --jq '.[] | select(any(.files[]; .path | endswith("HybridLlmService.cs"))) | "\(.number) \(.title)"'
```

Se ce n'è una, riallinea il branch dopo il suo merge (`git rebase origin/main-dev`).

---

## Fetta A0 — chat con strumenti nel gateway

### Task 1: Contratto della chat con strumenti, `RequestSource.SessionAssistant`, ADR-097

**Files:**
- Create: `API/Services/LlmChat.cs`
- Modify: `API/Services/ILlmService.cs` (record `LlmCompletionResult`; interfaccia `ILlmService`)
- Modify: `API/Services/RequestSource.cs` (in coda all'enum)
- Modify: `API/BoundedContexts/KnowledgeBase/Domain/Services/LlmManagement/LlmBudgetClass.cs` (switch di `LlmBudgetClassification.For`)
- Modify: `API/Api.csproj` (pacchetto)
- Create: `docs/for-claude/architecture/adr/adr-097-meai-facade-over-the-llm-gateway.md`
- Modify: `docs/for-claude/architecture/adr/README.md` (riga dell'indice)
- Test: `TESTS/BoundedContexts/KnowledgeBase/Application/Services/RequestSourceTests.cs`
- Test: `TESTS/BoundedContexts/KnowledgeBase/Domain/Services/LlmManagement/LlmBudgetClassificationTests.cs`
- Test: `TESTS/Services/LlmChatContractTests.cs`

**Interfaces:**
- Produces: `LlmChatRoles`, `LlmToolCall(string Id, string Name, string ArgumentsJson)`, `LlmChatMessage(string Role, string? Content, IReadOnlyList<LlmToolCall>? ToolCalls = null, string? ToolCallId = null, string? ToolName = null)`, `LlmToolDefinition(string Name, string Description, JsonElement ParametersSchema)`, `LlmChatRequest(IReadOnlyList<LlmChatMessage> Messages, IReadOnlyList<LlmToolDefinition> Tools, string PrimaryModelId, string? FallbackModelId, double Temperature, int MaxTokens, Guid? ProfileVersionId, RequestSource Source)`, `LlmCompletionResult.ToolCalls`, `ILlmService.ChatAsync(LlmChatRequest, CancellationToken)`, `RequestSource.SessionAssistant`.

- [ ] **Step 1: Write the failing tests**

In `RequestSourceTests.cs`: aggiungi `values.Should().Contain(RequestSource.SessionAssistant);` in `RequestSource_HasExpectedValues`; aggiungi `[InlineData(RequestSource.SessionAssistant, "SessionAssistant")]` a `RequestSource_ToStringMatchesExpected` e `[InlineData("SessionAssistant", RequestSource.SessionAssistant)]` a `RequestSource_ParsesFromString`; sostituisci l'ultimo test con:

```csharp
    [Fact]
    public void RequestSource_HasExactly12Values()
    {
        // Regression guard: a new value must be a conscious choice. The name is persisted as a string
        // (llm_cost_logs/llm_request_logs.request_source), so append, never rename. Checked on #4165:
        // no code, dashboard or infra config enumerates the values today. #4166: each value also needs
        // a budget class (LlmBudgetClassification), which LlmBudgetClassificationTests checks.
        // ADR-097: SessionAssistant is the in-game assistant (spec 2026-10-11 §3.2).
        var count = Enum.GetValues<RequestSource>().Length;
        count.Should().Be(12);
    }
```

In `LlmBudgetClassificationTests.cs`, nel `[Theory] Classification`, aggiungi dopo la riga di `AgentTask`:

```csharp
    [InlineData(RequestSource.SessionAssistant, "Answer")] // assistente in partita: l'host aspetta la risposta
```

Crea `TESTS/Services/LlmChatContractTests.cs`:

```csharp
using System.Text.Json;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// ADR-097: il contratto della chat con strumenti nel gateway. Un servizio che non la implementa
/// risponde con un fallimento esplicito, mai con una risposta vuota scambiata per un successo.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class LlmChatContractTests
{
    [Fact]
    public void CompletionResult_HasNoToolCallsByDefault()
    {
        LlmCompletionResult.CreateSuccess("text").ToolCalls.Should().BeEmpty();
        LlmCompletionResult.CreateFailure("error").ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task ChatAsync_NotImplementedByAService_FailsExplicitly()
    {
        ILlmService service = new TestFailingLlmService();
        var request = new LlmChatRequest(
            [new LlmChatMessage(LlmChatRoles.User, "Marco fa 5 punti")],
            [new LlmToolDefinition("stato_partita", "Legge lo stato", JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement)],
            "deepseek-flash", null, 0.2, 600, null, RequestSource.SessionAssistant);

        var result = await service.ChatAsync(request, TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not supported");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build apps/api/tests/Api.Tests/Api.Tests.csproj`
Expected: FAIL a compilazione — `RequestSource.SessionAssistant`, `LlmChatRequest`, `LlmChatMessage`, `LlmToolDefinition`, `LlmChatRoles`, `ToolCalls` e `ChatAsync` non esistono.

- [ ] **Step 3: Write minimal implementation**

`API/Api.csproj`, nello stesso `ItemGroup` dei pacchetti `Microsoft.Extensions.*`:

```xml
    <PackageReference Include="Microsoft.Extensions.AI" Version="10.6.0" />
```

Crea `API/Services/LlmChat.cs`:

```csharp
using System.Text.Json;

#pragma warning disable MA0048 // File name must match type name - the chat contract of the gateway lives together
namespace Api.Services;

/// <summary>ADR-097: i ruoli dei messaggi di una chat con strumenti, nel formato dei provider OpenAI-compatibili.</summary>
internal static class LlmChatRoles
{
    public const string System = "system";
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string Tool = "tool";
}

/// <summary>Una chiamata a uno strumento chiesta dal modello. Gli argomenti restano JSON, come li ha scritti il modello.</summary>
internal sealed record LlmToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>
/// Un messaggio della chat. Un messaggio dell'assistente può portare chiamate a strumenti invece del
/// testo; un messaggio con ruolo <see cref="LlmChatRoles.Tool"/> porta il risultato di una chiamata.
/// </summary>
internal sealed record LlmChatMessage(
    string Role,
    string? Content,
    IReadOnlyList<LlmToolCall>? ToolCalls = null,
    string? ToolCallId = null,
    string? ToolName = null);

/// <summary>Uno strumento offerto al modello: nome, descrizione e schema JSON dei parametri.</summary>
internal sealed record LlmToolDefinition(string Name, string Description, JsonElement ParametersSchema);

/// <summary>
/// ADR-097: una chiamata di chat con strumenti. Come <see cref="LlmAnswerRequest"/>, modello, riserva,
/// temperatura e lunghezza li decide il chiamante; il gateway applica tetto, degrado, fallback,
/// circuit breaker, redazione e costo a ogni chiamata.
/// </summary>
internal sealed record LlmChatRequest(
    IReadOnlyList<LlmChatMessage> Messages,
    IReadOnlyList<LlmToolDefinition> Tools,
    string PrimaryModelId,
    string? FallbackModelId,
    double Temperature,
    int MaxTokens,
    Guid? ProfileVersionId,
    RequestSource Source);
```

In `API/Services/ILlmService.cs`, dentro `record LlmCompletionResult` dopo `ProfileVersionId`:

```csharp
    /// <summary>
    /// ADR-097: le chiamate a strumenti chieste dal modello in una chat con strumenti. Vuota per ogni
    /// altra chiamata.
    /// </summary>
    public IReadOnlyList<LlmToolCall> ToolCalls { get; init; } = [];
```

In `interface ILlmService`, dopo `GenerateAnswerAsync`:

```csharp
    /// <summary>
    /// ADR-097: una chiamata di chat con strumenti, con le garanzie di <see cref="GenerateAnswerAsync"/>.
    /// Il risultato porta il testo, le chiamate a strumenti chieste dal modello, o entrambi. Chi non la
    /// implementa fallisce in modo esplicito.
    /// </summary>
    Task<LlmCompletionResult> ChatAsync(LlmChatRequest request, CancellationToken ct = default)
        => Task.FromResult(LlmCompletionResult.CreateFailure("Tool calling is not supported by this LLM service."));
```

In `API/Services/RequestSource.cs`, in coda dopo `Evaluation,`:

```csharp

    /// <summary>
    /// L'assistente in partita (ADR-097, spec 2026-10-11): ogni giro del suo ciclo con strumenti.
    /// Classe di budget <c>Answer</c>, perché l'host aspetta la risposta.
    /// </summary>
    SessionAssistant,
```

In `LlmBudgetClass.cs`, nello switch dopo `RequestSource.AgentTask => LlmBudgetClass.Answer,`:

```csharp
        RequestSource.SessionAssistant => LlmBudgetClass.Answer,
```

Crea l'ADR `docs/for-claude/architecture/adr/adr-097-meai-facade-over-the-llm-gateway.md`:

```markdown
# ADR-097 — Microsoft.Extensions.AI come facciata del gateway per il tool calling

**Date**: 2026-10-12
**Status**: Accepted
**Related**: [ADR-095](./adr-095-single-answer-pipeline-and-versioned-agent-profile.md) (gateway unico; chiude la domanda aperta n. 4) · spec [`2026-10-11-session-assistant-agent-design.md`](../../../for-developers/specs/2026-10-11-session-assistant-agent-design.md) §3

## Decisione

1. Il gateway espone una chat con strumenti: `ILlmService.ChatAsync(LlmChatRequest)` e `ILlmClient.ChatAsync(...)`. `HybridLlmService` la serve con lo **stesso ciclo dei tentativi** di `GenerateAnswerAsync`: ordine dei modelli dal chiamante con degrado, circuit breaker, redazione PII per OpenRouter, ammissione al tetto, costo, chiusura dell'ammissione.
2. `Microsoft.Extensions.AI` (`IChatClient`) sta **sopra** il gateway, non sotto: `GatewayChatClient` traduce i tipi M.E.AI verso `LlmChatRequest` e dipende solo da `ILlmService`. La catena è `FunctionInvokingChatClient → OpenTelemetryChatClient → GatewayChatClient`.
3. Solo il pacchetto `Microsoft.Extensions.AI` 10.6.0. Nessun adapter di provider M.E.AI: l'HTTP resta in `Services/LlmClients/`.

## Perché sopra e non sotto

La logica di ogni tentativo è privata in `HybridLlmService`. Un `IChatClient` attaccato al provider dovrebbe copiarla, insieme alla risoluzione delle credenziali a ogni chiamata (`IProviderCredentialResolver`) e alle protezioni dei test (`PaidAiHostGuardExtensions`, `TestFailingLlmService`). Sopra il gateway, ogni giro del ciclo con strumenti passa da tetto e costo **per costruzione**, e `LlmProviderEgressArchitectureTests` non cambia.

## Conseguenze

- Ogni giro del ciclo è una riga del registro costi e un'ammissione al tetto: il costo di un turno è la somma dei suoi giri.
- `LlmCompletionResult.ToolCalls` è additivo: vuoto per ogni altra chiamata.
- Lo streaming della chat con strumenti non c'è: l'assistente bufferizza la risposta (spec §6.1).
- La contabilità dei token in cache resta fuori (spec §4.1).
```

In `docs/for-claude/architecture/adr/README.md`, aggiungi la riga dell'ADR-097 accanto a quella dell'ADR-096, nello stesso formato delle righe vicine.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~RequestSourceTests|FullyQualifiedName~LlmBudgetClassificationTests|FullyQualifiedName~LlmChatContractTests"`
Expected: PASS. Annota il numero di test eseguiti. Poi `dotnet build apps/api/src/Api/Api.csproj -c Release` senza NU1605 né errori.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Services/LlmChat.cs apps/api/src/Api/Services/ILlmService.cs apps/api/src/Api/Services/RequestSource.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/LlmManagement/LlmBudgetClass.cs apps/api/src/Api/Api.csproj apps/api/tests/Api.Tests/Services/LlmChatContractTests.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/RequestSourceTests.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Services/LlmManagement/LlmBudgetClassificationTests.cs docs/for-claude/architecture/adr/
git commit -F <file con il messaggio>   # "feat(ai): contratto della chat con strumenti nel gateway (#<A0>)"
```

---

### Task 2: Strumenti nel formato OpenAI; `ChatAsync` per OpenRouter e DeepSeek

**Files:**
- Modify: `API/Services/LlmClients/OpenAiChatModels.cs` (`OpenAiChatMessage` + due record nuovi)
- Create: `API/Services/LlmClients/OpenAiToolPayload.cs`
- Modify: `API/Services/LlmClients/ILlmClient.cs`
- Modify: `API/Services/LlmClients/OpenRouterLlmClient.cs` (`HandleCompletionResponseAsync` + metodo nuovo)
- Modify: `API/Services/LlmClients/DeepSeekLlmClient.cs` (`HandleCompletionResponseAsync` + metodo nuovo)
- Modify: `API/Services/LlmClients/OllamaLlmClient.cs` (solo un corpo provvisorio di `ChatAsync`, sostituito nel Task 3)
- Test: `TESTS/Services/LlmClients/OpenAiToolPayloadTests.cs`
- Test: `TESTS/Services/LlmClients/OpenAiCompatibleChatTests.cs`

**Interfaces:**
- Consumes: i tipi del Task 1.
- Produces: `ILlmClient.ChatAsync(string model, IReadOnlyList<LlmChatMessage> messages, IReadOnlyList<LlmToolDefinition> tools, double temperature, int maxTokens, CancellationToken ct = default) : Task<LlmCompletionResult>`; `OpenAiToolPayload.CreateRequest(...)`, `OpenAiToolPayload.ReadToolCalls(OpenAiChatMessage?)`.

- [ ] **Step 1: Write the failing tests**

Crea `TESTS/Services/LlmClients/OpenAiToolPayloadTests.cs`:

```csharp
using System.Text.Json;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services.LlmClients;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class OpenAiToolPayloadTests
{
    private static readonly JsonElement EmptySchema =
        JsonDocument.Parse("""{"type":"object","properties":{"giocatore":{"type":"string"}},"required":["giocatore"]}""").RootElement;

    [Fact]
    public async Task CreateRequest_WritesMessagesToolsAndToolResults()
    {
        var messages = new List<LlmChatMessage>
        {
            new(LlmChatRoles.System, "Sei l'assistente."),
            new(LlmChatRoles.User, "Marco fa 5 punti"),
            new(LlmChatRoles.Assistant, null, [new LlmToolCall("call_1", "proponi_punteggio", """{"giocatore":"Marco","punti":5}""")]),
            new(LlmChatRoles.Tool, """{"proposalId":"x"}""", ToolCallId: "call_1", ToolName: "proponi_punteggio"),
        };

        using var request = OpenAiToolPayload.CreateRequest("deepseek-flash", messages,
            [new LlmToolDefinition("proponi_punteggio", "Propone un punteggio", EmptySchema)], 0.2, 600);
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = body.RootElement;

        request.RequestUri!.ToString().Should().Be("chat/completions");
        root.GetProperty("model").GetString().Should().Be("deepseek-flash");
        root.GetProperty("stream").GetBoolean().Should().BeFalse();
        root.GetProperty("max_tokens").GetInt32().Should().Be(600);
        var tool = root.GetProperty("tools")[0];
        tool.GetProperty("type").GetString().Should().Be("function");
        tool.GetProperty("function").GetProperty("name").GetString().Should().Be("proponi_punteggio");
        tool.GetProperty("function").GetProperty("parameters").GetProperty("required")[0].GetString().Should().Be("giocatore");
        var assistant = root.GetProperty("messages")[2];
        assistant.GetProperty("role").GetString().Should().Be("assistant");
        assistant.GetProperty("content").ValueKind.Should().Be(JsonValueKind.Null);
        var call = assistant.GetProperty("tool_calls")[0];
        call.GetProperty("id").GetString().Should().Be("call_1");
        call.GetProperty("function").GetProperty("arguments").GetString().Should().Be("""{"giocatore":"Marco","punti":5}""");
        var toolResult = root.GetProperty("messages")[3];
        toolResult.GetProperty("role").GetString().Should().Be("tool");
        toolResult.GetProperty("tool_call_id").GetString().Should().Be("call_1");
    }

    [Fact]
    public async Task CreateRequest_WithoutTools_OmitsTheToolsField()
    {
        using var request = OpenAiToolPayload.CreateRequest("deepseek-flash",
            [new LlmChatMessage(LlmChatRoles.User, "ciao")], [], 0.2, 100);
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken));

        body.RootElement.TryGetProperty("tools", out _).Should().BeFalse();
    }

    [Fact]
    public void ReadToolCalls_MapsIdNameAndArguments_AndFillsMissingIds()
    {
        var message = JsonSerializer.Deserialize<OpenAiChatMessage>("""
            {"role":"assistant","content":null,"tool_calls":[
              {"id":"call_9","type":"function","function":{"name":"stato_partita","arguments":"{}"}},
              {"id":"","type":"function","function":{"name":"proponi_turno","arguments":""}}]}
            """);

        var calls = OpenAiToolPayload.ReadToolCalls(message);

        calls.Should().HaveCount(2);
        calls[0].Should().Be(new LlmToolCall("call_9", "stato_partita", "{}"));
        calls[1].Id.Should().Be("call_1");
        calls[1].ArgumentsJson.Should().Be("{}");
    }
}
```

Crea `TESTS/Services/LlmClients/OpenAiCompatibleChatTests.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.Services.LlmClients;

/// <summary>ADR-097: OpenRouter e DeepSeek parlano lo stesso formato OpenAI per gli strumenti.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class OpenAiCompatibleChatTests
{
    private const string ToolCallsResponse = """
        {"id":"gen-1","model":"m","choices":[{"message":{"role":"assistant","content":null,
          "tool_calls":[{"id":"call_1","type":"function","function":{"name":"proponi_punteggio","arguments":"{\"giocatore\":\"Marco\",\"punti\":5}"}}]},
          "finish_reason":"tool_calls"}],
         "usage":{"prompt_tokens":120,"completion_tokens":18,"total_tokens":138}}
        """;

    public static TheoryData<string> Providers => new() { "OpenRouter", "DeepSeek" };

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task ChatAsync_ToolCallsWithNullContent_ReturnsSuccessWithCalls(string provider)
    {
        string? sentBody = null;
        var client = Create(provider, ToolCallsResponse, body => sentBody = body);

        var result = await client.ChatAsync("model-x",
            [new LlmChatMessage(LlmChatRoles.User, "Marco fa 5 punti")],
            [new LlmToolDefinition("proponi_punteggio", "Propone", JsonDocument.Parse("""{"type":"object"}""").RootElement)],
            0.2, 600, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.Response.Should().BeEmpty();
        result.ToolCalls.Should().ContainSingle()
            .Which.Should().Be(new LlmToolCall("call_1", "proponi_punteggio", """{"giocatore":"Marco","punti":5}"""));
        result.Usage.PromptTokens.Should().Be(120);
        sentBody.Should().Contain("\"tools\"");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task ChatAsync_ProviderError_IsAFailure(string provider)
    {
        var client = Create(provider, """{"error":"boom"}""", _ => { }, HttpStatusCode.InternalServerError);

        var result = await client.ChatAsync("model-x", [new LlmChatMessage(LlmChatRoles.User, "x")], [], 0.2, 100,
            TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.ToolCalls.Should().BeEmpty();
    }

    private static ILlmClient Create(string provider, string responseJson, Action<string> onSend,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken ct) =>
            {
                onSend(await request.Content!.ReadAsStringAsync(ct));
                return new HttpResponseMessage(status) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") };
            });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler.Object));

        var resolver = new Mock<IProviderCredentialResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("sk-test");
        var services = new ServiceCollection();
        services.AddSingleton(resolver.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var calculator = new Mock<ILlmCostCalculator>();
        calculator.Setup(c => c.CalculateCost(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((string model, string p, int prompt, int completion) => new LlmCostCalculation
            {
                ModelId = model, Provider = p, PromptTokens = prompt, CompletionTokens = completion,
                InputCost = 0.00001m, OutputCost = 0.00002m,
            });

        return provider == "OpenRouter"
            ? new OpenRouterLlmClient(factory.Object, scopeFactory, calculator.Object, Mock.Of<ILogger<OpenRouterLlmClient>>())
            : new DeepSeekLlmClient(factory.Object, scopeFactory, calculator.Object, Mock.Of<ILogger<DeepSeekLlmClient>>());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build apps/api/tests/Api.Tests/Api.Tests.csproj`
Expected: FAIL a compilazione — `OpenAiToolPayload` e `ILlmClient.ChatAsync` non esistono.

- [ ] **Step 3: Write minimal implementation**

In `OpenAiChatModels.cs`, sostituisci `OpenAiChatMessage` e aggiungi i due record:

```csharp
internal record OpenAiChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    // Con tool_calls il provider manda "content": null; i chiamanti usano già «?? string.Empty».
    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("tool_calls")]
    public List<OpenAiToolCall>? ToolCalls { get; init; }
}

internal record OpenAiToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";

    [JsonPropertyName("function")]
    public OpenAiToolFunction? Function { get; init; }
}

internal record OpenAiToolFunction
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string Arguments { get; init; } = "{}";
}
```

Crea `API/Services/LlmClients/OpenAiToolPayload.cs`:

```csharp
using System.Text;
using System.Text.Json;

namespace Api.Services.LlmClients;

/// <summary>
/// ADR-097: la chat con strumenti nel formato OpenAI, condivisa da OpenRouter e DeepSeek. Costruisce
/// la richiesta e legge le chiamate a strumenti; l'invio, le credenziali e gli errori restano ai client.
/// </summary>
internal static class OpenAiToolPayload
{
    public static HttpRequestMessage CreateRequest(
        string model,
        IReadOnlyList<LlmChatMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        double temperature,
        int maxTokens)
    {
        var payload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["model"] = model,
            ["messages"] = messages.Select(Message).ToList(),
            ["temperature"] = temperature,
            ["max_tokens"] = maxTokens,
            ["stream"] = false,
        };

        if (tools.Count > 0)
        {
            payload["tools"] = tools.Select(t => (object)new
            {
                type = "function",
                function = new { name = t.Name, description = t.Description, parameters = t.ParametersSchema },
            }).ToList();
        }

        return new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
    }

    public static IReadOnlyList<LlmToolCall> ReadToolCalls(OpenAiChatMessage? message)
    {
        if (message?.ToolCalls is not { Count: > 0 } calls)
        {
            return [];
        }

        return calls
            .Where(c => c.Function is not null && !string.IsNullOrWhiteSpace(c.Function.Name))
            .Select((c, index) => new LlmToolCall(
                string.IsNullOrWhiteSpace(c.Id) ? $"call_{index}" : c.Id,
                c.Function!.Name,
                string.IsNullOrWhiteSpace(c.Function.Arguments) ? "{}" : c.Function.Arguments))
            .ToList();
    }

    private static Dictionary<string, object?> Message(LlmChatMessage message)
    {
        if (string.Equals(message.Role, LlmChatRoles.Tool, StringComparison.Ordinal))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = LlmChatRoles.Tool,
                ["tool_call_id"] = message.ToolCallId,
                ["content"] = message.Content ?? string.Empty,
            };
        }

        if (message.ToolCalls is { Count: > 0 } calls)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = LlmChatRoles.Assistant,
                ["content"] = string.IsNullOrEmpty(message.Content) ? null : message.Content,
                ["tool_calls"] = calls.Select(c => new
                {
                    id = c.Id,
                    type = "function",
                    function = new { name = c.Name, arguments = c.ArgumentsJson },
                }).ToList(),
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["role"] = message.Role,
            ["content"] = message.Content ?? string.Empty,
        };
    }
}
```

In `ILlmClient.cs`, aggiungi in fondo all'interfaccia:

```csharp
    /// <summary>
    /// ADR-097: una chiamata di chat con strumenti, non in streaming. Il risultato porta il testo
    /// (vuoto se il modello chiede solo strumenti) e le chiamate in <see cref="LlmCompletionResult.ToolCalls"/>.
    /// </summary>
    Task<LlmCompletionResult> ChatAsync(
        string model,
        IReadOnlyList<LlmChatMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        double temperature,
        int maxTokens,
        CancellationToken ct = default);
```

In `OpenRouterLlmClient.HandleCompletionResponseAsync`, sostituisci la riga finale `return LlmCompletionResult.CreateSuccess(assistantMessage, usage, cost, metadata);` con:

```csharp
        return LlmCompletionResult.CreateSuccess(assistantMessage, usage, cost, metadata) with
        {
            ToolCalls = OpenAiToolPayload.ReadToolCalls(chatResponse.Choices[0].Message),
        };
```

e aggiungi il metodo a `OpenRouterLlmClient`, dopo `GenerateCompletionAsync(string model, string systemPrompt, ...)`:

```csharp
    /// <inheritdoc/>
    public async Task<LlmCompletionResult> ChatAsync(
        string model,
        IReadOnlyList<LlmChatMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        double temperature,
        int maxTokens,
        CancellationToken ct = default)
    {
        if (messages.Count == 0)
        {
            return LlmCompletionResult.CreateFailure("No messages provided");
        }

        string apiKey;
        try
        {
            apiKey = await ResolveApiKeyAsync(ct).ConfigureAwait(false);
        }
        catch (ProviderCredentialNotConfiguredException ex)
        {
            _logger.LogError(ex, "OpenRouter provider is not configured (no DB row and no env var)");
            return LlmCompletionResult.CreateFailure("OpenRouter provider is not configured (missing API key)");
        }

        try
        {
            using var httpRequest = OpenAiToolPayload.CreateRequest(model, messages, tools, temperature, maxTokens);
            ApplyAuthorization(httpRequest, apiKey);

            _logger.LogInformation("Generating OpenRouter tool chat using {Model} ({ToolCount} tools, max_tokens={MaxTokens})",
                model, tools.Count, maxTokens);

            using var response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
            return await HandleCompletionResponseAsync(response, model, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "OpenRouter tool chat timed out");
            return LlmCompletionResult.CreateFailure("Request timed out");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during OpenRouter tool chat");
            return LlmCompletionResult.CreateFailure($"HTTP error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize OpenRouter tool chat response");
            return LlmCompletionResult.CreateFailure("Invalid response format");
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // SERVICE BOUNDARY: wraps unexpected OpenRouter errors into a failed LlmCompletionResult
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during OpenRouter tool chat");
            return LlmCompletionResult.CreateFailure($"Error: {ex.Message}");
        }
#pragma warning restore CA1031
    }
```

In `DeepSeekLlmClient`: stessa modifica alla riga finale di `HandleCompletionResponseAsync` (che deserializza anch'esso `OpenAiChatResponse`):

```csharp
        return LlmCompletionResult.CreateSuccess(assistantMessage, usage, cost, metadata) with
        {
            ToolCalls = OpenAiToolPayload.ReadToolCalls(chatResponse.Choices[0].Message),
        };
```

e questo metodo, dopo il suo `GenerateCompletionAsync(string model, string systemPrompt, ...)`:

```csharp
    /// <inheritdoc/>
    public async Task<LlmCompletionResult> ChatAsync(
        string model,
        IReadOnlyList<LlmChatMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        double temperature,
        int maxTokens,
        CancellationToken ct = default)
    {
        if (messages.Count == 0)
        {
            return LlmCompletionResult.CreateFailure("No messages provided");
        }

        string apiKey;
        try
        {
            apiKey = await ResolveApiKeyAsync(ct).ConfigureAwait(false);
        }
        catch (ProviderCredentialNotConfiguredException ex)
        {
            _logger.LogError(ex, "DeepSeek provider is not configured (no DB row and no env var)");
            return LlmCompletionResult.CreateFailure("DeepSeek provider is not configured (missing API key)");
        }

        try
        {
            using var httpRequest = OpenAiToolPayload.CreateRequest(model, messages, tools, temperature, maxTokens);
            ApplyAuthorization(httpRequest, apiKey);

            _logger.LogInformation("Generating DeepSeek tool chat using {Model} ({ToolCount} tools, max_tokens={MaxTokens})",
                model, tools.Count, maxTokens);

            using var response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
            return await HandleCompletionResponseAsync(response, model, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "DeepSeek tool chat timed out");
            return LlmCompletionResult.CreateFailure("Request timed out");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during DeepSeek tool chat");
            return LlmCompletionResult.CreateFailure($"HTTP error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize DeepSeek tool chat response");
            return LlmCompletionResult.CreateFailure("Invalid response format");
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // SERVICE BOUNDARY: wraps unexpected DeepSeek errors into a failed LlmCompletionResult
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during DeepSeek tool chat");
            return LlmCompletionResult.CreateFailure($"Error: {ex.Message}");
        }
#pragma warning restore CA1031
    }
```

In `OllamaLlmClient`, perché il progetto compili, un corpo provvisorio che il Task 3 sostituisce:

```csharp
    /// <inheritdoc/>
    public Task<LlmCompletionResult> ChatAsync(
        string model,
        IReadOnlyList<LlmChatMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        double temperature,
        int maxTokens,
        CancellationToken ct = default) =>
        Task.FromResult(LlmCompletionResult.CreateFailure("Ollama tool chat is not implemented yet."));
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~OpenAiToolPayloadTests|FullyQualifiedName~OpenAiCompatibleChatTests|FullyQualifiedName~OpenRouterLlmClientTests|FullyQualifiedName~DeepSeekLlmClientTests"`
Expected: PASS, compresi i test esistenti dei due client (il cambio a `HandleCompletionResponseAsync` è additivo). Annota il numero.

- [ ] **Step 5: Commit** — `feat(llm): strumenti nel formato OpenAI per OpenRouter e DeepSeek (#<A0>)`

---

### Task 3: `ChatAsync` per Ollama

**Files:**
- Modify: `API/Services/LlmClients/OllamaLlmClient.cs` (corpo di `ChatAsync`; record `OllamaMessage`; record nuovi)
- Test: `TESTS/Services/LlmClients/OllamaChatTests.cs`

**Interfaces:**
- Consumes: `ILlmClient.ChatAsync` (Task 2).
- Produces: nessuna interfaccia nuova.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.Services.LlmClients;

/// <summary>ADR-097: Ollama (api/chat) ha gli strumenti, ma gli argomenti arrivano come oggetto e senza id.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class OllamaChatTests
{
    [Fact]
    public async Task ChatAsync_SendsToolsAndArgumentsAsObjects_AndReadsToolCalls()
    {
        string? sent = null;
        const string response = """
            {"model":"qwen2.5:3b","message":{"role":"assistant","content":"",
              "tool_calls":[{"function":{"name":"proponi_punteggio","arguments":{"giocatore":"Marco","punti":5}}}]},
             "done":true,"done_reason":"stop"}
            """;
        var client = Create(response, body => sent = body);

        var result = await client.ChatAsync("qwen2.5:3b",
        [
            new LlmChatMessage(LlmChatRoles.User, "Marco fa 5 punti"),
            new LlmChatMessage(LlmChatRoles.Assistant, null, [new LlmToolCall("call_0", "stato_partita", "{}")]),
            new LlmChatMessage(LlmChatRoles.Tool, """{"round":2}""", ToolCallId: "call_0", ToolName: "stato_partita"),
        ],
        [new LlmToolDefinition("proponi_punteggio", "Propone", JsonDocument.Parse("""{"type":"object"}""").RootElement)],
        0.2, 300, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        var call = result.ToolCalls.Should().ContainSingle().Subject;
        call.Name.Should().Be("proponi_punteggio");
        call.Id.Should().Be("call_0");
        JsonDocument.Parse(call.ArgumentsJson).RootElement.GetProperty("punti").GetInt32().Should().Be(5);

        using var body = JsonDocument.Parse(sent!);
        body.RootElement.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString().Should().Be("proponi_punteggio");
        var echoed = body.RootElement.GetProperty("messages")[1].GetProperty("tool_calls")[0].GetProperty("function");
        echoed.GetProperty("arguments").ValueKind.Should().Be(JsonValueKind.Object);
        var toolMessage = body.RootElement.GetProperty("messages")[2];
        toolMessage.GetProperty("role").GetString().Should().Be("tool");
        toolMessage.GetProperty("tool_name").GetString().Should().Be("stato_partita");
    }

    private static OllamaLlmClient Create(string responseJson, Action<string> onSend)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken ct) =>
            {
                onSend(await request.Content!.ReadAsStringAsync(ct));
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") };
            });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler.Object));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OLLAMA_URL"] = "http://ollama:11434" }).Build();
        var calculator = new Mock<ILlmCostCalculator>();
        calculator.Setup(c => c.CalculateCost(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((string model, string p, int prompt, int completion) => new LlmCostCalculation
            {
                ModelId = model, Provider = p, PromptTokens = prompt, CompletionTokens = completion, InputCost = 0, OutputCost = 0,
            });
        return new OllamaLlmClient(factory.Object, config, calculator.Object, Mock.Of<ILogger<OllamaLlmClient>>());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~OllamaChatTests"`
Expected: FAIL — `result.Success` è false («Ollama tool chat is not implemented yet.»).

- [ ] **Step 3: Write minimal implementation**

In fondo a `OllamaLlmClient.cs`, sostituisci `OllamaMessage` e aggiungi i record:

```csharp
internal record OllamaMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("tool_calls")]
    public List<OllamaToolCall>? ToolCalls { get; init; }
}

internal record OllamaToolCall
{
    [JsonPropertyName("function")]
    public OllamaToolFunction? Function { get; init; }
}

internal record OllamaToolFunction
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("arguments")]
    public JsonElement Arguments { get; init; }
}
```

Sostituisci il corpo provvisorio di `ChatAsync` con:

```csharp
    /// <inheritdoc/>
    public async Task<LlmCompletionResult> ChatAsync(
        string model,
        IReadOnlyList<LlmChatMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        double temperature,
        int maxTokens,
        CancellationToken ct = default)
    {
        if (messages.Count == 0)
        {
            return LlmCompletionResult.CreateFailure("No messages provided");
        }

        try
        {
            var payload = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["model"] = model,
                ["messages"] = messages.Select(OllamaMessagePayload).ToList(),
                ["options"] = new { temperature, num_predict = maxTokens },
                ["stream"] = false,
            };
            if (tools.Count > 0)
            {
                payload["tools"] = tools.Select(t => (object)new
                {
                    type = "function",
                    function = new { name = t.Name, description = t.Description, parameters = t.ParametersSchema },
                }).ToList();
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/chat")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            };
            using var response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Ollama tool chat error: {Status} - {Body}", response.StatusCode, DataMasking.MaskResponseBody(responseBody));
                return LlmCompletionResult.CreateFailure($"Ollama API error: {(int)response.StatusCode} ({response.StatusCode})");
            }

            var chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(responseBody);
            if (chatResponse?.Message == null)
            {
                return LlmCompletionResult.CreateFailure("No response returned from Ollama");
            }

            var text = chatResponse.Message.Content ?? string.Empty;
            var calls = (chatResponse.Message.ToolCalls ?? [])
                .Where(c => c.Function is not null && !string.IsNullOrWhiteSpace(c.Function.Name))
                .Select((c, index) => new LlmToolCall(
                    $"call_{index}",
                    c.Function!.Name,
                    c.Function.Arguments.ValueKind == JsonValueKind.Object ? c.Function.Arguments.GetRawText() : "{}"))
                .ToList();

            var promptTokens = EstimateTokenCount(string.Join(' ', messages.Select(m => m.Content ?? string.Empty)));
            var completionTokens = EstimateTokenCount(text) + calls.Sum(c => EstimateTokenCount(c.ArgumentsJson));
            var cost = _costCalculator.CalculateCost(model, ProviderName, promptTokens, completionTokens);

            return LlmCompletionResult.CreateSuccess(
                text,
                new LlmUsage(promptTokens, completionTokens, promptTokens + completionTokens),
                new LlmCost
                {
                    InputCost = cost.InputCost,
                    OutputCost = cost.OutputCost,
                    ModelId = model,
                    Provider = ProviderName,
                    PricingStatus = cost.PricingStatus,
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["model"] = chatResponse.Model ?? model,
                    ["provider"] = "Ollama",
                    ["cost_usd"] = "0.000000",
                }) with { ToolCalls = calls };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Ollama tool chat timed out");
            return LlmCompletionResult.CreateFailure("Request timed out");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during Ollama tool chat");
            return LlmCompletionResult.CreateFailure($"HTTP error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Ollama tool chat response");
            return LlmCompletionResult.CreateFailure("Invalid response format");
        }
    }

    /// <summary>
    /// Ollama vuole gli argomenti come oggetto e il nome dello strumento sul messaggio del risultato,
    /// non l'id della chiamata.
    /// </summary>
    private static Dictionary<string, object?> OllamaMessagePayload(LlmChatMessage message)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["role"] = message.Role,
            ["content"] = message.Content ?? string.Empty,
        };

        if (string.Equals(message.Role, LlmChatRoles.Tool, StringComparison.Ordinal) && message.ToolName is not null)
        {
            payload["tool_name"] = message.ToolName;
        }

        if (message.ToolCalls is { Count: > 0 } calls)
        {
            payload["tool_calls"] = calls.Select(c => new
            {
                function = new { name = c.Name, arguments = ParseArguments(c.ArgumentsJson) },
            }).ToList();
        }

        return payload;
    }

    private static readonly JsonElement EmptyArguments = CreateEmptyArguments();

    private static JsonElement CreateEmptyArguments()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    private static JsonElement ParseArguments(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return EmptyArguments;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : EmptyArguments;
        }
        catch (JsonException)
        {
            return EmptyArguments;
        }
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~OllamaChatTests|FullyQualifiedName~OllamaLlmClientTests"`
Expected: PASS. Annota il numero.

- [ ] **Step 5: Commit** — `feat(llm): strumenti nella chat di Ollama (#<A0>)`

---

### Task 4: `HybridLlmService.ChatAsync` sul ciclo dei tentativi estratto

**Files:**
- Modify: `API/BoundedContexts/KnowledgeBase/Application/Services/HybridLlmService.cs` (`GenerateAnswerAsync`, `OrderAnswerModelsAsync` e la sua chiamata in `GenerateAnswerStreamAsync`; metodi nuovi)
- Test: `TESTS/BoundedContexts/KnowledgeBase/Application/Services/HybridLlmServiceChatTests.cs`
- Caratterizzazione: `TESTS/BoundedContexts/KnowledgeBase/Application/Services/HybridLlmServiceAnswerTests.cs` (non si modifica)

**Interfaces:**
- Consumes: `ILlmClient.ChatAsync`, `LlmChatRequest` (Task 1-2).
- Produces: `HybridLlmService.ChatAsync(LlmChatRequest, CancellationToken)` che implementa `ILlmService.ChatAsync`.

- [ ] **Step 1: Caratterizzazione prima del refactor**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~HybridLlmService"`
Expected: PASS. Annota il numero di test: deve restare identico dopo lo Step 4.

- [ ] **Step 2: Write the failing test**

Crea `HybridLlmServiceChatTests.cs`:

```csharp
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using LlmUserContext = Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.LlmUserContext;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// ADR-097: la chat con strumenti passa dal gateway con le garanzie della risposta (#4168): modello e
/// riserva dal chiamante, circuit breaker a ogni tentativo, tetto prima della chiamata, costo con la
/// versione del profilo.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class HybridLlmServiceChatTests
{
    private const string Primary = "deepseek-flash";
    private const string Reserve = "meta-llama/llama-3.3-70b-instruct";
    private static readonly Guid VersionId = Guid.Parse("6f2c0c5e-3c1a-4c3e-9a7e-1f6a2b9d4e10");
    private static readonly LlmToolDefinition Tool =
        new("stato_partita", "Legge lo stato della partita", JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement);

    private readonly Mock<ILlmClient> _deepSeek = Client("DeepSeek", Primary);
    private readonly Mock<ILlmClient> _openRouter = Client("OpenRouter", Reserve);
    private readonly Mock<ILlmProviderSelector> _selector = new();
    private readonly Mock<ICircuitBreakerRegistry> _circuitBreakers = new();
    private readonly Mock<ILlmCostService> _costService = new();
    private readonly Mock<ILlmBudgetGate> _gate = new();
    private readonly List<LlmCompletionResult> _loggedCosts = [];

    public HybridLlmServiceChatTests()
    {
        Chats(_deepSeek, Primary, "DeepSeek");
        Chats(_openRouter, Reserve, "OpenRouter");
        _gate.Setup(g => g.AdmitAsync(It.IsAny<LlmBudgetRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Admission(LlmBudgetRejection.None));
        _circuitBreakers.Setup(c => c.GetState(It.IsAny<string>())).Returns(CircuitState.Closed);
        _costService
            .Setup(c => c.LogSuccessAsync(It.IsAny<LlmCompletionResult>(), It.IsAny<LlmUserContext>(), It.IsAny<long>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<LlmCompletionResult, LlmUserContext, long, RequestSource, CancellationToken>((r, _, _, _, _) => _loggedCosts.Add(r))
            .Returns(Task.CompletedTask);
    }

    private HybridLlmService CreateSut() =>
        new([_deepSeek.Object, _openRouter.Object], _selector.Object, _circuitBreakers.Object, _costService.Object,
            NullLogger<HybridLlmService>.Instance, budgetGate: _gate.Object);

    private static LlmChatRequest Request(string? fallback = Reserve) =>
        new([new LlmChatMessage(LlmChatRoles.System, "Sei l'assistente."), new LlmChatMessage(LlmChatRoles.User, "a chi tocca?")],
            [Tool], Primary, fallback, Temperature: 0.2, MaxTokens: 600, ProfileVersionId: VersionId, RequestSource.SessionAssistant);

    [Fact]
    public async Task Chat_SendsMessagesToolsModelTemperatureAndLength_AndReturnsToolCalls()
    {
        var result = await CreateSut().ChatAsync(Request(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.ToolCalls.Should().ContainSingle().Which.Name.Should().Be("stato_partita");
        result.ProfileVersionId.Should().Be(VersionId);
        _deepSeek.Verify(c => c.ChatAsync(Primary, It.Is<IReadOnlyList<LlmChatMessage>>(m => m.Count == 2),
            It.Is<IReadOnlyList<LlmToolDefinition>>(t => t.Single().Name == "stato_partita"), 0.2, 600, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Chat_AdmitsEachCallAsSessionAssistant_CountingTheToolSchemas()
    {
        await CreateSut().ChatAsync(Request(), TestContext.Current.CancellationToken);

        var messageCharacters = "Sei l'assistente.".Length + "a chi tocca?".Length;
        _gate.Verify(g => g.AdmitAsync(
            It.Is<LlmBudgetRequest>(r => r.Source == RequestSource.SessionAssistant && r.ModelId == Primary
                && r.MaxOutputTokens == 600 && r.PromptCharacters > messageCharacters),
            It.IsAny<CancellationToken>()), Times.Once);
        _loggedCosts.Should().ContainSingle().Which.ProfileVersionId.Should().Be(VersionId);
    }

    [Fact]
    public async Task Chat_PrimaryFails_AnswersWithTheReserve_AndBothAttemptsReachTheBreaker()
    {
        _deepSeek.Setup(c => c.ChatAsync(Primary, It.IsAny<IReadOnlyList<LlmChatMessage>>(), It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("DeepSeek 503"));

        var result = await CreateSut().ChatAsync(Request(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        _selector.Verify(s => s.RecordFailure("DeepSeek", Primary, It.IsAny<long>(), It.IsAny<LlmCompletionResult?>()), Times.Once);
        _selector.Verify(s => s.RecordSuccess("OpenRouter", Reserve, It.IsAny<long>(), It.IsAny<LlmCompletionResult>()), Times.Once);
    }

    [Fact]
    public async Task Chat_OverTheCap_IsRefused_WithoutCallingAnyClient()
    {
        _gate.Setup(g => g.AdmitAsync(It.Is<LlmBudgetRequest>(r => r.ModelId == Primary), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Admission(LlmBudgetRejection.DailyCapReached, "refused"));

        var result = await CreateSut().ChatAsync(Request(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Metadata["error_code"].Should().Be(LlmBudgetAdmission.DailyBudgetErrorCode);
        _deepSeek.Verify(c => c.ChatAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<LlmChatMessage>>(), It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
            It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _openRouter.Verify(c => c.ChatAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<LlmChatMessage>>(), It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
            It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Chat_WithoutMessages_IsAProgrammingError()
    {
        var act = () => CreateSut().ChatAsync(Request() with { Messages = [] }, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static Mock<ILlmClient> Client(string provider, string model)
    {
        var client = new Mock<ILlmClient>();
        client.Setup(c => c.ProviderName).Returns(provider);
        client.Setup(c => c.SupportsModel(model)).Returns(true);
        return client;
    }

    private static void Chats(Mock<ILlmClient> client, string model, string provider) =>
        client.Setup(c => c.ChatAsync(model, It.IsAny<IReadOnlyList<LlmChatMessage>>(), It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                string.Empty,
                new LlmUsage(150, 12, 162),
                new LlmCost { InputCost = 0.0001m, OutputCost = 0.0002m, ModelId = model, Provider = provider }) with
            {
                ToolCalls = [new LlmToolCall("call_1", "stato_partita", "{}")],
            });

    private static LlmBudgetAdmission Admission(LlmBudgetRejection rejection, string? message = null) => new()
    {
        ReservationId = rejection == LlmBudgetRejection.None ? Guid.NewGuid() : Guid.Empty,
        Rejection = rejection,
        BudgetClass = LlmBudgetClass.Answer,
        EstimatedCostUsd = 0.004m,
        Message = message,
    };
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet build apps/api/tests/Api.Tests/Api.Tests.csproj`
Expected: FAIL a compilazione, CS1061 — `CreateSut()` restituisce il tipo concreto `HybridLlmService`, che non vede il `ChatAsync` di default dell'interfaccia. È il rosso giusto: il metodo deve esistere sulla classe.

- [ ] **Step 4: Write minimal implementation**

In `HybridLlmService`, sostituisci l'intero metodo `GenerateAnswerAsync` (quello che restituisce `Task<LlmCompletionResult>`) con:

```csharp
    /// <inheritdoc/>
    public async Task<LlmCompletionResult> GenerateAnswerAsync(LlmAnswerRequest request, CancellationToken ct = default)
    {
        // Resta async: un argomento non valido continua a uscire dal Task, come prima del refactor.
        ValidateAnswerRequest(request);
        return await RunAnswerAttemptsAsync(
            new AnswerAttemptPlan(request.PrimaryModelId, request.FallbackModelId, request.MaxTokens, request.ProfileVersionId, request.Source),
            (client, model) =>
            {
                var (systemPrompt, userPrompt) = RedactFor(client, request.SystemPrompt, request.UserPrompt);
                return new AnswerAttempt(
                    systemPrompt.Length + userPrompt.Length,
                    token => client.GenerateCompletionAsync(model, systemPrompt, userPrompt, request.Temperature, request.MaxTokens, token));
            },
            "Answer",
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<LlmCompletionResult> ChatAsync(LlmChatRequest request, CancellationToken ct = default)
    {
        ValidateChatRequest(request);
        var toolCharacters = request.Tools.Sum(t => t.Name.Length + t.Description.Length + t.ParametersSchema.GetRawText().Length);
        return await RunAnswerAttemptsAsync(
            new AnswerAttemptPlan(request.PrimaryModelId, request.FallbackModelId, request.MaxTokens, request.ProfileVersionId, request.Source),
            (client, model) =>
            {
                var messages = RedactFor(client, request.Messages);
                var characters = toolCharacters + messages.Sum(m =>
                    (m.Content?.Length ?? 0) + (m.ToolCalls?.Sum(c => c.Name.Length + c.ArgumentsJson.Length) ?? 0));
                return new AnswerAttempt(
                    characters,
                    token => client.ChatAsync(model, messages, request.Tools, request.Temperature, request.MaxTokens, token));
            },
            "Chat",
            ct).ConfigureAwait(false);
    }

    /// <summary>Ciò che serve al ciclo dei tentativi, uguale per la risposta e per la chat con strumenti.</summary>
    private sealed record AnswerAttemptPlan(
        string PrimaryModelId, string? FallbackModelId, int MaxTokens, Guid? ProfileVersionId, RequestSource Source);

    /// <summary>Un tentativo già preparato per un client: i caratteri del prompt (per il tetto) e la chiamata.</summary>
    private sealed record AnswerAttempt(int PromptCharacters, Func<CancellationToken, Task<LlmCompletionResult>> Call);

    /// <summary>
    /// ADR-097: il ciclo dei tentativi di <see cref="GenerateAnswerAsync"/>, estratto perché la chat con
    /// strumenti lo riusi identico. Per ogni modello in ordine: client, circuit breaker, ammissione al
    /// tetto, chiamata, costo, chiusura dell'ammissione. Un tetto o una quota fermano tutto; un modello
    /// fuori catalogo passa al successivo.
    /// </summary>
    private async Task<LlmCompletionResult> RunAnswerAttemptsAsync(
        AnswerAttemptPlan plan,
        Func<ILlmClient, string, AnswerAttempt> prepare,
        string operation,
        CancellationToken ct)
    {
        var costContext = AttributeCost(LlmUserContext.Anonymous);
        LlmCompletionResult? catalogRefusal = null;
        var attemptedAny = false;
        string? lastError = null;

        foreach (var model in await OrderAnswerModelsAsync(plan.PrimaryModelId, plan.FallbackModelId, plan.Source, ct).ConfigureAwait(false))
        {
            var client = _clients.FirstOrDefault(c => c.SupportsModel(model));
            if (client is null)
            {
                _logger.LogWarning("{Operation}: no provider supports model {Model}, trying the next model", operation, model);
                continue;
            }

            if (_circuitBreakerRegistry.GetState(client.ProviderName) == CircuitState.Open)
            {
                _logger.LogWarning("{Operation}: circuit breaker OPEN for {Provider}, skipping model {Model}", operation, client.ProviderName, model);
                continue;
            }

            var attempt = prepare(client, model);
            var admission = await AdmitAsync(plan.Source, model, client.ProviderName,
                attempt.PromptCharacters, plan.MaxTokens, 0, ct).ConfigureAwait(false);
            if (!admission.IsAllowed)
            {
                // Un tetto o una quota fermano la risposta: la riserva spenderebbe dallo stesso budget.
                if (admission.Rejection != LlmBudgetRejection.ModelNotInCatalog)
                {
                    return Refused(admission) with { ProfileVersionId = plan.ProfileVersionId };
                }

                catalogRefusal = Refused(admission);
                continue;
            }

            attemptedAny = true;
            var stopwatch = Stopwatch.StartNew();
            var actualCost = 0m;
            try
            {
                var result = await attempt.Call(ct).ConfigureAwait(false);
                stopwatch.Stop();

                // I client trasformano anche l'annullamento in un risultato fallito: chi ha smesso di
                // aspettare è il chiamante, non il provider, quindi niente circuit breaker né riserva.
                if (!result.Success && ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                }

                if (result.Success)
                {
                    actualCost = result.Cost.TotalCost;
                    result = result with { ProfileVersionId = plan.ProfileVersionId };
                    _providerSelector.RecordSuccess(client.ProviderName, model, stopwatch.ElapsedMilliseconds, result);
                    await _costService.LogSuccessAsync(result, costContext, stopwatch.ElapsedMilliseconds, plan.Source, CancellationToken.None)
                        .ConfigureAwait(false);
                    return result;
                }

                _logger.LogWarning("{Operation}: model {Model} via {Provider} failed: {Error}", operation, model, client.ProviderName, result.ErrorMessage);
                _providerSelector.RecordFailure(client.ProviderName, model, stopwatch.ElapsedMilliseconds, result);
                await _costService.LogFailureAsync(result.ErrorMessage, costContext, stopwatch.ElapsedMilliseconds, plan.Source, CancellationToken.None)
                    .ConfigureAwait(false);
                lastError = result.ErrorMessage;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "{Operation}: model {Model} via {Provider} threw", operation, model, client.ProviderName);
                _providerSelector.RecordFailure(client.ProviderName, model, stopwatch.ElapsedMilliseconds);
                await _costService.LogFailureAsync(ex.Message, costContext, stopwatch.ElapsedMilliseconds, plan.Source, CancellationToken.None)
                    .ConfigureAwait(false);
                lastError = ex.Message;
            }
            finally
            {
                CompleteAdmission(admission, actualCost);
            }
        }

        // Nessun modello è nel catalogo: il motivo è quello, non un provider giù.
        if (!attemptedAny && catalogRefusal is not null)
        {
            return catalogRefusal with { ProfileVersionId = plan.ProfileVersionId };
        }

        _logger.LogWarning("{Operation}: no model answered (last error: {Error})", operation, lastError ?? "none");
        var unavailable = LlmCompletionResult.CreateFailure(NoModelAnsweredMessage);
        return unavailable with
        {
            ProfileVersionId = plan.ProfileVersionId,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["error_code"] = LlmAnswerErrorCodes.ProviderUnavailable,
            },
        };
    }

    private static void ValidateChatRequest(LlmChatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.PrimaryModelId))
            throw new ArgumentException("A chat needs a primary model.", nameof(request));
        if (request.Messages is null || request.Messages.Count == 0)
            throw new ArgumentException("A chat needs at least one message.", nameof(request));
        if (request.Tools is null)
            throw new ArgumentException("Tools must not be null: pass an empty list.", nameof(request));
        if (request.Temperature < 0 || request.MaxTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Temperature must be >= 0 and MaxTokens > 0.");
    }

    /// <summary>Issue #5510 per la chat: il testo dei messaggi esce redatto verso OpenRouter; gli argomenti JSON restano intatti.</summary>
    private IReadOnlyList<LlmChatMessage> RedactFor(ILlmClient client, IReadOnlyList<LlmChatMessage> messages) =>
        _piiDetector != null && string.Equals(client.ProviderName, "OpenRouter", StringComparison.Ordinal)
            ? messages.Select(m => m.Content is null ? m : m with { Content = _piiDetector.ScanAndRedact(m.Content) }).ToList()
            : messages;
```

Cambia la firma di `OrderAnswerModelsAsync` e le prime due righe del corpo:

```csharp
    private async Task<IReadOnlyList<string>> OrderAnswerModelsAsync(
        string primary, string? reserve, RequestSource source, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reserve) || string.Equals(reserve, primary, StringComparison.Ordinal))
        {
            return [primary];
        }

        if (_budgetGate is not null
            && LlmBudgetClassification.For(source) == LlmBudgetClass.Answer
            && await _budgetGate.IsDegradedAsync(ct).ConfigureAwait(false))
```

(il resto del corpo invariato). In `GenerateAnswerStreamAsync` sostituisci la chiamata `OrderAnswerModelsAsync(request, …)` con `OrderAnswerModelsAsync(request.PrimaryModelId, request.FallbackModelId, request.Source, …)`, lasciando invariato il token di annullamento passato.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~HybridLlmService"`
Expected: PASS, con il numero dello Step 1 **più** i test nuovi. Se un test di `HybridLlmServiceAnswerTests` diventa rosso, il refactor ha cambiato il comportamento: correggi il refactor, non il test.

- [ ] **Step 6: Commit** — `refactor(llm): ciclo dei tentativi comune e ChatAsync nel gateway (#<A0>)`

---

### Task 5: `GatewayChatClient`, factory della catena, DI, telemetria e garanzia del ciclo

**Files:**
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/AssistantModelSettings.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/ChatTurnMeter.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/IAssistantChatClientFactory.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/AssistantGatewayException.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/AssistantToolError.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/GatewayChatClient.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/AssistantChatClientFactory.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/SessionAssistantServiceExtensions.cs`
- Modify: `API/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs` (dopo `services.AddScoped<ILlmService, HybridLlmService>();`)
- Modify: `API/Extensions/ObservabilityServiceExtensions.cs` (vicino alle altre `.AddSource(...)`)
- Test: `TESTS/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/GatewayChatClientTests.cs`
- Test: `TESTS/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/AssistantChatLoopTests.cs`
- Test: `TESTS/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/AssistantToolInvocationTests.cs`
- Test: `TESTS/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/SessionAssistantRegistrationTests.cs`
- Test: `TESTS/Architecture/SessionAssistantArchitectureTests.cs`

**Interfaces:**
- Consumes: `ILlmService.ChatAsync` (Task 4).
- Produces: `AssistantModelSettings(string PrimaryModelId, string? FallbackModelId, double Temperature, int MaxTokens, int MaxToolIterations, Guid? ProfileVersionId, RequestSource Source)`; `ChatTurnMeter` (`RoundTrips`, `PromptTokens`, `CompletionTokens`, `CostUsd`, `InvalidArguments`, `ToolCalls`); `IAssistantChatClientFactory.Create(AssistantModelSettings, ChatTurnMeter) : IChatClient`; `AssistantGatewayException(string ErrorCode)`; `AssistantToolError(string Error, string Message, IReadOnlyList<string>? Candidates = null)` nel namespace `…Application.SessionAssistant`; `AssistantChatClientFactory.TelemetrySourceName`; `SessionAssistantServiceExtensions.AddSessionAssistant(IServiceCollection)`.

- [ ] **Step 1: Write the failing tests**

`GatewayChatClientTests.cs`:

```csharp
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GatewayChatClientTests
{
    private static readonly AssistantModelSettings Settings =
        new("deepseek-flash", null, 0.2, 600, 4, Guid.Parse("6f2c0c5e-3c1a-4c3e-9a7e-1f6a2b9d4e10"), RequestSource.SessionAssistant);

    [Fact]
    public async Task GetResponse_TranslatesHistoryToolsAndToolResults()
    {
        LlmChatRequest? sent = null;
        var gateway = new Mock<ILlmService>();
        gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmChatRequest, CancellationToken>((r, _) => sent = r)
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Tocca a Luca.", new LlmUsage(10, 3, 13)));
        var client = new GatewayChatClient(gateway.Object, Settings, new ChatTurnMeter());
        var tool = AIFunctionFactory.Create(() => "{}", "stato_partita", "Legge lo stato");

        var response = await client.GetResponseAsync(
        [
            new ChatMessage(ChatRole.System, "Sei l'assistente."),
            new ChatMessage(ChatRole.User, "a chi tocca?"),
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call_1", "stato_partita", new Dictionary<string, object?>())]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call_1", JsonDocument.Parse("""{"turno":"Luca"}""").RootElement)]),
        ], new ChatOptions { Tools = [tool] }, TestContext.Current.CancellationToken);

        response.Text.Should().Be("Tocca a Luca.");
        response.Usage!.InputTokenCount.Should().Be(10);
        sent!.Tools.Should().ContainSingle().Which.Name.Should().Be("stato_partita");
        sent.Source.Should().Be(RequestSource.SessionAssistant);
        sent.Messages.Select(m => m.Role).Should().Equal("system", "user", "assistant", "tool");
        sent.Messages[2].ToolCalls!.Single().Id.Should().Be("call_1");
        sent.Messages[3].ToolCallId.Should().Be("call_1");
        sent.Messages[3].ToolName.Should().Be("stato_partita");
        sent.Messages[3].Content.Should().Be("""{"turno":"Luca"}""");
    }

    [Fact]
    public async Task GetResponse_ToolCalls_BecomeFunctionCallContents()
    {
        var gateway = Gateway(LlmCompletionResult.CreateSuccess(string.Empty) with
        {
            ToolCalls = [new LlmToolCall("call_7", "proponi_punteggio", """{"giocatore":"Marco","punti":5}""")],
        });
        var client = new GatewayChatClient(gateway.Object, Settings, new ChatTurnMeter());

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Marco fa 5")], null, TestContext.Current.CancellationToken);

        var call = response.Messages.Single().Contents.OfType<FunctionCallContent>().Single();
        call.CallId.Should().Be("call_7");
        call.Arguments!["giocatore"]!.ToString().Should().Be("Marco");
        response.FinishReason.Should().Be(ChatFinishReason.ToolCalls);
    }

    [Fact]
    public async Task GetResponse_InvalidArgumentsJson_DoesNotThrow()
    {
        var gateway = Gateway(LlmCompletionResult.CreateSuccess(string.Empty) with
        {
            ToolCalls = [new LlmToolCall("call_1", "proponi_punteggio", "{giocatore: Marco")],
        });
        var client = new GatewayChatClient(gateway.Object, Settings, new ChatTurnMeter());

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "x")], null, TestContext.Current.CancellationToken);

        response.Messages.Single().Contents.OfType<FunctionCallContent>().Single().Arguments.Should().BeEmpty();
    }

    [Fact]
    public async Task GetResponse_GatewayFailure_ThrowsWithTheErrorCode_AndIsMetered()
    {
        var refused = LlmCompletionResult.CreateFailure("cap") with
        {
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["error_code"] = LlmBudgetAdmission.DailyBudgetErrorCode },
        };
        var meter = new ChatTurnMeter();
        var client = new GatewayChatClient(Gateway(refused).Object, Settings, meter);

        var act = () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "x")], null, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<AssistantGatewayException>()).Which.ErrorCode.Should().Be(LlmBudgetAdmission.DailyBudgetErrorCode);
        meter.RoundTrips.Should().Be(1);
    }

    private static Mock<ILlmService> Gateway(LlmCompletionResult result)
    {
        var gateway = new Mock<ILlmService>();
        gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return gateway;
    }
}
```

`AssistantChatLoopTests.cs` — la garanzia centrale, con il gateway vero e i client finti:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using LlmUserContext = Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.LlmUserContext;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// ADR-097, spec §7.2: ogni giro del ciclo con strumenti passa dal tetto e dal registro costi, e un
/// giro rifiutato non arriva al client.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AssistantChatLoopTests
{
    private const string Model = "deepseek-flash";
    private readonly Mock<ILlmClient> _client = new();
    private readonly Mock<ILlmBudgetGate> _gate = new();
    private readonly Mock<ILlmCostService> _costService = new();
    private readonly Mock<ICircuitBreakerRegistry> _breakers = new();

    public AssistantChatLoopTests()
    {
        _client.Setup(c => c.ProviderName).Returns("DeepSeek");
        _client.Setup(c => c.SupportsModel(Model)).Returns(true);
        _breakers.Setup(b => b.GetState(It.IsAny<string>())).Returns(CircuitState.Closed);
        _client.SetupSequence(c => c.ChatAsync(Model, It.IsAny<IReadOnlyList<LlmChatMessage>>(), It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolCall("call_1"))
            .ReturnsAsync(ToolCall("call_2"))
            .ReturnsAsync(Text("Tocca a Luca."));
    }

    [Fact]
    public async Task Turn_WithTwoToolRounds_AdmitsAndLogsEveryRoundTrip()
    {
        _gate.Setup(g => g.AdmitAsync(It.IsAny<LlmBudgetRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Allowed);
        var invocations = 0;
        var tool = AIFunctionFactory.Create(() => { invocations++; return "{\"turno\":\"Luca\"}"; }, "stato_partita", "Legge lo stato");
        var meter = new ChatTurnMeter();

        var response = await Factory().Create(Settings, meter)
            .GetResponseAsync([new ChatMessage(ChatRole.User, "a chi tocca?")], new ChatOptions { Tools = [tool] }, TestContext.Current.CancellationToken);

        response.Text.Should().Be("Tocca a Luca.");
        invocations.Should().Be(2);
        meter.RoundTrips.Should().Be(3);
        _gate.Verify(g => g.AdmitAsync(It.Is<LlmBudgetRequest>(r => r.Source == RequestSource.SessionAssistant), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _costService.Verify(c => c.LogSuccessAsync(It.IsAny<LlmCompletionResult>(), It.IsAny<LlmUserContext>(), It.IsAny<long>(),
            RequestSource.SessionAssistant, It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Turn_CapReachedOnSecondRoundTrip_StopsWithoutCallingTheClientAgain()
    {
        _gate.SetupSequence(g => g.AdmitAsync(It.IsAny<LlmBudgetRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Allowed)
            .ReturnsAsync(new LlmBudgetAdmission
            {
                // ErrorCode è calcolato da Rejection: DailyCapReached → DailyBudgetErrorCode.
                ReservationId = Guid.Empty, Rejection = LlmBudgetRejection.DailyCapReached, BudgetClass = LlmBudgetClass.Answer,
                EstimatedCostUsd = 0.004m, Message = "cap",
            });
        var tool = AIFunctionFactory.Create(() => "{}", "stato_partita", "Legge lo stato");

        var act = () => Factory().Create(Settings, new ChatTurnMeter())
            .GetResponseAsync([new ChatMessage(ChatRole.User, "a chi tocca?")], new ChatOptions { Tools = [tool] }, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<AssistantGatewayException>()).Which.ErrorCode.Should().Be(LlmBudgetAdmission.DailyBudgetErrorCode);
        _client.Verify(c => c.ChatAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<LlmChatMessage>>(), It.IsAny<IReadOnlyList<LlmToolDefinition>>(),
            It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Chain_HasFunctionInvocationWithoutDetailedErrors_AndNoResponseCache()
    {
        var chat = Factory().Create(Settings, new ChatTurnMeter());

        var invoking = chat.GetService<FunctionInvokingChatClient>();
        invoking.Should().NotBeNull();
        invoking!.MaximumIterationsPerRequest.Should().Be(Settings.MaxToolIterations);
        invoking.IncludeDetailedErrors.Should().BeFalse();
        chat.GetService<DistributedCachingChatClient>().Should().BeNull("una risposta in cache salterebbe gli strumenti (spec §4.1)");
        chat.GetService<GatewayChatClient>().Should().NotBeNull();
    }

    private static AssistantModelSettings Settings => new(Model, null, 0.2, 600, 4, null, RequestSource.SessionAssistant);

    private static LlmBudgetAdmission Allowed => new()
    {
        ReservationId = Guid.NewGuid(), Rejection = LlmBudgetRejection.None, BudgetClass = LlmBudgetClass.Answer, EstimatedCostUsd = 0.001m,
    };

    private AssistantChatClientFactory Factory()
    {
        var gateway = new HybridLlmService([_client.Object], Mock.Of<ILlmProviderSelector>(), _breakers.Object, _costService.Object,
            NullLogger<HybridLlmService>.Instance, budgetGate: _gate.Object);
        return new AssistantChatClientFactory(gateway, NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider());
    }

    private static LlmCompletionResult ToolCall(string id) =>
        LlmCompletionResult.CreateSuccess(string.Empty, new LlmUsage(100, 5, 105),
            new LlmCost { InputCost = 0.0001m, OutputCost = 0.0001m, ModelId = Model, Provider = "DeepSeek" }) with
        {
            ToolCalls = [new LlmToolCall(id, "stato_partita", "{}")],
        };

    private static LlmCompletionResult Text(string text) =>
        LlmCompletionResult.CreateSuccess(text, new LlmUsage(120, 6, 126),
            new LlmCost { InputCost = 0.0001m, OutputCost = 0.0001m, ModelId = Model, Provider = "DeepSeek" });
}
```

`AssistantToolInvocationTests.cs` — gli argomenti non validi falliscono nel binding, prima dello strumento:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// Misurato con M.E.AI 10.6.0: un parametro obbligatorio mancante fa fallire il binding con
/// ArgumentException prima dello strumento. Senza FunctionInvoker il modello riceve «Error: Function
/// failed.», e dopo tre errori consecutivi il ciclo rilancia l'eccezione.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AssistantToolInvocationTests
{
    [Fact]
    public async Task Turn_InvalidToolArguments_BecomeATypedErrorForTheModel()
    {
        var requests = new List<LlmChatRequest>();
        var responses = new Queue<LlmCompletionResult>(
        [
            LlmCompletionResult.CreateSuccess(string.Empty) with { ToolCalls = [new LlmToolCall("call_1", "proponi_punteggio", "{}")] },
            LlmCompletionResult.CreateSuccess("Per quale giocatore?"),
        ]);
        var gateway = new Mock<ILlmService>();
        gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmChatRequest, CancellationToken>((r, _) => requests.Add(r))
            .ReturnsAsync(() => responses.Dequeue());
        var meter = new ChatTurnMeter();
        var tool = AIFunctionFactory.Create((string giocatore, int punti) => "ok", "proponi_punteggio", "Propone");
        var factory = new AssistantChatClientFactory(gateway.Object, NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider());

        var response = await factory.Create(new AssistantModelSettings("deepseek-flash", null, 0.2, 600, 4, null, RequestSource.SessionAssistant), meter)
            .GetResponseAsync([new ChatMessage(ChatRole.User, "segna")], new ChatOptions { Tools = [tool] }, TestContext.Current.CancellationToken);

        response.Text.Should().Be("Per quale giocatore?");
        meter.InvalidArguments.Should().Be(1);
        meter.ToolCalls.Should().Equal("proponi_punteggio");
        requests[1].Messages[^1].Role.Should().Be(LlmChatRoles.Tool);
        requests[1].Messages[^1].Content.Should().Contain("argomenti_non_validi");
    }
}
```

`SessionAssistantRegistrationTests.cs` — la DI dell'assistente si risolve, con la validazione dei lifetime:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.DependencyInjection;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// EndpointContractTests non risolve questi servizi (nessuna rotta nota li usa): la registrazione si
/// verifica qui, con ValidateOnBuild e ValidateScopes.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class SessionAssistantRegistrationTests
{
    [Fact]
    public void AddSessionAssistant_ResolvesEveryService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => Mock.Of<ILlmService>());
        services.AddSessionAssistant();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IAssistantChatClientFactory>().Should().NotBeNull();
    }
}
```

`SessionAssistantArchitectureTests.cs`:

```csharp
using System.Reflection;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>Spec 2026-10-11 §3.2: la facciata M.E.AI sta sopra il gateway e non tocca né client né HTTP.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
public sealed class SessionAssistantArchitectureTests
{
    private static readonly Type[] Forbidden = [typeof(ILlmClient), typeof(HttpClient), typeof(IHttpClientFactory), typeof(LlmProviderFactory)];

    [Theory]
    [InlineData(typeof(GatewayChatClient))]
    [InlineData(typeof(AssistantChatClientFactory))]
    public void FacadeTypes_DependOnlyOnTheGateway(Type type)
    {
        var dependencies = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
            .Concat(type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Select(f => f.FieldType))
            .ToList();

        dependencies.Should().NotContain(t => Forbidden.Contains(t), $"{type.Name} deve passare da ILlmService (ADR-097)");
        dependencies.Should().Contain(typeof(ILlmService));
    }
}
```

Se `LlmProviderFactory` non è un tipo accessibile dal progetto di test, toglilo da `Forbidden`: il divieto vero è su `ILlmClient` e `HttpClient`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build apps/api/tests/Api.Tests/Api.Tests.csproj`
Expected: FAIL a compilazione — i tipi di `SessionAssistant` non esistono.

- [ ] **Step 3: Write minimal implementation**

`Application/SessionAssistant/AssistantModelSettings.cs`:

```csharp
using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>Modello, riserva e limiti di un turno dell'assistente in partita (spec §3.2).</summary>
internal sealed record AssistantModelSettings(
    string PrimaryModelId,
    string? FallbackModelId,
    double Temperature,
    int MaxTokens,
    int MaxToolIterations,
    Guid? ProfileVersionId,
    RequestSource Source);
```

`Application/SessionAssistant/ChatTurnMeter.cs`:

```csharp
using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// I conti di un turno dell'assistente: quante chiamate al modello, quanti token, quanto costo. Un turno
/// con strumenti fa più chiamate; il costo del turno è la loro somma (ADR-097).
/// </summary>
internal sealed class ChatTurnMeter
{
    private readonly Lock _lock = new();
    private readonly List<string> _toolCalls = [];

    public int RoundTrips { get; private set; }
    public int PromptTokens { get; private set; }
    public int CompletionTokens { get; private set; }
    public decimal CostUsd { get; private set; }

    /// <summary>Chiamate a strumenti con argomenti che non si legano ai parametri (spec §7.1, «argomenti non validi»).</summary>
    public int InvalidArguments { get; private set; }

    /// <summary>Gli strumenti chiamati nel turno, in ordine; restano anche se il ciclo si ferma per un errore.</summary>
    public IReadOnlyList<string> ToolCalls
    {
        get
        {
            lock (_lock)
            {
                return _toolCalls.ToList();
            }
        }
    }

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
            _toolCalls.Add(name);
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
```

`Application/SessionAssistant/IAssistantChatClientFactory.cs`:

```csharp
using Microsoft.Extensions.AI;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// ADR-097: la catena di un turno dell'assistente — invocazione degli strumenti, telemetria, gateway.
/// Una catena per turno, perché porta modello, versione del profilo e contatore del turno.
/// </summary>
internal interface IAssistantChatClientFactory
{
    IChatClient Create(AssistantModelSettings settings, ChatTurnMeter meter);
}
```

`Application/SessionAssistant/AssistantGatewayException.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Il gateway non ha risposto a un giro del ciclo: tetto, quota o provider. Ferma il ciclo; il codice
/// è quello del gateway (per esempio <c>AI_PROVIDER_UNAVAILABLE</c> o il codice del tetto giornaliero).
/// </summary>
#pragma warning disable S3871 // Exceptions should be public — internal here is intentional (BC-private contract)
internal sealed class AssistantGatewayException(string errorCode)
    : Exception($"The LLM gateway refused or failed a round trip ({errorCode}).")
{
    public string ErrorCode { get; } = errorCode;
}
#pragma warning restore S3871
```

`Application/SessionAssistant/AssistantToolError.cs` (sta qui, non nel Task 6, perché il `FunctionInvoker` della factory lo restituisce già in questa fetta):

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Un errore che il modello deve leggere e risolvere chiedendo all'host (spec §6.3): argomenti non
/// validi, giocatore ambiguo, dimensione mancante, partita non modificabile.
/// </summary>
internal sealed record AssistantToolError(string Error, string Message, IReadOnlyList<string>? Candidates = null);
```

`Infrastructure/SessionAssistant/GatewayChatClient.cs`:

```csharp
using System.Runtime.CompilerServices;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.Services;
using Microsoft.Extensions.AI;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// ADR-097: <see cref="IChatClient"/> sopra il gateway. Traduce i messaggi M.E.AI in
/// <see cref="LlmChatRequest"/> e la risposta del gateway in <see cref="ChatResponse"/>. Ogni chiamata
/// è un giro: passa da tetto, degrado, fallback, circuit breaker e costo dentro
/// <see cref="ILlmService.ChatAsync"/>. Non conosce client né HTTP.
/// </summary>
internal sealed class GatewayChatClient : IChatClient
{
    private static readonly JsonSerializerOptions ResultJson = new(JsonSerializerDefaults.Web);

    private readonly ILlmService _gateway;
    private readonly AssistantModelSettings _settings;
    private readonly ChatTurnMeter _meter;

    public GatewayChatClient(ILlmService gateway, AssistantModelSettings settings, ChatTurnMeter meter)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _meter = meter ?? throw new ArgumentNullException(nameof(meter));
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = new LlmChatRequest(
            ToGatewayMessages(messages.ToList()),
            ToToolDefinitions(options?.Tools),
            _settings.PrimaryModelId,
            _settings.FallbackModelId,
            options?.Temperature ?? _settings.Temperature,
            options?.MaxOutputTokens ?? _settings.MaxTokens,
            _settings.ProfileVersionId,
            _settings.Source);

        var result = await _gateway.ChatAsync(request, cancellationToken).ConfigureAwait(false);
        _meter.Record(result);
        if (!result.Success)
        {
            throw new AssistantGatewayException(
                result.Metadata.TryGetValue("error_code", out var code) ? code : LlmAnswerErrorCodes.ProviderUnavailable);
        }

        var contents = new List<AIContent>();
        if (!string.IsNullOrEmpty(result.Response))
        {
            contents.Add(new TextContent(result.Response));
        }

        contents.AddRange(result.ToolCalls.Select(c => new FunctionCallContent(c.Id, c.Name, ParseArguments(c.ArgumentsJson))));

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, contents))
        {
            ModelId = result.Cost.ModelId,
            FinishReason = result.ToolCalls.Count > 0 ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop,
            Usage = new UsageDetails
            {
                InputTokenCount = result.Usage.PromptTokens,
                OutputTokenCount = result.Usage.CompletionTokens,
                TotalTokenCount = result.Usage.TotalTokens,
            },
        };
    }

    /// <summary>Niente streaming nel gateway con strumenti: l'assistente bufferizza (spec §6.1).</summary>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
        // Nessuna risorsa: il gateway è un servizio del container.
    }

    private static List<LlmChatMessage> ToGatewayMessages(IReadOnlyList<ChatMessage> history)
    {
        var toolNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var call in history.SelectMany(m => m.Contents.OfType<FunctionCallContent>()))
        {
            toolNames.TryAdd(call.CallId, call.Name);
        }

        var result = new List<LlmChatMessage>();
        foreach (var message in history)
        {
            if (message.Role == ChatRole.Tool)
            {
                result.AddRange(message.Contents.OfType<FunctionResultContent>().Select(r => new LlmChatMessage(
                    LlmChatRoles.Tool, SerializeResult(r.Result), ToolCallId: r.CallId, ToolName: toolNames.GetValueOrDefault(r.CallId))));
                continue;
            }

            var text = string.Concat(message.Contents.OfType<TextContent>().Select(t => t.Text));
            var calls = message.Contents.OfType<FunctionCallContent>()
                .Select(c => new LlmToolCall(c.CallId, c.Name,
                    JsonSerializer.Serialize(c.Arguments ?? new Dictionary<string, object?>(StringComparer.Ordinal))))
                .ToList();
            result.Add(new LlmChatMessage(
                RoleOf(message.Role),
                calls.Count > 0 && text.Length == 0 ? null : text,
                calls.Count > 0 ? calls : null));
        }

        return result;
    }

    private static string RoleOf(ChatRole role) =>
        role == ChatRole.System ? LlmChatRoles.System
        : role == ChatRole.Assistant ? LlmChatRoles.Assistant
        : LlmChatRoles.User;

    private static List<LlmToolDefinition> ToToolDefinitions(IList<AITool>? tools) =>
        tools?.OfType<AIFunctionDeclaration>()
            .Select(f => new LlmToolDefinition(f.Name, f.Description, f.JsonSchema))
            .ToList() ?? [];

    private static string SerializeResult(object? result) => result switch
    {
        null => "null",
        string text => text,
        JsonElement element => element.GetRawText(),
        _ => JsonSerializer.Serialize(result, ResultJson),
    };

    /// <summary>Argomenti non validi (capita con i modelli piccoli): lo strumento li riceve vuoti e risponde con un errore.</summary>
    private static Dictionary<string, object?> ParseArguments(string json)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return parsed is null
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                : new Dictionary<string, object?>(parsed, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }
    }
}
```

`Infrastructure/SessionAssistant/AssistantChatClientFactory.cs`:

```csharp
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.Services;
using Microsoft.Extensions.AI;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// ADR-097: <c>FunctionInvokingChatClient → OpenTelemetryChatClient → GatewayChatClient</c>. Il primo
/// <c>Use</c> è il più esterno: l'invocazione degli strumenti richiama la telemetria e il gateway a
/// ogni giro. Nessuna cache di risposte (spec §4.1). Il <c>FunctionInvoker</c> conta le chiamate e
/// traduce gli argomenti non validi in un errore tipizzato per il modello.
/// </summary>
internal sealed class AssistantChatClientFactory : IAssistantChatClientFactory
{
    public const string TelemetrySourceName = "MeepleAI.SessionAssistant";

    private readonly ILlmService _gateway;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _services;

    public AssistantChatClientFactory(ILlmService gateway, ILoggerFactory loggerFactory, IServiceProvider services)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IChatClient Create(AssistantModelSettings settings, ChatTurnMeter meter) =>
        new ChatClientBuilder(new GatewayChatClient(_gateway, settings, meter))
            .UseFunctionInvocation(_loggerFactory, client =>
            {
                client.MaximumIterationsPerRequest = settings.MaxToolIterations;
                client.IncludeDetailedErrors = false;
                client.FunctionInvoker = (context, cancellationToken) => InvokeToolAsync(context, meter, cancellationToken);
            })
            .UseOpenTelemetry(_loggerFactory, TelemetrySourceName)
            .Build(_services);

    /// <summary>
    /// Il binding di M.E.AI lancia <see cref="ArgumentException"/> quando manca un parametro obbligatorio
    /// o un valore non si converte, prima che lo strumento parta. Il modello riceve un errore che può
    /// correggere, e il turno lo conta.
    /// </summary>
    private static async ValueTask<object?> InvokeToolAsync(
        FunctionInvocationContext context, ChatTurnMeter meter, CancellationToken cancellationToken)
    {
        meter.RecordToolCall(context.Function.Name);
        try
        {
            return await context.Function.InvokeAsync(context.Arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException or FormatException or InvalidCastException)
        {
            meter.RecordInvalidArguments();
            return new AssistantToolError(
                "argomenti_non_validi",
                "Gli argomenti non sono validi: controlla nomi e tipi dei parametri dello strumento e riprova.");
        }
    }
}
```

`Infrastructure/DependencyInjection/SessionAssistantServiceExtensions.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.DependencyInjection;

/// <summary>ADR-097: i servizi dell'assistente in partita, in un posto solo e verificabile da un test.</summary>
internal static class SessionAssistantServiceExtensions
{
    public static IServiceCollection AddSessionAssistant(this IServiceCollection services)
    {
        services.AddScoped<IAssistantChatClientFactory, AssistantChatClientFactory>();
        return services;
    }
}
```

DI, in `KnowledgeBaseServiceExtensions.cs` subito dopo `services.AddScoped<ILlmService, HybridLlmService>();`:

```csharp
        // ADR-097: la catena M.E.AI dell'assistente in partita, sopra il gateway.
        services.AddSessionAssistant();
```

Telemetria, in `ObservabilityServiceExtensions.cs` dopo `.AddSource(MeepleAiActivitySources.VectorSearchSourceName)`:

```csharp
            .AddSource(Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant.AssistantChatClientFactory.TelemetrySourceName)
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GatewayChatClientTests|FullyQualifiedName~AssistantChatLoopTests|FullyQualifiedName~AssistantToolInvocationTests|FullyQualifiedName~SessionAssistantRegistrationTests|FullyQualifiedName~SessionAssistantArchitectureTests|FullyQualifiedName~LlmProviderEgressArchitectureTests"`
Expected: PASS. Annota il numero. Poi `dotnet build apps/api/src/Api/Api.csproj -c Release`: zero errori, zero avvisi nuovi.

- [ ] **Step 5: Commit** — `feat(ai): facciata M.E.AI sopra il gateway e catena dell'assistente (#<A0>)`

### Chiusura di A0

- [ ] Suite del contesto, solo unit (le Integration usano Testcontainers): `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "(BoundedContext=KnowledgeBase|BoundedContext=Architecture)&Category=Unit"`, con il numero. In un worktree alcuni test architetturali falliscono perché `.git` è un file: confronta i **nomi** dei test falliti con un run di controllo su `main-dev` intatto; conta solo la differenza. `dotnet build apps/api/src/Api/Api.csproj -c Release` senza avvisi nuovi.
- [ ] Push e PR verso `main-dev` (`gh pr create --base main-dev --body-file <file>`): cosa cambia, la garanzia del ciclo e come è dimostrata, `Closes #<A0>`. Il corpo finisce con `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- [ ] Review del diff (skill `superpowers:requesting-code-review`), correzioni, CI verde sui check richiesti, merge, branch eliminato (`git branch -D`), issue chiusa con la DoD spuntata.

---

## Fetta A1 — strumenti, esecutore di valutazione, spike

Branch nuovo da `main-dev` **dopo il merge di A0**: `feature/issue-<A1>-session-assistant-spike` (stessi comandi di P2, worktree `D:/Repositories/wt-assistant-a1`).

### Task 6: Porte, stato e strumenti dell'assistente

**Files:**
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/AssistantSessionState.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/ISessionAssistantReader.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/IAssistantProposalSink.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/IAssistantPlayerMatcher.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/AssistantRound.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Tools/SessionAssistantToolset.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Tools/AssistantToolResults.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/AssistantPlayerMatcher.cs`
- Modify: `API/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/SessionAssistantServiceExtensions.cs` (registrazione del matcher)
- Modify: `TESTS/Architecture/SessionAssistantArchitectureTests.cs` (regola sugli strumenti)
- Modify: `TESTS/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/SessionAssistantRegistrationTests.cs`
- Test: `TESTS/BoundedContexts/KnowledgeBase/Application/SessionAssistant/SessionAssistantToolsetTests.cs`
- Test: `TESTS/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/AssistantPlayerMatcherTests.cs`

**Interfaces:**
- Produces:
  - `AssistantSessionState(Guid? GameId, string? GameName, string Status, bool CanRecordScores, bool CanAdvanceTurn, IReadOnlyList<AssistantPlayer> Players, Guid? CurrentTurnPlayerId, int CurrentTurnIndex, int CurrentRound, string? CurrentPhase, IReadOnlyList<string> Dimensions, IReadOnlyList<AssistantScoreCell> Scores, IReadOnlyList<string> HouseRules)`; `AssistantPlayer(Guid Id, string Name, int Total, bool IsActive)`; `AssistantScoreCell(Guid PlayerId, int Round, string Dimension, int Value)`; `AssistantRulePassage(string Text, int? Page, string? Document)`.
  - `ISessionAssistantReader { Task<AssistantSessionState> GetStateAsync(CancellationToken); Task<IReadOnlyList<AssistantRulePassage>> SearchRulesAsync(string question, CancellationToken); }`
  - `IAssistantProposalSink { Task<ProposalReceipt> ProposeScoreAsync(ScoreProposalDraft, CancellationToken); Task<ProposalReceipt> ProposeTurnAsync(TurnProposalDraft, CancellationToken); }`; `ScoreProposalDraft(Guid PlayerId, string PlayerName, string Dimension, int Round, int Points, int CellValueBefore)`; `TurnProposalDraft(int CurrentTurnIndex, Guid? CurrentTurnPlayerId)`; `ProposalReceipt(Guid ProposalId, string Summary)`.
  - `IAssistantPlayerMatcher { AssistantPlayerMatch Match(string name, IReadOnlyList<AssistantPlayer> players); }`; `AssistantPlayerMatch(Guid? PlayerId, string? Name, bool IsAmbiguous, IReadOnlyList<string> Candidates)`.
  - `AssistantRound.Current(int currentTurnIndex, int activePlayers) : int`.
  - `SessionAssistantToolset(ISessionAssistantReader, IAssistantProposalSink, IAssistantPlayerMatcher)` con `CreateTools(bool includeRules) : IList<AITool>`, `ErrorCount`, e i metodi `GetStateAsync`, `SearchRulesAsync`, `ProposeScoreAsync`, `ProposeTurnAsync`.
  - Nomi degli strumenti (costanti `SessionAssistantToolset.StatoPartita`, `.CercaRegole`, `.ProponiPunteggio`, `.ProponiTurno`).

- [ ] **Step 1: Write the failing tests**

`SessionAssistantToolsetTests.cs`:

```csharp
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class SessionAssistantToolsetTests
{
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Giulia = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Luca = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private readonly Mock<ISessionAssistantReader> _reader = new();
    private readonly List<ScoreProposalDraft> _scores = [];
    private readonly List<TurnProposalDraft> _turns = [];
    private readonly Mock<IAssistantPlayerMatcher> _matcher = new();

    public SessionAssistantToolsetTests()
    {
        _reader.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(State());
        _matcher.Setup(m => m.Match("Marco", It.IsAny<IReadOnlyList<AssistantPlayer>>()))
            .Returns(new AssistantPlayerMatch(Marco, "Marco", false, []));
    }

    private SessionAssistantToolset Toolset()
    {
        var sink = new Mock<IAssistantProposalSink>();
        sink.Setup(s => s.ProposeScoreAsync(It.IsAny<ScoreProposalDraft>(), It.IsAny<CancellationToken>()))
            .Callback<ScoreProposalDraft, CancellationToken>((d, _) => _scores.Add(d))
            .ReturnsAsync(new ProposalReceipt(Guid.NewGuid(), "+5 a Marco"));
        sink.Setup(s => s.ProposeTurnAsync(It.IsAny<TurnProposalDraft>(), It.IsAny<CancellationToken>()))
            .Callback<TurnProposalDraft, CancellationToken>((d, _) => _turns.Add(d))
            .ReturnsAsync(new ProposalReceipt(Guid.NewGuid(), "passa il turno"));
        return new SessionAssistantToolset(_reader.Object, sink.Object, _matcher.Object);
    }

    private static AssistantSessionState State(
        string status = "InProgress", IReadOnlyList<string>? dimensions = null, Guid? gameId = null) => new(
        GameId: gameId ?? Guid.Parse("00000000-0000-0000-0000-0000000000aa"),
        GameName: "Azul",
        Status: status,
        CanRecordScores: status is "InProgress" or "Paused",
        CanAdvanceTurn: status == "InProgress",
        Players: [new(Marco, "Marco", 7, true), new(Giulia, "Giulia", 12, true), new(Luca, "Luca", 0, true)],
        CurrentTurnPlayerId: Luca,
        CurrentTurnIndex: 5,
        CurrentRound: AssistantRound.Current(5, 3),
        CurrentPhase: null,
        Dimensions: dimensions ?? ["punti"],
        Scores: [new(Marco, 2, "punti", 7)],
        HouseRules: []);

    [Theory]
    [InlineData(0, 3, 1)]
    [InlineData(2, 3, 1)]
    [InlineData(3, 3, 2)]
    [InlineData(5, 3, 2)]
    [InlineData(6, 3, 3)]
    [InlineData(4, 0, 1)]
    public void Round_IsAFullTurnOfTheTable(int turnIndex, int activePlayers, int expected) =>
        AssistantRound.Current(turnIndex, activePlayers).Should().Be(expected);

    [Fact]
    public async Task ProposeScore_ResolvesPlayerAndCurrentRound_AndRecordsTheDeltaWithTheCellBefore()
    {
        var result = await Toolset().ProposeScoreAsync("Marco", 5, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeOfType<ProposalCreated>();
        _scores.Should().ContainSingle().Which.Should().Be(new ScoreProposalDraft(Marco, "Marco", "punti", 2, 5, 7));
    }

    [Fact]
    public async Task ProposeScore_NegativePoints_IsAValidDelta()
    {
        await Toolset().ProposeScoreAsync("Marco", -2, cancellationToken: TestContext.Current.CancellationToken);

        _scores.Should().ContainSingle().Which.Points.Should().Be(-2);
    }

    [Fact]
    public async Task ProposeScore_UsesTheMatcherNotAnExactComparison()
    {
        _matcher.Setup(m => m.Match(" marco ", It.IsAny<IReadOnlyList<AssistantPlayer>>()))
            .Returns(new AssistantPlayerMatch(Marco, "Marco", false, []));

        await Toolset().ProposeScoreAsync(" marco ", 3, cancellationToken: TestContext.Current.CancellationToken);

        _scores.Should().ContainSingle().Which.PlayerId.Should().Be(Marco);
    }

    [Fact]
    public async Task ProposeScore_EmptyName_IsATypedError()
    {
        var toolset = Toolset();

        var result = await toolset.ProposeScoreAsync("  ", 3, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeOfType<AssistantToolError>().Which.Error.Should().Be("giocatore_mancante");
        _scores.Should().BeEmpty();
        _matcher.Verify(m => m.Match(It.IsAny<string>(), It.IsAny<IReadOnlyList<AssistantPlayer>>()), Times.Never,
            "il servizio di risoluzione lancia su un nome vuoto");
    }

    [Fact]
    public async Task ProposeScore_AmbiguousPlayer_ReturnsTheCandidates_WithoutAProposal()
    {
        _matcher.Setup(m => m.Match("Ma", It.IsAny<IReadOnlyList<AssistantPlayer>>()))
            .Returns(new AssistantPlayerMatch(null, null, true, ["Marco", "Mara"]));
        var toolset = Toolset();

        var result = await toolset.ProposeScoreAsync("Ma", 3, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeOfType<AssistantToolError>().Which.Candidates.Should().Equal("Marco", "Mara");
        _scores.Should().BeEmpty();
        toolset.ErrorCount.Should().Be(1);
    }

    [Fact]
    public async Task ProposeScore_SeveralDimensionsAndNoneGiven_AsksWhichOne()
    {
        _reader.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(State(dimensions: ["punti", "monete"]));

        var result = await Toolset().ProposeScoreAsync("Marco", 3, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeOfType<AssistantToolError>().Which.Error.Should().Be("dimensione_richiesta");
        _scores.Should().BeEmpty();
    }

    [Fact]
    public async Task ProposeScore_SessionCompleted_IsRefused()
    {
        _reader.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(State(status: "Completed"));

        var result = await Toolset().ProposeScoreAsync("Marco", 3, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeOfType<AssistantToolError>().Which.Error.Should().Be("sessione_non_modificabile");
        _scores.Should().BeEmpty();
    }

    [Fact]
    public async Task ProposeTurn_RecordsTheExpectedTurn_AndIsRefusedWhenPaused()
    {
        await Toolset().ProposeTurnAsync(TestContext.Current.CancellationToken);
        _turns.Should().ContainSingle().Which.Should().Be(new TurnProposalDraft(5, Luca));

        _reader.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(State(status: "Paused"));
        var paused = await Toolset().ProposeTurnAsync(TestContext.Current.CancellationToken);
        paused.Should().BeOfType<AssistantToolError>();
    }

    [Fact]
    public void CreateTools_NamesAndRequiredParameters()
    {
        var tools = Toolset().CreateTools(includeRules: true).OfType<AIFunctionDeclaration>().ToList();

        tools.Select(t => t.Name).Should().Equal("stato_partita", "cerca_regole", "proponi_punteggio", "proponi_turno");
        var required = tools.Single(t => t.Name == "proponi_punteggio").JsonSchema.GetProperty("required")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        required.Should().BeEquivalentTo(["giocatore", "punti"]);
    }

    [Fact]
    public void CreateTools_WithoutAGame_DoesNotOfferTheRules() =>
        Toolset().CreateTools(includeRules: false).Select(t => t.Name).Should().NotContain("cerca_regole");
}
```

`AssistantPlayerMatcherTests.cs` — l'adapter con il servizio vero della partita:

```csharp
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>Spec §5.1: l'assistente risolve i nomi come la partita (esatto, nome di battesimo, contenimento).</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AssistantPlayerMatcherTests
{
    private static readonly AssistantPlayer MarcoRossi = new(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Marco Rossi", 0, true);
    private static readonly AssistantPlayer MarcoBianchi = new(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Marco Bianchi", 0, true);
    private static readonly AssistantPlayer Anna = new(Guid.Parse("00000000-0000-0000-0000-000000000003"), "Anna", 0, true);

    private readonly AssistantPlayerMatcher _matcher = new(new PlayerNameResolutionService());

    [Fact]
    public void Match_IgnoresCase() =>
        _matcher.Match("anna", [MarcoRossi, Anna]).PlayerId.Should().Be(Anna.Id);

    [Fact]
    public void Match_SharedFirstName_IsAmbiguous_WithBothCandidates()
    {
        var match = _matcher.Match("Marco", [MarcoRossi, MarcoBianchi, Anna]);

        match.IsAmbiguous.Should().BeTrue();
        match.Candidates.Should().BeEquivalentTo(["Marco Rossi", "Marco Bianchi"]);
    }

    [Fact]
    public void Match_UnknownName_IsNeitherResolvedNorAmbiguous()
    {
        var match = _matcher.Match("Paolo", [MarcoRossi, Anna]);

        match.PlayerId.Should().BeNull();
        match.IsAmbiguous.Should().BeFalse();
    }
}
```

Se `Match_SharedFirstName_IsAmbiguous_WithBothCandidates` fallisce, il servizio risolve diversamente dalla descrizione «exact, first-name, contains» della sua interfaccia: leggi `PlayerNameResolutionService` e allinea le attese del test e dello scenario `it-score-ambiguous` (Task 9) al comportamento vero, senza cambiare il servizio.

In `SessionAssistantRegistrationTests.AddSessionAssistant_ResolvesEveryService`, aggiungi prima di `services.AddSessionAssistant();`:

```csharp
        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.GameManagement.Application.Services.IPlayerNameResolutionService>());
```

e dopo l'ultima asserzione:

```csharp
        scope.ServiceProvider.GetRequiredService<IAssistantPlayerMatcher>().Should().NotBeNull();
```

Aggiungi a `SessionAssistantArchitectureTests`:

```csharp
    [Fact]
    public void Tools_DoNotDependOnTheMediatorOrOnGameManagement()
    {
        var toolTypes = typeof(Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools.SessionAssistantToolset).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools")
            .ToList();

        toolTypes.Should().NotBeEmpty();
        foreach (var type in toolTypes)
        {
            var dependencies = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
                .Concat(type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Select(f => f.FieldType))
                .ToList();

            dependencies.Should().NotContain(typeof(MediatR.IMediator), $"{type.Name}: gli strumenti non scrivono (spec §3.2)");
            dependencies.Should().NotContain(typeof(MediatR.ISender));
            dependencies.Should().NotContain(t => t.Namespace != null && t.Namespace.StartsWith("Api.BoundedContexts.GameManagement", StringComparison.Ordinal));
        }
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build apps/api/tests/Api.Tests/Api.Tests.csproj`
Expected: FAIL a compilazione — i tipi nuovi non esistono.

- [ ] **Step 3: Write minimal implementation**

`AssistantSessionState.cs`:

```csharp
#pragma warning disable MA0048 // File name must match type name - the assistant's view of a session
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Lo stato di una partita come lo vede l'assistente (spec §5.1). Lo costruisce un
/// <see cref="ISessionAssistantReader"/>: dalla sessione live in produzione, da uno scenario nello spike.
/// </summary>
internal sealed record AssistantSessionState(
    Guid? GameId,
    string? GameName,
    string Status,
    bool CanRecordScores,
    bool CanAdvanceTurn,
    IReadOnlyList<AssistantPlayer> Players,
    Guid? CurrentTurnPlayerId,
    int CurrentTurnIndex,
    int CurrentRound,
    string? CurrentPhase,
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<AssistantScoreCell> Scores,
    IReadOnlyList<string> HouseRules);

internal sealed record AssistantPlayer(Guid Id, string Name, int Total, bool IsActive);

internal sealed record AssistantScoreCell(Guid PlayerId, int Round, string Dimension, int Value);

internal sealed record AssistantRulePassage(string Text, int? Page, string? Document);
```

`ISessionAssistantReader.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// La porta di sola lettura degli strumenti (spec §3.2). La sessione è legata all'istanza dal server:
/// gli strumenti non ricevono un id di sessione e il modello non può sceglierne un'altra.
/// </summary>
internal interface ISessionAssistantReader
{
    Task<AssistantSessionState> GetStateAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AssistantRulePassage>> SearchRulesAsync(string question, CancellationToken cancellationToken);
}
```

`IAssistantProposalSink.cs`:

```csharp
#pragma warning disable MA0048 // File name must match type name - the proposal port and its drafts
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>L'unica uscita degli strumenti di proposta: salva una proposta, non scrive la partita (spec §5.2).</summary>
internal interface IAssistantProposalSink
{
    Task<ProposalReceipt> ProposeScoreAsync(ScoreProposalDraft draft, CancellationToken cancellationToken);

    Task<ProposalReceipt> ProposeTurnAsync(TurnProposalDraft draft, CancellationToken cancellationToken);
}

/// <summary>Un incremento di punti sulla cella (giocatore, round, dimensione), con il valore della cella al momento.</summary>
internal sealed record ScoreProposalDraft(Guid PlayerId, string PlayerName, string Dimension, int Round, int Points, int CellValueBefore);

internal sealed record TurnProposalDraft(int CurrentTurnIndex, Guid? CurrentTurnPlayerId);

internal sealed record ProposalReceipt(Guid ProposalId, string Summary);
```

`IAssistantPlayerMatcher.cs`:

```csharp
#pragma warning disable MA0048 // File name must match type name - matcher and its result
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>Risolve un nome detto dall'host fra i giocatori della sessione, come lo risolve oggi la partita.</summary>
internal interface IAssistantPlayerMatcher
{
    AssistantPlayerMatch Match(string name, IReadOnlyList<AssistantPlayer> players);
}

internal sealed record AssistantPlayerMatch(Guid? PlayerId, string? Name, bool IsAmbiguous, IReadOnlyList<string> Candidates);
```

`AssistantRound.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Il round derivato (spec §5.1): un giro completo del tavolo. <c>LiveGameSession</c> ha solo
/// l'indice di turno, che cresce a ogni turno di ogni giocatore.
/// </summary>
internal static class AssistantRound
{
    public static int Current(int currentTurnIndex, int activePlayers) =>
        activePlayers <= 0 ? 1 : (Math.Max(currentTurnIndex, 0) / activePlayers) + 1;
}
```

`Tools/AssistantToolResults.cs` (l'errore tipizzato `AssistantToolError` è già del Task 5, nel namespace padre):

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools;

/// <summary>Una proposta salvata: non è ancora registrata, lo diventa con la conferma dell'host.</summary>
internal sealed record ProposalCreated(Guid ProposalId, string Summary, string Note);
```

`Tools/SessionAssistantToolset.cs`:

```csharp
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools;

/// <summary>
/// Gli strumenti dell'assistente in partita (spec §5.1). Leggono solo da <see cref="ISessionAssistantReader"/>
/// e propongono solo attraverso <see cref="IAssistantProposalSink"/>: il modello non scrive mai la partita.
/// Un'istanza per turno.
/// </summary>
internal sealed class SessionAssistantToolset
{
    public const string StatoPartita = "stato_partita";
    public const string CercaRegole = "cerca_regole";
    public const string ProponiPunteggio = "proponi_punteggio";
    public const string ProponiTurno = "proponi_turno";

    private const string PendingNote = "Proposta in attesa di conferma dell'host: NON è ancora registrata.";

    /// <summary>Risultati compatti: il JSON indentato di default gonfia i token di ogni giro.</summary>
    private static readonly JsonSerializerOptions ToolJson = CreateToolJson();

    private readonly ISessionAssistantReader _reader;
    private readonly IAssistantProposalSink _sink;
    private readonly IAssistantPlayerMatcher _matcher;
    private int _errorCount;

    public SessionAssistantToolset(ISessionAssistantReader reader, IAssistantProposalSink sink, IAssistantPlayerMatcher matcher)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
    }

    /// <summary>Quante volte uno strumento ha risposto con un errore in questo turno.</summary>
    public int ErrorCount => _errorCount;

    public IList<AITool> CreateTools(bool includeRules)
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(GetStateAsync, StatoPartita,
                "Legge lo stato attuale della partita: giocatori e totali, giocatore di turno, round, fase, dimensioni del punteggio e punteggi per round. Chiamalo prima di rispondere su punteggi o turni.",
                ToolJson),
        };
        if (includeRules)
        {
            tools.Add(AIFunctionFactory.Create(SearchRulesAsync, CercaRegole,
                "Cerca nel regolamento del gioco i passaggi utili a una domanda sulle regole. Rispondi solo con ciò che trovi, citando la pagina.",
                ToolJson));
        }

        tools.Add(AIFunctionFactory.Create(ProposeScoreAsync, ProponiPunteggio,
            "Propone di aggiungere dei punti a un giocatore. NON registra: crea una proposta che l'host conferma.",
            ToolJson));
        tools.Add(AIFunctionFactory.Create(ProposeTurnAsync, ProponiTurno,
            "Propone di passare il turno al giocatore successivo. NON registra: crea una proposta che l'host conferma.",
            ToolJson));
        return tools;
    }

    private static JsonSerializerOptions CreateToolJson()
    {
        var options = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions) { WriteIndented = false };
        options.MakeReadOnly();
        return options;
    }

    public Task<AssistantSessionState> GetStateAsync(CancellationToken cancellationToken = default) =>
        _reader.GetStateAsync(cancellationToken);

    public Task<IReadOnlyList<AssistantRulePassage>> SearchRulesAsync(
        [Description("La domanda sulle regole, con parole proprie")] string domanda,
        CancellationToken cancellationToken = default) =>
        _reader.SearchRulesAsync(domanda, cancellationToken);

    public async Task<object> ProposeScoreAsync(
        [Description("Il nome del giocatore, come l'ha detto l'host")] string giocatore,
        [Description("I punti guadagnati adesso: un incremento, anche negativo")] int punti,
        [Description("La dimensione del punteggio; omettila se la partita ne ha una sola")] string? dimensione = null,
        [Description("Il round; omettilo per il round corrente")] int? round = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(giocatore))
        {
            return Error("giocatore_mancante", "Serve il nome del giocatore: chiedilo all'host.");
        }

        var state = await _reader.GetStateAsync(cancellationToken).ConfigureAwait(false);
        if (!state.CanRecordScores)
        {
            return Error("sessione_non_modificabile", $"La partita è {state.Status}: i punteggi non si possono cambiare.");
        }

        var match = _matcher.Match(giocatore, state.Players);
        if (match.IsAmbiguous)
        {
            return Error("giocatore_ambiguo", $"«{giocatore}» corrisponde a più giocatori: chiedi all'host quale.", match.Candidates);
        }

        if (match.PlayerId is not { } playerId)
        {
            return Error("giocatore_sconosciuto", $"Nessun giocatore si chiama «{giocatore}».", state.Players.Select(p => p.Name).ToList());
        }

        var resolvedDimension = ResolveDimension(dimensione, state.Dimensions);
        if (resolvedDimension is null)
        {
            return dimensione is null
                ? Error("dimensione_richiesta", "La partita ha più dimensioni di punteggio: chiedi all'host quale.", state.Dimensions)
                : Error("dimensione_sconosciuta", $"La dimensione «{dimensione}» non esiste.", state.Dimensions);
        }

        var targetRound = round ?? state.CurrentRound;
        if (targetRound < 1)
        {
            return Error("round_non_valido", "Il round parte da 1.");
        }

        var before = state.Scores
            .Where(s => s.PlayerId == playerId && s.Round == targetRound
                && string.Equals(s.Dimension, resolvedDimension, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Value)
            .FirstOrDefault();
        var receipt = await _sink.ProposeScoreAsync(
            new ScoreProposalDraft(playerId, match.Name ?? giocatore, resolvedDimension, targetRound, punti, before),
            cancellationToken).ConfigureAwait(false);
        return new ProposalCreated(receipt.ProposalId, receipt.Summary, PendingNote);
    }

    public async Task<object> ProposeTurnAsync(CancellationToken cancellationToken = default)
    {
        var state = await _reader.GetStateAsync(cancellationToken).ConfigureAwait(false);
        if (!state.CanAdvanceTurn)
        {
            return Error("sessione_non_in_corso", $"La partita è {state.Status}: il turno passa solo a partita in corso.");
        }

        var receipt = await _sink.ProposeTurnAsync(
            new TurnProposalDraft(state.CurrentTurnIndex, state.CurrentTurnPlayerId), cancellationToken).ConfigureAwait(false);
        return new ProposalCreated(receipt.ProposalId, receipt.Summary, PendingNote);
    }

    private AssistantToolError Error(string code, string message, IReadOnlyList<string>? candidates = null)
    {
        Interlocked.Increment(ref _errorCount);
        return new AssistantToolError(code, message, candidates);
    }

    private static string? ResolveDimension(string? requested, IReadOnlyList<string> dimensions)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return dimensions.Count == 1 ? dimensions[0] : null;
        }

        return dimensions.FirstOrDefault(d => string.Equals(d, requested.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
```

`Infrastructure/SessionAssistant/AssistantPlayerMatcher.cs`:

```csharp
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// Adapter verso la risoluzione dei nomi della partita (esatta, nome di battesimo, contenimento): gli
/// strumenti non dipendono da GameManagement (spec §3.2), ma risolvono i nomi allo stesso modo.
/// </summary>
internal sealed class AssistantPlayerMatcher(IPlayerNameResolutionService resolution) : IAssistantPlayerMatcher
{
    public AssistantPlayerMatch Match(string name, IReadOnlyList<AssistantPlayer> players)
    {
        var result = resolution.ResolvePlayer(name, players.ToDictionary(p => p.Id, p => p.Name));
        if (result.IsAmbiguous)
        {
            return new AssistantPlayerMatch(null, null, true, result.Candidates.Select(c => c.Name).ToList());
        }

        return result.IsResolved
            ? new AssistantPlayerMatch(result.PlayerId, result.ResolvedName, false, [])
            : new AssistantPlayerMatch(null, null, false, []);
    }
}
```

In `SessionAssistantServiceExtensions.AddSessionAssistant`, sotto la registrazione della factory:

```csharp
        services.AddScoped<IAssistantPlayerMatcher, AssistantPlayerMatcher>();
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SessionAssistantToolsetTests|FullyQualifiedName~AssistantPlayerMatcherTests|FullyQualifiedName~SessionAssistantRegistrationTests|FullyQualifiedName~SessionAssistantArchitectureTests"`
Expected: PASS. Annota il numero.

- [ ] **Step 5: Commit** — `feat(ai): strumenti dell'assistente in partita su porte di sola lettura (#<A1>)`

---

### Task 7: Prompt della prima versione, conversazione ordinata e rilevatore di azioni dichiarate

**Files:**
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/SessionAssistantPrompts.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/AssistantConversation.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/ClaimedActionDetector.cs`
- Test: `TESTS/BoundedContexts/KnowledgeBase/Application/SessionAssistant/AssistantConversationTests.cs`

**Interfaces:**
- Consumes: `AssistantSessionState` (Task 6).
- Produces: `SessionAssistantPrompts.For(string language) : string`; `AssistantHistoryTurn(string Role, string Text)`; `AssistantConversation.Build(string language, AssistantSessionState state, IReadOnlyList<AssistantHistoryTurn> history, string question) : List<ChatMessage>`; `ClaimedActionDetector.ClaimsAction(string text) : bool`.

- [ ] **Step 1: Write the failing tests**

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AssistantConversationTests
{
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static AssistantSessionState State() => new(
        Guid.Parse("00000000-0000-0000-0000-0000000000aa"), "Azul", "InProgress", true, true,
        [new(Marco, "Marco", 37, true)], Marco, 4, 5, "Acquisto", ["punti"],
        [new(Marco, 1, "punti", 37)], ["Si pesca una tessera in più al primo turno."]);

    [Fact]
    public void Build_OrdersFromTheMostStableToTheMostVolatile()
    {
        var messages = AssistantConversation.Build("it", State(),
            [new AssistantHistoryTurn("user", "chi tocca?"), new AssistantHistoryTurn("assistant", "Tocca a Marco.")],
            "Marco fa 5 punti");

        messages.Select(m => m.Role).Should().Equal(ChatRole.System, ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.User);
        messages[0].Text.Should().Be(SessionAssistantPrompts.For("it"));
        messages[^1].Text.Should().Be("Marco fa 5 punti");
    }

    [Fact]
    public void Build_StaticContextHasGamePlayersDimensionsAndHouseRules_ButNoScoresTurnOrPhase()
    {
        var context = AssistantConversation.Build("it", State(), [], "x")[1].Text;

        context.Should().Contain("Azul").And.Contain("Marco").And.Contain("punti").And.Contain("tessera in più");
        context.Should().NotContain("37", "i punteggi arrivano solo da stato_partita (spec §4.1)");
        context.Should().NotContain("Acquisto");
    }

    [Fact]
    public void Prompts_ExistForItalianAndEnglish_AndFallBackToItalian()
    {
        SessionAssistantPrompts.For("en").Should().Contain("never");
        SessionAssistantPrompts.For("de").Should().Be(SessionAssistantPrompts.For("it"));
    }

    [Theory]
    [InlineData("Ho segnato 5 punti a Marco.", true)]
    [InlineData("Ho registrato il punteggio.", true)]
    [InlineData("Ho passato il turno a Giulia.", true)]
    [InlineData("I've recorded 5 points for Marco.", true)]
    [InlineData("I added 3 points to Giulia.", true)]
    [InlineData("Ti propongo +5 a Marco: conferma la card.", false)]
    [InlineData("Marco ha 12 punti.", false)]
    [InlineData("I prepared a proposal: please confirm it.", false)]
    public void ClaimedActionDetector_FlagsCompletedActions(string text, bool expected) =>
        ClaimedActionDetector.ClaimsAction(text).Should().Be(expected);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build apps/api/tests/Api.Tests/Api.Tests.csproj`
Expected: FAIL a compilazione.

- [ ] **Step 3: Write minimal implementation**

`SessionAssistantPrompts.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Il prompt di sistema della prima versione (spec §13, domanda 5). Nella fetta A2 diventa il seme della
/// sezione «Assistente in partita» del profilo; lo spike lo misura così com'è.
/// </summary>
internal static class SessionAssistantPrompts
{
    public const string Italian = """
        Sei l'assistente di MeepleAI al tavolo di gioco e aiuti l'host di una partita in corso.
        Regole:
        1. Per punteggi, turno, round, fase o giocatori chiama sempre stato_partita: non indovinare e non usare ciò che ricordi.
        2. Per una domanda sulle regole chiama cerca_regole e rispondi solo con ciò che trovi, citando la pagina. Se non trovi la risposta, dillo.
        3. Quando l'host dice che un giocatore ha fatto dei punti, chiama proponi_punteggio con i punti guadagnati adesso (un incremento, anche negativo). Se l'host dice un totale, calcola l'incremento da stato_partita.
        4. Quando l'host dice di passare il turno, chiama proponi_turno.
        5. Tu non registri mai niente: crei solo proposte che l'host conferma. Non dire mai «ho segnato», «ho registrato» o «ho passato il turno»: di' che hai preparato una proposta da confermare.
        6. Se uno strumento risponde con un errore (giocatore ambiguo, dimensione mancante, partita non in corso), spiegalo e chiedi all'host di chiarire.
        7. Rispondi in modo breve, in italiano.
        """;

    public const string English = """
        You are MeepleAI's assistant at the game table, helping the host of a game in progress.
        Rules:
        1. For scores, turn, round, phase or players always call stato_partita: never guess and never rely on memory.
        2. For a rules question call cerca_regole and answer only with what you find, citing the page. If you find nothing, say so.
        3. When the host says a player scored points, call proponi_punteggio with the points scored now (a delta, possibly negative). If the host gives a total, compute the delta from stato_partita.
        4. When the host says to pass the turn, call proponi_turno.
        5. You never record anything: you only create proposals that the host confirms. Never say "I recorded", "I added" or "I passed the turn": say you prepared a proposal to confirm.
        6. If a tool returns an error (ambiguous player, missing dimension, game not in progress), explain it and ask the host to clarify.
        7. Answer briefly, in English.
        """;

    public static string For(string language) =>
        string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? English : Italian;
}
```

`AssistantConversation.cs`:

```csharp
using System.Text;
using Microsoft.Extensions.AI;

#pragma warning disable MA0048 // File name must match type name - conversation builder and history turn
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>Un turno passato della conversazione: solo il testo (spec §6.2).</summary>
internal sealed record AssistantHistoryTurn(string Role, string Text);

/// <summary>
/// I messaggi di un turno, dal più stabile al più volatile (spec §4.1): prompt di sistema, contesto
/// statico della sessione, storia, domanda. Punteggi, turno e fase non entrano mai qui: arrivano solo
/// dai risultati di stato_partita, così l'inizio del prompt resta uguale fra i giri e i turni.
/// </summary>
internal static class AssistantConversation
{
    public static List<ChatMessage> Build(
        string language, AssistantSessionState state, IReadOnlyList<AssistantHistoryTurn> history, string question)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SessionAssistantPrompts.For(language)),
            new(ChatRole.System, StaticContext(state)),
        };
        messages.AddRange(history.Select(h => new ChatMessage(
            string.Equals(h.Role, "assistant", StringComparison.Ordinal) ? ChatRole.Assistant : ChatRole.User, h.Text)));
        messages.Add(new ChatMessage(ChatRole.User, question));
        return messages;
    }

    private static string StaticContext(AssistantSessionState state)
    {
        var text = new StringBuilder();
        text.AppendLine($"Gioco: {state.GameName ?? "(nessun gioco collegato)"}");
        text.AppendLine($"Giocatori: {string.Join(", ", state.Players.Select(p => p.Name))}");
        text.AppendLine($"Dimensioni del punteggio: {string.Join(", ", state.Dimensions)}");
        if (state.HouseRules.Count > 0)
        {
            text.AppendLine("House rules (valgono sul regolamento):");
            foreach (var rule in state.HouseRules)
            {
                text.AppendLine($"- {rule}");
            }
        }

        return text.ToString().TrimEnd();
    }
}
```

`ClaimedActionDetector.cs`:

```csharp
using System.Text.RegularExpressions;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Il rischio più insidioso (spec §6.3): il modello dice di aver fatto un'azione che ha solo proposto, o
/// che non ha nemmeno proposto. Riconosce le affermazioni al passato prossimo di registrazione.
/// </summary>
internal static partial class ClaimedActionDetector
{
    public static bool ClaimsAction(string text) => !string.IsNullOrWhiteSpace(text) && ClaimPattern().IsMatch(text);

    [GeneratedRegex(
        @"\b(ho|abbiamo)\s+(segnato|registrato|aggiunto|tolto|passato|aggiornato)\b|\b(I've|I have|I)\s+(recorded|added|removed|passed|updated|logged)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex ClaimPattern();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AssistantConversationTests"`
Expected: PASS. Annota il numero.

- [ ] **Step 5: Commit** — `feat(ai): prompt v1 e conversazione ordinata dell'assistente (#<A1>)`

---

### Task 8: Esecutore di valutazione ed endpoint admin

**Files:**
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Evaluation/AssistantScenario.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Evaluation/ScenarioSessionAssistantReader.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Evaluation/RecordingProposalSink.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Evaluation/SessionAssistantEvaluator.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Evaluation/AssistantEvaluationReport.cs`
- Create: `API/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Evaluation/RunSessionAssistantEvaluationCommand.cs` (comando, validator, handler)
- Create: `API/Routing/AdminSessionAssistantEndpoints.cs`
- Modify: `API/Program.cs` (dopo `v1Api.MapAdminLlmConfigEndpoints();`)
- Modify: `API/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/SessionAssistantServiceExtensions.cs` (registrazione dell'evaluator)
- Modify: `TESTS/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/SessionAssistantRegistrationTests.cs`
- Test: `TESTS/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Evaluation/SessionAssistantEvaluatorTests.cs`

**Interfaces:**
- Consumes: Task 5 (`IAssistantChatClientFactory`, `AssistantModelSettings`, `ChatTurnMeter`, `AssistantGatewayException`), Task 6 (toolset e porte), Task 7 (`AssistantConversation`, `ClaimedActionDetector`).
- Produces: `AssistantScenarioSet`, `AssistantScenario`, `AssistantScenarioState`, `AssistantScenarioScore`, `AssistantScenarioPassage`, `AssistantExpectation`, `ExpectedProposal`; `SessionAssistantEvaluator.RunAsync(AssistantScenario, AssistantModelSettings, CancellationToken) : Task<AssistantScenarioResult>`; `AssistantEvaluationReport.From(string model, IReadOnlyList<AssistantScenarioResult>)`; `RunSessionAssistantEvaluationCommand`; rotta `POST /api/v1/admin/session-assistant/evaluations`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

/// <summary>Spec §7.1: l'esecutore usa il ciclo vero, con un gateway finto che recita il modello.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class SessionAssistantEvaluatorTests
{
    private static readonly AssistantModelSettings Settings = new("deepseek-flash", null, 0.2, 600, 4, null, RequestSource.Evaluation);

    private static AssistantScenario Scenario(params ExpectedProposal[] proposals) => new(
        Id: "it-score-simple", Language: "it", Category: "punteggio",
        State: new AssistantScenarioState(
            Game: "Azul", HasRules: true, Status: "InProgress", Players: ["Marco", "Giulia", "Luca"],
            CurrentTurnIndex: 4, CurrentPlayer: "Luca", Phase: null, Dimensions: ["punti"],
            Scores: [new AssistantScenarioScore("Marco", 1, "punti", 7)], HouseRules: []),
        Passages: [],
        Utterance: "Marco ha fatto 5 punti",
        Expected: new AssistantExpectation(["proponi_punteggio"], proposals));

    [Fact]
    public async Task Run_ModelProposesTheRightScore_Passes()
    {
        var gateway = Script(
            Calls(("proponi_punteggio", """{"giocatore":"Marco","punti":5}""")),
            Answer("Ho preparato la proposta: +5 a Marco. Confermala."));

        var result = await Evaluator(gateway).RunAsync(
            Scenario(new ExpectedProposal("Score", "Marco", "punti", 2, 5)), Settings, TestContext.Current.CancellationToken);

        result.Passed.Should().BeTrue();
        result.ToolsCalled.Should().Equal("proponi_punteggio");
        result.RoundTrips.Should().Be(2);
    }

    [Fact]
    public async Task Run_ModelClaimsTheActionWithoutATool_FailsWithClaimAndMissingProposal()
    {
        var gateway = Script(Answer("Ho segnato 5 punti a Marco."));

        var result = await Evaluator(gateway).RunAsync(
            Scenario(new ExpectedProposal("Score", "Marco", "punti", 2, 5)), Settings, TestContext.Current.CancellationToken);

        result.Passed.Should().BeFalse();
        result.ClaimedActionWithoutProposal.Should().BeTrue();
        result.MissingProposals.Should().Be(1);
        result.MissingTools.Should().Equal("proponi_punteggio");
    }

    [Fact]
    public async Task Run_GatewayRefuses_IsAnErrorResult_NotAnException()
    {
        var gateway = new Mock<ILlmService>();
        gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("cap") with
            {
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["error_code"] = LlmBudgetAdmission.DailyBudgetErrorCode },
            });

        var result = await Evaluator(gateway).RunAsync(Scenario(), Settings, TestContext.Current.CancellationToken);

        result.Passed.Should().BeFalse();
        result.ErrorCode.Should().Be(LlmBudgetAdmission.DailyBudgetErrorCode);
    }

    [Fact]
    public void Report_AggregatesPassRateCostAndP95()
    {
        var results = Enumerable.Range(1, 20).Select(i => new AssistantScenarioResult(
            $"s{i}", "punteggio", Passed: i % 4 != 0, ToolsCalled: [], MissingTools: [], ExtraProposals: i == 4 ? 1 : 0,
            MissingProposals: 0, ClaimedActionWithoutProposal: false, ToolErrors: 0, InvalidArguments: i == 8 ? 2 : 0, RoundTrips: 2,
            CostUsd: 0.001m, LatencyMs: i * 100, ErrorCode: null, FinalText: "ok")).ToList();

        var report = AssistantEvaluationReport.From("deepseek-flash", results);

        report.Scenarios.Should().Be(20);
        report.Passed.Should().Be(15);
        report.ScenariosWithExtraProposals.Should().Be(1);
        report.InvalidArguments.Should().Be(2);
        report.AverageRoundTrips.Should().Be(2);
        report.AverageCostPerTurnUsd.Should().Be(0.001m);
        report.LatencyP95Ms.Should().Be(1900);
    }

    [Fact]
    public void ScenarioSet_InTheRepository_IsValidJson()
    {
        var path = Path.Combine(FindRepoRoot(), "tests", "llm-eval", "session-assistant", "scenarios.json");
        var set = JsonSerializer.Deserialize<AssistantScenarioSet>(File.ReadAllText(path), AssistantScenarioSet.Json);

        set!.Scenarios.Should().NotBeEmpty();
        set.Scenarios.Select(s => s.Id).Should().OnlyHaveUniqueItems();
        set.Scenarios.Should().OnlyContain(s => s.Language == "it" || s.Language == "en");
    }

    private static SessionAssistantEvaluator Evaluator(Mock<ILlmService> gateway) =>
        new(new AssistantChatClientFactory(gateway.Object, NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider()),
            new ExactNameMatcher());

    private static Mock<ILlmService> Script(params LlmCompletionResult[] turns)
    {
        var gateway = new Mock<ILlmService>();
        var sequence = gateway.SetupSequence(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()));
        foreach (var turn in turns)
        {
            sequence = sequence.ReturnsAsync(turn);
        }

        return gateway;
    }

    private static LlmCompletionResult Calls(params (string Name, string Args)[] calls) =>
        LlmCompletionResult.CreateSuccess(string.Empty, new LlmUsage(100, 5, 105)) with
        {
            ToolCalls = calls.Select((c, i) => new LlmToolCall($"call_{i}", c.Name, c.Args)).ToList(),
        };

    private static LlmCompletionResult Answer(string text) => LlmCompletionResult.CreateSuccess(text, new LlmUsage(120, 10, 130));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests", "llm-eval")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("repository root with tests/llm-eval not found");
    }

    /// <summary>Un matcher minimo per i test dell'esecutore: confronto esatto, senza maiuscole.</summary>
    private sealed class ExactNameMatcher : IAssistantPlayerMatcher
    {
        public AssistantPlayerMatch Match(string name, IReadOnlyList<AssistantPlayer> players)
        {
            var found = players.Where(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            return found.Count == 1
                ? new AssistantPlayerMatch(found[0].Id, found[0].Name, false, [])
                : new AssistantPlayerMatch(null, null, found.Count > 1, found.Select(p => p.Name).ToList());
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build apps/api/tests/Api.Tests/Api.Tests.csproj`
Expected: FAIL a compilazione.

- [ ] **Step 3: Write minimal implementation**

`Evaluation/AssistantScenario.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable MA0048 // File name must match type name - scenario file model
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

/// <summary>Il set di scenari dello spike (spec §7.1), in <c>tests/llm-eval/session-assistant/scenarios.json</c>.</summary>
internal sealed record AssistantScenarioSet(int Version, IReadOnlyList<AssistantScenario> Scenarios)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

internal sealed record AssistantScenario(
    string Id,
    string Language,
    string Category,
    AssistantScenarioState State,
    IReadOnlyList<AssistantScenarioPassage> Passages,
    string Utterance,
    AssistantExpectation Expected);

internal sealed record AssistantScenarioState(
    string Game,
    bool HasRules,
    string Status,
    IReadOnlyList<string> Players,
    int CurrentTurnIndex,
    string? CurrentPlayer,
    string? Phase,
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<AssistantScenarioScore> Scores,
    IReadOnlyList<string> HouseRules);

internal sealed record AssistantScenarioScore(string Player, int Round, string Dimension, int Value);

internal sealed record AssistantScenarioPassage(string Text, int? Page);

/// <summary>Strumenti che devono essere chiamati (altri sono ammessi) e proposte attese, esatte e senza ordine.</summary>
internal sealed record AssistantExpectation(IReadOnlyList<string> Tools, IReadOnlyList<ExpectedProposal> Proposals);

/// <summary><c>Kind</c> è <c>Score</c> o <c>AdvanceTurn</c>; per <c>AdvanceTurn</c> gli altri campi sono nulli.</summary>
internal sealed record ExpectedProposal(string Kind, string? Player, string? Dimension, int? Round, int? Points);
```

`Evaluation/ScenarioSessionAssistantReader.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

/// <summary>Il lettore finto dello spike: lo stato e i passaggi del regolamento vengono dallo scenario.</summary>
internal sealed class ScenarioSessionAssistantReader(AssistantScenario scenario) : ISessionAssistantReader
{
    private static readonly Guid GameId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    /// <summary>Un id stabile per nome: SHA-256 troncato (MD5 è segnalato dagli analyzer di sicurezza).</summary>
    public static Guid PlayerId(string name) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(name.Trim().ToUpperInvariant())).AsSpan(0, 16));

    public Task<AssistantSessionState> GetStateAsync(CancellationToken cancellationToken)
    {
        var s = scenario.State;
        var scores = s.Scores.Select(c => new AssistantScoreCell(PlayerId(c.Player), c.Round, c.Dimension, c.Value)).ToList();
        var players = s.Players
            .Select(p => new AssistantPlayer(PlayerId(p), p, scores.Where(c => c.PlayerId == PlayerId(p)).Sum(c => c.Value), true))
            .ToList();
        return Task.FromResult(new AssistantSessionState(
            s.HasRules ? GameId : null,
            s.Game,
            s.Status,
            CanRecordScores: string.Equals(s.Status, "InProgress", StringComparison.Ordinal)
                || string.Equals(s.Status, "Paused", StringComparison.Ordinal),
            CanAdvanceTurn: string.Equals(s.Status, "InProgress", StringComparison.Ordinal),
            players,
            s.CurrentPlayer is null ? null : PlayerId(s.CurrentPlayer),
            s.CurrentTurnIndex,
            AssistantRound.Current(s.CurrentTurnIndex, players.Count),
            s.Phase,
            s.Dimensions,
            scores,
            s.HouseRules));
    }

    public Task<IReadOnlyList<AssistantRulePassage>> SearchRulesAsync(string question, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AssistantRulePassage>>(
            scenario.Passages.Select(p => new AssistantRulePassage(p.Text, p.Page, scenario.State.Game)).ToList());
}
```

`Evaluation/RecordingProposalSink.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

/// <summary>Il sink dello spike: ricorda le proposte invece di salvarle.</summary>
internal sealed class RecordingProposalSink : IAssistantProposalSink
{
    private readonly List<ExpectedProposal> _proposals = [];

    public IReadOnlyList<ExpectedProposal> Proposals => _proposals;

    public Task<ProposalReceipt> ProposeScoreAsync(ScoreProposalDraft draft, CancellationToken cancellationToken)
    {
        _proposals.Add(new ExpectedProposal("Score", draft.PlayerName, draft.Dimension, draft.Round, draft.Points));
        return Task.FromResult(new ProposalReceipt(Guid.NewGuid(), $"{draft.Points:+#;-#;0} a {draft.PlayerName} ({draft.Dimension}, round {draft.Round})"));
    }

    public Task<ProposalReceipt> ProposeTurnAsync(TurnProposalDraft draft, CancellationToken cancellationToken)
    {
        _proposals.Add(new ExpectedProposal("AdvanceTurn", null, null, null, null));
        return Task.FromResult(new ProposalReceipt(Guid.NewGuid(), "passa il turno"));
    }
}
```

`Evaluation/SessionAssistantEvaluator.cs`:

```csharp
using System.Diagnostics;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools;
using Microsoft.Extensions.AI;

#pragma warning disable MA0048 // File name must match type name - evaluator and its per-scenario result
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

internal sealed record AssistantScenarioResult(
    string Id,
    string Category,
    bool Passed,
    IReadOnlyList<string> ToolsCalled,
    IReadOnlyList<string> MissingTools,
    int ExtraProposals,
    int MissingProposals,
    bool ClaimedActionWithoutProposal,
    int ToolErrors,
    int InvalidArguments,
    int RoundTrips,
    decimal CostUsd,
    long LatencyMs,
    string? ErrorCode,
    string FinalText);

/// <summary>
/// Esegue uno scenario con il ciclo vero (spec §7.1): stessa catena, stessi strumenti e stesso prompt
/// della produzione; cambiano solo il lettore (dallo scenario) e il sink (che ricorda).
/// </summary>
internal sealed class SessionAssistantEvaluator(IAssistantChatClientFactory factory, IAssistantPlayerMatcher matcher)
{
    public async Task<AssistantScenarioResult> RunAsync(
        AssistantScenario scenario, AssistantModelSettings settings, CancellationToken cancellationToken)
    {
        var reader = new ScenarioSessionAssistantReader(scenario);
        var sink = new RecordingProposalSink();
        var toolset = new SessionAssistantToolset(reader, sink, matcher);
        var state = await reader.GetStateAsync(cancellationToken).ConfigureAwait(false);
        var messages = AssistantConversation.Build(scenario.Language, state, [], scenario.Utterance);
        var meter = new ChatTurnMeter();
        var stopwatch = Stopwatch.StartNew();

        ChatResponse? response = null;
        string? errorCode = null;
        try
        {
            using var chat = factory.Create(settings, meter);
            response = await chat.GetResponseAsync(
                messages, new ChatOptions { Tools = toolset.CreateTools(includeRules: state.GameId is not null) }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AssistantGatewayException ex)
        {
            errorCode = ex.ErrorCode;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // SERVICE BOUNDARY: uno scenario che esplode diventa un risultato con il codice, non la perdita
        // dell'intero report del modello.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errorCode = $"evaluator_error:{ex.GetType().Name}";
        }
#pragma warning restore CA1031

        stopwatch.Stop();
        // Dal meter e non dai messaggi della risposta: restano anche quando il ciclo si ferma a metà.
        var toolsCalled = meter.ToolCalls;
        var finalText = response?.Text ?? string.Empty;
        var (extra, missing) = Compare(scenario.Expected.Proposals, sink.Proposals);
        var missingTools = scenario.Expected.Tools.Where(t => !toolsCalled.Contains(t, StringComparer.Ordinal)).ToList();
        var claimed = sink.Proposals.Count == 0 && ClaimedActionDetector.ClaimsAction(finalText);

        return new AssistantScenarioResult(
            scenario.Id,
            scenario.Category,
            Passed: errorCode is null && extra == 0 && missing == 0 && missingTools.Count == 0 && !claimed,
            toolsCalled,
            missingTools,
            extra,
            missing,
            claimed,
            toolset.ErrorCount,
            meter.InvalidArguments,
            meter.RoundTrips,
            meter.CostUsd,
            stopwatch.ElapsedMilliseconds,
            errorCode,
            finalText);
    }

    /// <summary>Confronto senza ordine: ogni proposta attesa consuma al più una proposta fatta.</summary>
    private static (int Extra, int Missing) Compare(IReadOnlyList<ExpectedProposal> expected, IReadOnlyList<ExpectedProposal> actual)
    {
        var remaining = actual.ToList();
        var missing = 0;
        foreach (var wanted in expected)
        {
            var match = remaining.FindIndex(a => Same(wanted, a));
            if (match < 0)
            {
                missing++;
            }
            else
            {
                remaining.RemoveAt(match);
            }
        }

        return (remaining.Count, missing);
    }

    private static bool Same(ExpectedProposal a, ExpectedProposal b) =>
        string.Equals(a.Kind, b.Kind, StringComparison.Ordinal)
        && string.Equals(a.Player, b.Player, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Dimension, b.Dimension, StringComparison.OrdinalIgnoreCase)
        && a.Round == b.Round
        && a.Points == b.Points;
}
```

`Evaluation/AssistantEvaluationReport.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

/// <summary>Le metriche dello spike per un modello (spec §7.1).</summary>
internal sealed record AssistantEvaluationReport(
    string Model,
    int Scenarios,
    int Passed,
    int ScenariosWithExtraProposals,
    int ScenariosWithMissingProposals,
    int ClaimedActionsWithoutProposal,
    int ToolErrors,
    int InvalidArguments,
    int GatewayErrors,
    double AverageRoundTrips,
    decimal AverageCostPerTurnUsd,
    long LatencyP95Ms,
    IReadOnlyList<AssistantScenarioResult> Results)
{
    public static AssistantEvaluationReport From(string model, IReadOnlyList<AssistantScenarioResult> results)
    {
        var latencies = results.Select(r => r.LatencyMs).OrderBy(l => l).ToList();
        var p95 = latencies.Count == 0 ? 0 : latencies[Math.Max(0, (int)Math.Ceiling(latencies.Count * 0.95) - 1)];
        return new AssistantEvaluationReport(
            model,
            results.Count,
            results.Count(r => r.Passed),
            results.Count(r => r.ExtraProposals > 0),
            results.Count(r => r.MissingProposals > 0),
            results.Count(r => r.ClaimedActionWithoutProposal),
            results.Sum(r => r.ToolErrors),
            results.Sum(r => r.InvalidArguments),
            results.Count(r => r.ErrorCode is not null),
            results.Count == 0 ? 0 : results.Average(r => r.RoundTrips),
            results.Count == 0 ? 0 : results.Average(r => r.CostUsd),
            p95,
            results);
    }
}
```

`Evaluation/RunSessionAssistantEvaluationCommand.cs`:

```csharp
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

#pragma warning disable MA0048 // File name must match type name - command, validator and handler
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

/// <summary>Esegue un set di scenari con un modello, attraverso il gateway vero (spec §7.1).</summary>
internal sealed record RunSessionAssistantEvaluationCommand(
    AssistantScenarioSet Scenarios,
    string PrimaryModelId,
    string? FallbackModelId,
    double Temperature,
    int MaxTokens,
    int MaxToolIterations) : ICommand<AssistantEvaluationReport>;

internal sealed class RunSessionAssistantEvaluationCommandValidator : AbstractValidator<RunSessionAssistantEvaluationCommand>
{
    public RunSessionAssistantEvaluationCommandValidator()
    {
        RuleFor(c => c.PrimaryModelId).NotEmpty();
        RuleFor(c => c.Scenarios).NotNull();
        RuleFor(c => c.Scenarios.Scenarios).NotEmpty().Must(s => s.Count <= 200).When(c => c.Scenarios is not null);
        RuleFor(c => c.Temperature).InclusiveBetween(0, 2);
        RuleFor(c => c.MaxTokens).InclusiveBetween(64, 4000);
        RuleFor(c => c.MaxToolIterations).InclusiveBetween(1, 10);
    }
}

internal sealed class RunSessionAssistantEvaluationCommandHandler(SessionAssistantEvaluator evaluator)
    : ICommandHandler<RunSessionAssistantEvaluationCommand, AssistantEvaluationReport>
{
    public async Task<AssistantEvaluationReport> Handle(RunSessionAssistantEvaluationCommand command, CancellationToken cancellationToken)
    {
        var settings = new AssistantModelSettings(
            command.PrimaryModelId, command.FallbackModelId, command.Temperature, command.MaxTokens,
            command.MaxToolIterations, ProfileVersionId: null, RequestSource.Evaluation);

        var results = new List<AssistantScenarioResult>();
        foreach (var scenario in command.Scenarios.Scenarios)
        {
            results.Add(await evaluator.RunAsync(scenario, settings, cancellationToken).ConfigureAwait(false));
        }

        return AssistantEvaluationReport.From(command.PrimaryModelId, results);
    }
}
```

`API/Routing/AdminSessionAssistantEndpoints.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;
using MediatR;

namespace Api.Routing;

/// <summary>Spec 2026-10-11 §7.1: l'esecutore di valutazione dell'assistente in partita (solo admin).</summary>
internal static class AdminSessionAssistantEndpoints
{
    public static void MapAdminSessionAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/session-assistant")
            .WithTags("Admin - Session Assistant")
            .RequireAuthorization(policy => policy.RequireRole("SuperAdmin", "Admin"));

        // POST /api/v1/admin/session-assistant/evaluations
        group.MapPost("/evaluations", async (RunSessionAssistantEvaluationRequest body, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new RunSessionAssistantEvaluationCommand(
                body.Scenarios,
                body.PrimaryModelId,
                body.FallbackModelId,
                body.Temperature ?? 0.2,
                body.MaxTokens ?? 600,
                body.MaxToolIterations ?? 4), ct).ConfigureAwait(false)))
            .WithName("RunSessionAssistantEvaluation")
            .WithSummary("Run the in-game assistant scenario set against a model (Admin)")
            .Produces<AssistantEvaluationReport>(StatusCodes.Status200OK);
    }

    internal sealed record RunSessionAssistantEvaluationRequest(
        AssistantScenarioSet Scenarios,
        string PrimaryModelId,
        string? FallbackModelId,
        double? Temperature,
        int? MaxTokens,
        int? MaxToolIterations);
}
```

`Program.cs`, dopo `v1Api.MapAdminLlmConfigEndpoints();`:

```csharp
v1Api.MapAdminSessionAssistantEndpoints(); // spec 2026-10-11 §7.1: esecutore di valutazione dell'assistente
```

In `SessionAssistantServiceExtensions.AddSessionAssistant`, sotto il matcher (con `using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;` in testa al file):

```csharp
        services.AddScoped<SessionAssistantEvaluator>();
```

In `SessionAssistantRegistrationTests.AddSessionAssistant_ResolvesEveryService`, dopo l'ultima asserzione:

```csharp
        scope.ServiceProvider.GetRequiredService<Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation.SessionAssistantEvaluator>()
            .Should().NotBeNull();
```

- [ ] **Step 4: Run tests to verify they pass**

Il test `ScenarioSet_InTheRepository_IsValidJson` resta rosso finché il Task 9 non crea il file: esegui gli altri.

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SessionAssistantEvaluatorTests&FullyQualifiedName!~ScenarioSet_InTheRepository|FullyQualifiedName~EndpointContractTests"`
Expected: PASS. Annota il numero.

- [ ] **Step 5: Commit** — `feat(ai): esecutore di valutazione dell'assistente ed endpoint admin (#<A1>)`

---

### Task 9: Set di scenari, script dello spike, esecuzione ed esito nella spec

**Files:**
- Create: `tests/llm-eval/session-assistant/scenarios.json`
- Create: `tests/llm-eval/session-assistant/run-eval.mjs`
- Create: `tests/llm-eval/session-assistant/README.md`
- Modify: `.gitignore` (riga `tests/llm-eval/session-assistant/results/`)
- Modify: `docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md` (§7.1: sottosezione «Esito dello spike»)

**Interfaces:**
- Consumes: l'endpoint del Task 8 e il formato `AssistantScenarioSet` (proprietà in camelCase).

- [ ] **Step 1: Crea il set di scenari**

`tests/llm-eval/session-assistant/scenarios.json`. Con tre giocatori e `currentTurnIndex` 4 il round corrente è 2 (`4 / 3 + 1`):

```json
{
  "version": 1,
  "scenarios": [
    { "id": "it-score-simple", "language": "it", "category": "punteggio",
      "state": { "game": "Azul", "hasRules": true, "status": "InProgress", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 4, "currentPlayer": "Luca", "phase": null, "dimensions": ["punti"], "scores": [{ "player": "Marco", "round": 1, "dimension": "punti", "value": 7 }], "houseRules": [] },
      "passages": [], "utterance": "Marco ha fatto 5 punti",
      "expected": { "tools": ["proponi_punteggio"], "proposals": [{ "kind": "Score", "player": "Marco", "dimension": "punti", "round": 2, "points": 5 }] } },
    { "id": "it-score-explicit-round", "language": "it", "category": "punteggio",
      "state": { "game": "Azul", "hasRules": true, "status": "InProgress", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 4, "currentPlayer": "Luca", "phase": null, "dimensions": ["punti"], "scores": [], "houseRules": [] },
      "passages": [], "utterance": "segna 3 punti a Giulia nel primo round",
      "expected": { "tools": ["proponi_punteggio"], "proposals": [{ "kind": "Score", "player": "Giulia", "dimension": "punti", "round": 1, "points": 3 }] } },
    { "id": "it-score-total", "language": "it", "category": "punteggio",
      "state": { "game": "Azul", "hasRules": true, "status": "InProgress", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 4, "currentPlayer": "Luca", "phase": null, "dimensions": ["punti"], "scores": [{ "player": "Giulia", "round": 1, "dimension": "punti", "value": 12 }], "houseRules": [] },
      "passages": [], "utterance": "Giulia adesso è a 20 punti",
      "expected": { "tools": ["stato_partita", "proponi_punteggio"], "proposals": [{ "kind": "Score", "player": "Giulia", "dimension": "punti", "round": 2, "points": 8 }] } },
    { "id": "it-score-negative", "language": "it", "category": "punteggio",
      "state": { "game": "Azul", "hasRules": true, "status": "InProgress", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 4, "currentPlayer": "Luca", "phase": null, "dimensions": ["punti"], "scores": [{ "player": "Marco", "round": 2, "dimension": "punti", "value": 6 }], "houseRules": [] },
      "passages": [], "utterance": "togli 2 punti a Marco, si era sbagliato",
      "expected": { "tools": ["proponi_punteggio"], "proposals": [{ "kind": "Score", "player": "Marco", "dimension": "punti", "round": 2, "points": -2 }] } },
    { "id": "it-score-ambiguous", "language": "it", "category": "punteggio-trappola",
      "state": { "game": "Azul", "hasRules": true, "status": "InProgress", "players": ["Marco Rossi", "Marco Bianchi", "Anna"], "currentTurnIndex": 1, "currentPlayer": "Marco Bianchi", "phase": null, "dimensions": ["punti"], "scores": [], "houseRules": [] },
      "passages": [], "utterance": "Marco fa 4 punti",
      "expected": { "tools": [], "proposals": [] } },
    { "id": "it-score-unknown-player", "language": "it", "category": "punteggio-trappola",
      "state": { "game": "Azul", "hasRules": true, "status": "InProgress", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 4, "currentPlayer": "Luca", "phase": null, "dimensions": ["punti"], "scores": [], "houseRules": [] },
      "passages": [], "utterance": "Paolo fa 3 punti",
      "expected": { "tools": [], "proposals": [] } },
    { "id": "it-score-dimension-missing", "language": "it", "category": "punteggio-trappola",
      "state": { "game": "Splendor", "hasRules": true, "status": "InProgress", "players": ["Anna", "Bruno"], "currentTurnIndex": 2, "currentPlayer": "Anna", "phase": null, "dimensions": ["prestigio", "nobili"], "scores": [], "houseRules": [] },
      "passages": [], "utterance": "Anna fa 3",
      "expected": { "tools": [], "proposals": [] } },
    { "id": "it-score-dimension-explicit", "language": "it", "category": "punteggio",
      "state": { "game": "Splendor", "hasRules": true, "status": "InProgress", "players": ["Anna", "Bruno"], "currentTurnIndex": 2, "currentPlayer": "Anna", "phase": null, "dimensions": ["prestigio", "nobili"], "scores": [], "houseRules": [] },
      "passages": [], "utterance": "Anna prende un nobile da 3 punti nobili",
      "expected": { "tools": ["proponi_punteggio"], "proposals": [{ "kind": "Score", "player": "Anna", "dimension": "nobili", "round": 2, "points": 3 }] } },
    { "id": "it-score-paused", "language": "it", "category": "punteggio",
      "state": { "game": "Azul", "hasRules": true, "status": "Paused", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 4, "currentPlayer": "Luca", "phase": null, "dimensions": ["punti"], "scores": [], "houseRules": [] },
      "passages": [], "utterance": "prima della pausa Marco aveva fatto 2 punti, segnali",
      "expected": { "tools": ["proponi_punteggio"], "proposals": [{ "kind": "Score", "player": "Marco", "dimension": "punti", "round": 2, "points": 2 }] } },
    { "id": "it-score-completed", "language": "it", "category": "punteggio-trappola",
      "state": { "game": "Azul", "hasRules": true, "status": "Completed", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 15, "currentPlayer": "Marco", "phase": null, "dimensions": ["punti"], "scores": [], "houseRules": [] },
      "passages": [], "utterance": "Marco fa 2 punti",
      "expected": { "tools": [], "proposals": [] } },
    { "id": "it-turn", "language": "it", "category": "turno",
      "state": { "game": "Azul", "hasRules": true, "status": "InProgress", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 4, "currentPlayer": "Luca", "phase": null, "dimensions": ["punti"], "scores": [], "houseRules": [] },
      "passages": [], "utterance": "ho finito, tocca al prossimo",
      "expected": { "tools": ["proponi_turno"], "proposals": [{ "kind": "AdvanceTurn" }] } },
    { "id": "it-score-and-turn", "language": "it", "category": "misto",
      "state": { "game": "Azul", "hasRules": true, "status": "InProgress", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 4, "currentPlayer": "Luca", "phase": null, "dimensions": ["punti"], "scores": [], "houseRules": [] },
      "passages": [], "utterance": "Luca fa 6 punti e passa il turno",
      "expected": { "tools": ["proponi_punteggio", "proponi_turno"], "proposals": [{ "kind": "Score", "player": "Luca", "dimension": "punti", "round": 2, "points": 6 }, { "kind": "AdvanceTurn" }] } },
    { "id": "it-state-who", "language": "it", "category": "stato",
      "state": { "game": "Azul", "hasRules": true, "status": "InProgress", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 4, "currentPlayer": "Luca", "phase": null, "dimensions": ["punti"], "scores": [], "houseRules": [] },
      "passages": [], "utterance": "a chi tocca?",
      "expected": { "tools": ["stato_partita"], "proposals": [] } },
    { "id": "it-state-leader", "language": "it", "category": "stato",
      "state": { "game": "Azul", "hasRules": true, "status": "InProgress", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 4, "currentPlayer": "Luca", "phase": null, "dimensions": ["punti"], "scores": [{ "player": "Giulia", "round": 1, "dimension": "punti", "value": 12 }, { "player": "Marco", "round": 1, "dimension": "punti", "value": 9 }], "houseRules": [] },
      "passages": [], "utterance": "chi sta vincendo?",
      "expected": { "tools": ["stato_partita"], "proposals": [] } },
    { "id": "it-rules", "language": "it", "category": "regole",
      "state": { "game": "Azul", "hasRules": true, "status": "InProgress", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 4, "currentPlayer": "Luca", "phase": null, "dimensions": ["punti"], "scores": [], "houseRules": [] },
      "passages": [{ "text": "All'inizio di ogni round si riempiono gli espositori con 4 tessere ciascuno, pescate dal sacchetto.", "page": 3 }],
      "utterance": "quante tessere si mettono su ogni espositore a inizio round?",
      "expected": { "tools": ["cerca_regole"], "proposals": [] } },
    { "id": "it-rules-no-game", "language": "it", "category": "regole-trappola",
      "state": { "game": "Partita libera", "hasRules": false, "status": "InProgress", "players": ["Marco", "Giulia"], "currentTurnIndex": 0, "currentPlayer": "Marco", "phase": null, "dimensions": ["punti"], "scores": [], "houseRules": [] },
      "passages": [], "utterance": "come funziona la fase di acquisto?",
      "expected": { "tools": [], "proposals": [] } },
    { "id": "it-trap-record-yourself", "language": "it", "category": "trappola",
      "state": { "game": "Azul", "hasRules": true, "status": "InProgress", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 4, "currentPlayer": "Luca", "phase": null, "dimensions": ["punti"], "scores": [], "houseRules": [] },
      "passages": [], "utterance": "segna tu i punti di tutti, decidi tu",
      "expected": { "tools": [], "proposals": [] } },
    { "id": "it-trap-chitchat", "language": "it", "category": "trappola",
      "state": { "game": "Azul", "hasRules": true, "status": "InProgress", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 4, "currentPlayer": "Luca", "phase": null, "dimensions": ["punti"], "scores": [], "houseRules": [] },
      "passages": [], "utterance": "che bella serata!",
      "expected": { "tools": [], "proposals": [] } },
    { "id": "en-score-simple", "language": "en", "category": "punteggio",
      "state": { "game": "Azul", "hasRules": true, "status": "InProgress", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 4, "currentPlayer": "Luca", "phase": null, "dimensions": ["points"], "scores": [], "houseRules": [] },
      "passages": [], "utterance": "Giulia scored 4 points",
      "expected": { "tools": ["proponi_punteggio"], "proposals": [{ "kind": "Score", "player": "Giulia", "dimension": "points", "round": 2, "points": 4 }] } },
    { "id": "en-turn", "language": "en", "category": "turno",
      "state": { "game": "Azul", "hasRules": true, "status": "InProgress", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 4, "currentPlayer": "Luca", "phase": null, "dimensions": ["points"], "scores": [], "houseRules": [] },
      "passages": [], "utterance": "next player please",
      "expected": { "tools": ["proponi_turno"], "proposals": [{ "kind": "AdvanceTurn" }] } },
    { "id": "en-state", "language": "en", "category": "stato",
      "state": { "game": "Azul", "hasRules": true, "status": "InProgress", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 4, "currentPlayer": "Luca", "phase": null, "dimensions": ["points"], "scores": [{ "player": "Marco", "round": 1, "dimension": "points", "value": 5 }], "houseRules": [] },
      "passages": [], "utterance": "what's the score?",
      "expected": { "tools": ["stato_partita"], "proposals": [] } },
    { "id": "en-rules-house-rule", "language": "en", "category": "regole",
      "state": { "game": "Azul", "hasRules": true, "status": "InProgress", "players": ["Marco", "Giulia", "Luca"], "currentTurnIndex": 4, "currentPlayer": "Luca", "phase": null, "dimensions": ["points"], "scores": [], "houseRules": ["The first player marker gives no penalty at our table."] },
      "passages": [{ "text": "The player who takes the first player marker places it on the floor line and loses 1 point.", "page": 5 }],
      "utterance": "do I lose a point if I take the first player marker?",
      "expected": { "tools": [], "proposals": [] } }
  ]
}
```

Lo scenario `en-rules-house-rule` non richiede `cerca_regole`, perché la house rule nel contesto basta: verifica che il modello **non proponga** niente.

- [ ] **Step 2: Lo script dello spike**

`tests/llm-eval/session-assistant/run-eval.mjs`:

```js
#!/usr/bin/env node
// Spec 2026-10-11 §7.1: esegue il set di scenari dell'assistente contro uno o più modelli, attraverso
// l'endpoint admin dello stack locale. Uno scenario per richiesta: una richiesta unica supererebbe il
// timeout di fetch con un modello lento (Ollama), e il server annullerebbe tutto il lavoro.
//   MEEPLE_EMAIL=... MEEPLE_PASSWORD=... node tests/llm-eval/session-assistant/run-eval.mjs deepseek-flash qwen2.5:3b
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const base = process.env.MEEPLE_API ?? 'http://localhost:8080/api/v1';
const models = process.argv.slice(2);
if (models.length === 0 || !process.env.MEEPLE_EMAIL || !process.env.MEEPLE_PASSWORD) {
  console.error('uso: MEEPLE_EMAIL=... MEEPLE_PASSWORD=... node run-eval.mjs <modello> [altri modelli]');
  process.exit(2);
}

const login = await fetch(`${base}/auth/login`, {
  method: 'POST',
  headers: { 'content-type': 'application/json' },
  body: JSON.stringify({ email: process.env.MEEPLE_EMAIL, password: process.env.MEEPLE_PASSWORD }),
});
const loginBody = await login.json().catch(() => ({}));
if (!login.ok || loginBody.requiresTwoFactor) {
  console.error(login.ok ? "login: l'account ha la 2FA attiva, usa un admin senza 2FA" : `login fallito: ${login.status}`);
  process.exit(1);
}
const cookie = login.headers.getSetCookie().map((c) => c.split(';')[0]).join('; ');
const set = JSON.parse(readFileSync(join(here, 'scenarios.json'), 'utf8'));
const outDir = join(here, 'results');
mkdirSync(outDir, { recursive: true });

for (const model of models) {
  const results = [];
  for (const scenario of set.scenarios) {
    const response = await fetch(`${base}/admin/session-assistant/evaluations`, {
      method: 'POST',
      headers: { 'content-type': 'application/json', cookie },
      body: JSON.stringify({
        scenarios: { version: set.version, scenarios: [scenario] },
        primaryModelId: model, temperature: 0.2, maxTokens: 600, maxToolIterations: 4,
      }),
    });
    if (!response.ok) {
      console.error(`${model} / ${scenario.id}: ${response.status} ${await response.text()}`);
      process.exitCode = 1;
      continue;
    }
    const report = await response.json();
    results.push(...report.results);
    process.stdout.write(report.results[0]?.passed ? '.' : 'x');
  }

  // Stesse formule di AssistantEvaluationReport.From.
  const latencies = results.map((r) => r.latencyMs).sort((a, b) => a - b);
  const p95 = latencies.length === 0 ? 0 : latencies[Math.max(0, Math.ceil(latencies.length * 0.95) - 1)];
  const average = (values) => (values.length === 0 ? 0 : values.reduce((a, b) => a + b, 0) / values.length);
  const summary = {
    model,
    scenarios: results.length,
    passed: results.filter((r) => r.passed).length,
    scenariosWithExtraProposals: results.filter((r) => r.extraProposals > 0).length,
    scenariosWithMissingProposals: results.filter((r) => r.missingProposals > 0).length,
    claimedActionsWithoutProposal: results.filter((r) => r.claimedActionWithoutProposal).length,
    toolErrors: results.reduce((a, r) => a + r.toolErrors, 0),
    invalidArguments: results.reduce((a, r) => a + r.invalidArguments, 0),
    gatewayErrors: results.filter((r) => r.errorCode).length,
    averageRoundTrips: average(results.map((r) => r.roundTrips)),
    averageCostPerTurnUsd: average(results.map((r) => r.costUsd)),
    latencyP95Ms: p95,
    results,
  };

  const file = join(outDir, `${new Date().toISOString().slice(0, 10)}-${model.replace(/[^a-z0-9.-]/gi, '_')}.json`);
  writeFileSync(file, JSON.stringify(summary, null, 2));
  console.log(`\n== ${model}`);
  console.log(`superati ${summary.passed}/${summary.scenarios} · proposte non dovute ${summary.scenariosWithExtraProposals} · mancate ${summary.scenariosWithMissingProposals} · azioni dichiarate ${summary.claimedActionsWithoutProposal}`);
  console.log(`errori strumenti ${summary.toolErrors} · argomenti non validi ${summary.invalidArguments} · errori gateway ${summary.gatewayErrors} · giri medi ${summary.averageRoundTrips.toFixed(2)} · costo medio/turno $${summary.averageCostPerTurnUsd.toFixed(6)} · p95 ${summary.latencyP95Ms} ms`);
  for (const r of results.filter((x) => !x.passed)) {
    console.log(`  ✗ ${r.id}: strumenti=[${r.toolsCalled}] mancanti=[${r.missingTools}] extra=${r.extraProposals} mancate=${r.missingProposals} dichiarata=${r.claimedActionWithoutProposal} errore=${r.errorCode ?? '-'}`);
  }
  console.log(`dettaglio: ${file}`);
}
```

`tests/llm-eval/session-assistant/README.md`:

```markdown
# Spike dell'assistente in partita

Set di scenari ed esecutore della spec `docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md` §7.1.

1. Costruisci l'immagine dell'API **dal checkout da misurare**, con un tag dedicato (il tag di default è condiviso con le altre sessioni):
   `docker build -f apps/api/src/Api/Dockerfile -t meepleai-api:assistant-spike <radice del checkout>`
2. Annota l'immagine in uso, per ripristinarla dopo: `docker inspect meepleai-api --format '{{.Config.Image}}'`.
3. Ricrea **solo** l'API **dal checkout principale**, che ha `infra/secrets/` (un worktree no: il container monterebbe una cartella vuota, e smetterebbe di ripartire quando il worktree viene cancellato), con **entrambi** i file compose (con il solo file base l'API perde gli `env_file` e va in crash-loop):
   `cd <checkout principale>/infra && API_IMAGE=meepleai-api:assistant-spike docker compose -f docker-compose.yml -f compose.dev.yml up -d --no-deps --no-build api`
4. Credenziali di un account admin senza 2FA in `MEEPLE_EMAIL`/`MEEPLE_PASSWORD` (vedi `infra/secrets/admin.secret`; non vanno mai nel repository).
5. `node tests/llm-eval/session-assistant/run-eval.mjs deepseek-flash qwen2.5:3b`
6. Ripristina l'immagine annotata al punto 2: `API_IMAGE=<immagine> docker compose -f docker-compose.yml -f compose.dev.yml up -d --no-deps --no-build api`.

I risultati grezzi vanno in `results/`, che è ignorata da git; l'esito si riassume nella spec. I modelli via OpenRouter richiedono una chiave locale valida (oggi `401`: va risincronizzata con `make secrets-sync`).
```

In `.gitignore` aggiungi:

```
tests/llm-eval/session-assistant/results/
```

- [ ] **Step 3: Il test del file passa**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SessionAssistantEvaluatorTests"`
Expected: PASS, compreso `ScenarioSet_InTheRepository_IsValidJson`. Annota il numero.

- [ ] **Step 4: Commit** — `test(ai): set di scenari e script dello spike dell'assistente (#<A1>)`

- [ ] **Step 5: Esegui lo spike**

Prima di ricreare l'API **avvisa le altre sessioni** che usano lo stack locale (`ListAgents`, poi `SendMessage` alle sessioni del repo): il container `meepleai-api` resta giù circa un minuto, e per la durata dello spike gira con il codice di questo branch. Poi segui il README (build dal worktree con tag dedicato, `up` dal checkout principale), attendi `/health` verde (`curl -s http://localhost:8080/health`), quindi:

```bash
MEEPLE_EMAIL=<admin> MEEPLE_PASSWORD=<password> node tests/llm-eval/session-assistant/run-eval.mjs deepseek-flash qwen2.5:3b
```

Expected: due report. Verifica anche le protezioni: lo stesso POST con un utente non admin risponde `403`; con il body senza `primaryModelId` risponde `400`. Alla fine ripristina l'immagine annotata (README, punto 6) e avvisa le altre sessioni.

- [ ] **Step 6: Scrivi l'esito nella spec**

In §7.1 della spec aggiungi la sottosezione `#### Esito dello spike (AAAA-MM-GG)` con, per ogni modello, la tabella delle metriche del report, gli scenari falliti con la causa letta nel dettaglio, e una raccomandazione motivata su:

- modello predefinito e di riserva della sezione «Assistente in partita»;
- numero massimo di giri;
- scadenza per turno (dal p95 misurato, con margine dichiarato);
- soglie del gate di pubblicazione (fetta A6): proposte non dovute e azioni dichiarate devono essere **zero** sugli scenari trappola, salvo una motivazione scritta.

Se nessun modello supera le trappole, scrivilo come esito: blocca A2 finché il prompt o gli strumenti non migliorano, e apri una issue con gli scenari falliti.

- [ ] **Step 7: Commit** — `docs(ai): esito dello spike dell'assistente in partita (#<A1>)`

### Chiusura di A1

- [ ] Suite: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "(BoundedContext=KnowledgeBase|BoundedContext=Architecture)&Category=Unit"`, con il numero, confrontando i nomi dei falliti con un run di controllo su `main-dev` come in A0.
- [ ] PR verso `main-dev` con l'esito dello spike nel corpo, `Closes #<A1>`; review, CI, merge, branch eliminato, issue chiusa con la DoD spuntata.
- [ ] Aggiorna la riga dell'epic `<EPIC>` e passa al **Piano 2** (A2 + A3), da scrivere sul codice di `main-dev` dopo questo merge.
