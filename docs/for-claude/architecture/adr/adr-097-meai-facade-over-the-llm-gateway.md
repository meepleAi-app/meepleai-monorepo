# ADR-097 — Microsoft.Extensions.AI come facciata del gateway per il tool calling

**Date**: 2026-10-12
**Status**: Accepted
**Issue**: [#4239](https://github.com/meepleAi-app/meepleai-monorepo/issues/4239) (fetta A0 dell'epic #4238)
**Related**: [ADR-095](./adr-095-single-answer-pipeline-and-versioned-agent-profile.md) (gateway unico; chiude la domanda aperta n. 4) · spec [`2026-10-11-session-assistant-agent-design.md`](../../../for-developers/specs/2026-10-11-session-assistant-agent-design.md) §3

## Decisione

1. Il gateway espone una chat con strumenti: `ILlmService.ChatAsync(LlmChatRequest)` e `ILlmClient.ChatAsync(...)`. `HybridLlmService` la serve con lo **stesso ciclo dei tentativi** di `GenerateAnswerAsync`: ordine dei modelli dal chiamante con degrado, circuit breaker, redazione PII per OpenRouter, ammissione al tetto, costo, chiusura dell'ammissione.
2. `Microsoft.Extensions.AI` (`IChatClient`) sta **sopra** il gateway, non sotto: `GatewayChatClient` traduce i tipi M.E.AI verso `LlmChatRequest` e dipende solo da `ILlmService`. La catena è `FunctionInvokingChatClient → OpenTelemetryChatClient → GatewayChatClient`.
3. Solo il pacchetto `Microsoft.Extensions.AI` 10.6.0, che dipende dalle `Microsoft.Extensions.*` 10.0.8 già in uso. Nessun adapter di provider M.E.AI: l'HTTP resta in `Services/LlmClients/`.

## Perché sopra e non sotto

La logica di ogni tentativo è privata in `HybridLlmService`. Un `IChatClient` attaccato al provider dovrebbe copiarla, insieme alla risoluzione delle credenziali a ogni chiamata (`IProviderCredentialResolver`) e alle protezioni dei test (`PaidAiHostGuardExtensions`, `TestFailingLlmService`). Sopra il gateway, ogni giro del ciclo con strumenti passa da tetto e costo **per costruzione**, e `LlmProviderEgressArchitectureTests` non cambia.

## Conseguenze

- Ogni giro del ciclo è una riga del registro costi e un'ammissione al tetto: il costo di un turno è la somma dei suoi giri.
- `LlmCompletionResult.ToolCalls` è additivo: vuoto per ogni altra chiamata.
- Lo streaming della chat con strumenti non c'è: l'assistente bufferizza la risposta (spec §6.1).
- Gli argomenti non validi di uno strumento falliscono nel binding di M.E.AI prima dello strumento: un `FunctionInvoker` li traduce in un errore tipizzato per il modello invece di «Error: Function failed.».
- La contabilità dei token in cache resta fuori (spec §4.1).
