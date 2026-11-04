# Assistente in partita — Piano 2 (fette A2 + A3): proposte, sezione del profilo, turno e conferma

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** dare all'assistente in partita ciò che gli manca per funzionare davvero: la proposta come aggregato persistito, la sua sezione nel profilo versionato, il turno che legge la sessione live e crea proposte, e la conferma dell'host come unico ponte verso le scritture. L'assistente resta **spento** nella versione pubblicata finché #4270 non è chiusa.

**Architecture:** `AssistantProposal` è un aggregato di `KnowledgeBase` con una macchina a stati e concorrenza ottimistica su `xmin`. Il turno (`AskSessionAssistantCommand`, in streaming) prende i parametri dalla versione pubblicata del profilo, legge la sessione attraverso una porta di sola lettura implementata in Infrastructure con le query di GameManagement, esegue il ciclo di A0 con gli strumenti di A1 e salva messaggi e proposte. La conferma (`ConfirmAssistantProposalCommand`) prenota la proposta, controlla che lo stato della partita sia quello atteso e applica attraverso i comandi esistenti della sessione live.

**Tech Stack:** .NET 9, ASP.NET Minimal APIs + MediatR (anche `IStreamRequest`), EF Core su PostgreSQL (jsonb, `xmin`), `Microsoft.Extensions.AI` 10.6.0, xUnit v3 + Moq + FluentAssertions, Testcontainers per l'integrazione.

**Spec:** [`docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md`](../specs/2026-10-11-session-assistant-agent-design.md) (§3.2, §4.3, §5, §6, §7.2, §8 righe A2 e A3). Le fette A4-A7 hanno piani propri.

**Issue:** epic #4238 · A2 #4275 · A3a #4276 · A3b #4277. Prima di pubblicare l'assistente: #4270.

## Global Constraints

- Percorsi: `API` = `apps/api/src/Api`, `TESTS` = `apps/api/tests/Api.Tests`, `KB` = `API/BoundedContexts/KnowledgeBase`.
- **Il modello non scrive mai la partita.** Gli strumenti ricevono solo `ISessionAssistantReader` e `IAssistantProposalSink`. L'unico tipo che può scrivere è `IAssistantProposalApplier`, e lo riceve solo `ConfirmAssistantProposalCommandHandler`.
- `KB/Application/SessionAssistant/**` non nomina tipi di `Api.BoundedContexts.GameManagement.*`. Gli adattatori verso GameManagement stanno in `KB/Infrastructure/SessionAssistant/` e usano `IMediator`.
- Gli endpoint usano solo `IMediator` (CQRS). Solo il **proprietario** della sessione (`CreatedByUserId`) può chiedere, confermare e rifiutare: il filtro `RequireLiveSessionParticipant` controlla la partecipazione, quindi il controllo del proprietario sta negli handler.
- L'assistente è **spento** se la sezione del profilo manca. Nessuna migrazione di questo piano lo abilita: i test lo abilitano pubblicando una versione nel proprio database.
- `cerca_regole` non è offerto in A3 (arriva con A5): il turno chiama `CreateTools(includeRules: false)`.
- Round derivato sull'indice di turno **a base 1** del dominio: `(CurrentTurnIndex - 1) / giocatori in ordine di turno + 1` per indice ≥ 1, e 1 per indice 0.
- Punti = **incremento**. Alla conferma si scrive il valore atteso della cella più l'incremento.
- Stati della proposta, esatti: `Pending`, `Applying`, `Accepted`, `Edited`, `Failed`, `Rejected`, `Expired`, `Stale`. Codici d'errore HTTP, esatti: `assistant_disabled`, `assistant_model_without_tools`, `session_state_invalid`, `proposal_stale`, `proposal_not_pending`, `proposal_not_applicable`. Codici dell'evento `error`: `budget_paused`, `assistant_unavailable`, `turn_timeout`. Valori di `failure_code` di una proposta `Failed`: `session_state_invalid`, `proposal_not_applicable`, `session_state_unavailable`.
- L'API genera il file di documentazione XML (`GenerateDocumentationFile`), e gli avvisi sono errori: un commento che documenta con `<param>` **alcuni** parametri e non tutti non compila (CS1573), e un `<see cref>` deve risolversi (CS1574). Nei record posizionali la descrizione dei parametri va nel `<summary>` o nei `<remarks>`.
- Un `catch` che scrive un log passa l'eccezione al logger (S6667).
- `AddSessionAssistant()` è verificato da `SessionAssistantRegistrationTests` con `ValidateOnBuild`, su un contenitore quasi vuoto: ogni servizio aggiunto lì chiede, **nello stesso task**, i doppi delle sue dipendenze in quel test.
- Un'entità con il token `xmin` va dichiarata in `ConcurrencyTokenWritePathInventoryTests.DeclaredWritePaths`, con la tecnica con cui il suo percorso di scrittura lo preserva.
- Migrazioni solo additive (tabella nuova, colonna nullable, aggiornamento di dati): il `Migration Safety Gate` blocca il resto. Si generano con `dotnet ef migrations add <Nome>` da `API`, dopo `dotnet restore`; il file `Designer` e lo snapshot li scrive lo strumento.
- Entità con `xmin`: si caricano tracciate (`AsTracking`) e non si riattaccano mai con `Update()` (ADR-060). Ogni handler che modifica chiama `IUnitOfWork.SaveChangesAsync`.
- Analyzer con `TreatWarningsAsErrors`: `StringComparer`/`StringComparison` espliciti (MA0002, MA0006); `ConfigureAwait(false)` sugli `await` del codice di produzione (MA0004); un `catch` generico vuole `#pragma warning disable CA1031` e un commento `SERVICE BOUNDARY`; più tipi in un file vogliono `#pragma warning disable MA0048`; un'eccezione `internal` vuole `S3871`; nessun `TODO` (S1135); una proprietà non copia una collezione (S2365); niente metodi privati inutilizzati (S1144).
- Test: xUnit v3 (`TestContext.Current.CancellationToken`), `[Trait("Category", TestCategories.Unit|Integration)]`, `[Trait("BoundedContext", "KnowledgeBase")]`, `[Trait("Issue", "<numero>")]`. I test di integrazione usano `SharedHostPerTestDatabaseFixture` (prefisso del database entro 25 caratteri; `BeginTestAsync()` prima di ogni uso della factory) o `IntegrationHostFixture`, e si eseguono **in locale prima della PR**: la CI delle PR non li esegue (`dev-fast.yml` è solo unit; l'integrazione gira su `main-dev` dopo il merge).
- Prima di ogni `dotnet test` locale: nessun `testhost` di questo worktree vivo. Non terminare i `testhost` delle altre sessioni. Riportare sempre il **numero** di test eseguiti.
- La suite unit ha cinque fallimenti noti in ogni worktree (`.git` è un file): `EveryExternalEgressRegistration_IsPinned`, `EveryHttpClientRegistration_IsClassified`, `IlDefaultDelCodiceCoincideConQuelloDiCompose`, `LaScansioneTrovaEntrambiIFileCompose`, `PinnedRegistrations_DoNotOverrideTheirOwnPrimaryHandler`. Si confrontano i **nomi** con un run di controllo su `main-dev`.
- Dopo una perturbazione di prova: copia del file fuori dal repository, ripristino per copia e `touch`. Mai `git checkout --` su un file con modifiche non committate.
- Commit: `feat|fix|test|docs|refactor(scope): descrizione (#issue)`, intestazione entro 72 caratteri, corpo che finisce con `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Mai `--no-verify`. Prima del commit: `rm -rf apps/web/.next/types`; in un worktree nuovo `cd apps/web && pnpm install --offline --prefer-offline`. Commit e push in background (hook lenti). Push con l'account `meepleAi-app`.
- Nessun conteggio volatile in prosa nei documenti, nei corpi di issue e PR.

## Scostamenti dalla spec decisi in questo piano

Ognuno va riportato nella spec dal task che lo introduce.

| # | Spec | Piano | Perché |
|---|---|---|---|
| S1 | §5.1: round = `CurrentTurnIndex / giocatori attivi + 1` | `(CurrentTurnIndex - 1) / n + 1`, con `n` = giocatori attivi non spettatori. Lo stato che vede l'assistente elenca solo quei giocatori | `LiveGameSession.Start()` porta l'indice a 1 e `GetCurrentTurnPlayerId` usa `(indice - 1) % n`: con la formula della spec l'ultimo giocatore del primo giro cade nel round 2. **Limite noto**: il dominio conta sull'ordine di turno (`SetTurnOrder`), che `LiveSessionDto` non espone; se l'host imposta un ordine che esclude un giocatore attivo, il round derivato diverge. Con l'ordine predefinito, quello di ingresso, coincide |
| S2 | §6.2: della storia entrano gli ultimi messaggi del `ChatThread` della sessione | Il thread dell'assistente nasce con `GameId` nullo e `LiveSessionId` valorizzato | `ChatThreadEntity.GameId` ha una chiave esterna verso `shared_games`: una sessione su un gioco privato la violerebbe |
| S3 | §3.2: la sezione ha abilitato, prompt, giri, scadenza, uso della riserva | In più temperatura e lunghezza massima della risposta. Un assistente abilitato deve avere un prompt per la lingua di riserva del profilo | Lo spike ha misurato 0,2 e 600: prenderle dal resto del profilo (pensato per le risposte sulle regole) farebbe girare l'assistente con parametri mai misurati. Senza il prompt di riserva, una sessione in una lingua non prevista resterebbe senza prompt |
| S4 | §3.2: `PublishDraft` rifiuta un modello senza tool calling | `PublishDraft` e `RollbackTo` ricevono l'insieme dei modelli con tool calling; il turno ricontrolla a ogni richiesta | Il dominio non può leggere il catalogo, e la migrazione di dati che pubblica scavalca l'aggregato |
| S5 | §8: A2 comprende la migrazione che pubblica l'assistente | Fuori dal piano, dopo #4270 | Il testo e gli strumenti che A2 pubblicherebbe non sono quelli misurati (spec §7.1) |
| S6 | §6.2: eventi `proposal` durante il turno | `proposal` e `answer` arrivano dopo il salvataggio; durante il ciclo arriva solo `status`. I controlli iniziali falliti sono un errore HTTP, non un evento | Una card inviata prima del salvataggio si potrebbe confermare quando la proposta non è ancora nel database |
| S7 | §5.3: il secondo tap «riceve lo stato attuale» | Su `Applying`, `Accepted` o `Edited` la conferma risponde `200` con lo stato attuale; su `Rejected`, `Expired`, `Stale` o `Failed` risponde `409 proposal_not_pending` | Un doppio tap non deve mostrare un errore all'host, ma una proposta chiusa in altro modo sì |
| S8 | §5.3: il dominio che rifiuta dà `session_state_invalid` | `ConflictException` del dominio, una sessione sparita, o una scrittura concorrente sulla sessione (`xmin`) danno `session_state_invalid`; `DomainException` (giocatore rimosso, dimensione tolta) dà `proposal_not_applicable`. In tutti i casi la proposta diventa `Failed` e l'host richiede. Se dopo la prenotazione non si riesce a leggere lo stato della partita, la proposta diventa `Failed` con `session_state_unavailable` e l'errore originale risale | Sono cause diverse, e l'host deve poterle distinguere. Una corsa sulla sessione nello stesso istante è rara: un secondo tentativo automatico non vale la complessità nel pilota |
| S9 | §6.2: le `parts` dei messaggi vanno in `MessagesJson` | Nessuna `parts` in A3: il legame messaggio → proposte sta in `assistant_proposals.message_id`. Il thread dell'assistente non compare nell'elenco delle chat dell'utente | Le `parts` servono alle citazioni (A5); continuare quel thread dalla pagina delle chat mescolerebbe le due conversazioni |
| S10 | §5.2: una proposta `Pending` scade all'inizio del turno successivo o alla chiusura della sessione | All'inizio del turno, con un solo `UPDATE` eseguito subito; alla chiusura non si tocca nulla | Con le entità tracciate, una conferma nello stesso istante farebbe fallire il turno dopo la chiamata al modello. Alla chiusura la conferma tardiva è già rifiutata dal dominio: la pulizia va con le metriche di A6 |
| S11 | §5.3: un controllo segnala le proposte `Applying` più vecchie di un minuto | Le segnala il turno successivo, senza soglia di tempo | La proposta non ha un istante di prenotazione, e trovarne una `Applying` all'inizio di un turno è già l'anomalia |
| S12 | §5.3: il comando di dominio salva da solo | In più: gira in uno scope suo, e dopo la prenotazione la conferma non segue l'annullamento della richiesta | Un salvataggio fallito della sessione non deve restare nel change tracker della proposta; un host che chiude la pagina non deve lasciare una proposta a metà |
| S13 | §5.2: `Stale` solo da `Pending` | `Stale` anche da `Applying` | La spec si contraddice: §5.3 controlla lo stato atteso **dopo** la prenotazione, quindi la proposta è già `Applying` quando risulta superata |
| S14 | §5.1: i punti sono interi | I punti di una proposta e della sua correzione stanno entro ±100.000, e la cella di partenza entro ±1.000.000.000. Fuori dai limiti lo strumento risponde con un errore tipizzato | I punti li scrive un modello e si sommano alla cella: senza limiti un numero assurdo farebbe il giro dell'intero e finirebbe nel punteggio vero |
| S15 | §6.2: l'assistente risponde in `Setup`, `InProgress` o `Paused` | Anche in `Created`: risponde finché la sessione non è `Completed` | `MoveToSetup` non ha chiamanti: una sessione passa da `Created` a `InProgress`. Senza `Created` l'assistente non risponderebbe mai prima dell'avvio. Prima dell'avvio gli strumenti di proposta rifiutano comunque |
| S16 | §6.2: ogni chiamata al gateway porta l'id della sessione | Non lo porta | `LlmChatRequest` (A0) non ha il campo. Serve al costo per partita (§7.3, criterio 3): va con le metriche di A6 |
| S17 | §3.2: `AgentProfileFieldEffectTests` riceve le righe campo → effetto della sezione | Le righe stanno nei test del turno (`AssistantTurnTests` e `SessionAssistantTurnEndpointTests`) | L'effetto dei campi dell'assistente si osserva solo sul suo endpoint, che ha un host di test suo |
| S18 | §5.3: il rifiuto ha «le stesse verifiche di stato» della conferma | Un secondo tap sul rifiuto risponde `200` con la proposta già rifiutata. Una seconda conferma con una correzione diversa risponde `200` con l'esito della prima | L'idempotenza vale per entrambi i gesti: l'host non deve vedere un errore per aver toccato due volte |

## Review Focus

Le condizioni che la spec implica e che i test dei task non coprirebbero da soli. Ognuna ha il suo test nel task indicato.

1. **Un aggiornamento del prezzo di un modello azzera `SupportsToolCalling`.** `AiModelConfiguration` ricostruisce `ModelSettings` con il costruttore in tre punti: una proprietà nuova fuori dal costruttore sparirebbe a ogni modifica dell'admin, e l'assistente si spegnerebbe da solo. → Task 4, `UpdatePricing_KeepsTheToolCallingCapability` e i due test gemelli.
2. **Una versione del profilo salvata prima di questo piano.** Il jsonb non ha la chiave `sessionAssistant`: deve caricarsi come «disabilitato», non fallire. → Task 3, `LegacyContentWithoutTheSection_LoadsAsDisabled`.
3. **Due conferme nello stesso istante.** La seconda non deve scrivere né restituire un errore incomprensibile. → Task 2 (`SecondConcurrentSave_FailsOnXmin`) e Task 13 (`ConcurrentConfirms_OfAnAdvanceTurn_AdvanceOnce`).
4. **Lo stato della partita è cambiato fra proposta e conferma** (un altro punteggio nella stessa cella, il turno già passato a mano). → Task 11, `Confirm_WhenTheCellChanged_IsStaleAndDoesNotWrite`; Task 13 lo ripete sul database vero.
5. **Il turno fallisce a metà** (tetto, scadenza, provider): le proposte già create restano confermabili e l'host riceve un evento `error`, non una connessione troncata. → Task 9, `Turn_BudgetRefusedMidLoop_KeepsTheProposalsAndEmitsBudgetPaused` e `Turn_Timeout_EmitsTurnTimeout`.
6. **Il primo turno di una partita non ancora avviata** (`CurrentTurnIndex` a 0, nessun punteggio): il round è 1 e gli strumenti di proposta rispondono con un errore tipizzato, non con un'eccezione. → Task 6, `Round_IsOneBeforeTheSessionStarts_AndNothingCanBeWritten`.
7. **Il thread dell'assistente perde il legame con la sessione al secondo turno.** `ChatThreadRepository.UpdateAsync` riscrive l'intera riga: una colonna che la mappatura dimentica viene azzerata, e dal terzo turno l'assistente non ritrova la sua storia. → Task 7, `UpdateAsync_KeepsTheLiveSessionId`.
8. **Una conferma si ferma a metà dopo la prenotazione** (la sessione è sparita, lo stato non si legge): la proposta non deve restare `Applying` per sempre, con una card che non si può né confermare né rifiutare. → Task 11, `Confirm_WhenTheStateCannotBeRead_FailsTheProposal_InsteadOfLeavingItApplying` e `ARefusalOfTheSession_BecomesAFailureCode`.
9. **Il thread dell'assistente compare fra le chat dell'utente** (elenco, storico, cruscotto), da dove si potrebbe continuarlo come una chat sulle regole. → Task 7, `TheUserChatLists_LeaveOutTheAssistantThreads_ButTheExportStillSeesThem` e `Handle_LeavesOutTheAssistantThreads`.

---

## Struttura dei file

```
KB/Domain/Enums/AssistantProposalKind.cs, AssistantProposalStatus.cs            (Task 1)
KB/Domain/ValueObjects/AssistantProposalPayload.cs                              (Task 1: payload, expected, confirmed)
KB/Domain/Entities/AssistantProposal.cs                                         (Task 1)
KB/Domain/Repositories/IAssistantProposalRepository.cs                          (Task 2)
KB/Infrastructure/Persistence/AssistantProposalJson.cs                          (Task 2)
KB/Infrastructure/Persistence/AssistantProposalRepository.cs                    (Task 2)
KB/Infrastructure/Persistence/Configurations/AssistantProposalConfiguration.cs  (Task 2)
API/Infrastructure/Migrations/*_AddAssistantProposals.cs                        (Task 2, generata)
KB/Domain/ValueObjects/SessionAssistantSettings.cs                              (Task 3)
KB/Domain/ValueObjects/AgentProfileContent.cs                                   (Task 3, modifica)
KB/Infrastructure/Persistence/AgentProfileContentJson.cs                        (Task 3, modifica)
API/BoundedContexts/SystemConfiguration/Domain/ValueObjects/ModelSettings.cs    (Task 4, modifica)
API/BoundedContexts/SystemConfiguration/Domain/Entities/AiModelConfiguration.cs (Task 4, modifica)
API/BoundedContexts/SystemConfiguration/Application/Commands/SeedAiModelsCommandHandler.cs (Task 4, modifica)
KB/Domain/Services/LlmManagement/IToolCallingModelCatalog.cs                    (Task 4)
KB/Infrastructure/Services/CatalogToolCallingModels.cs                          (Task 4)
KB/Domain/Entities/AgentProfile.cs                                              (Task 5, modifica)

KB/Application/SessionAssistant/AssistantRound.cs                               (Task 6, modifica)
KB/Application/SessionAssistant/IAssistantSessionDirectory.cs                   (Task 6)
KB/Infrastructure/SessionAssistant/LiveSessionAssistantDirectory.cs             (Task 6: directory, lettore, mappatura)
tests/llm-eval/session-assistant/scenarios.json                                 (Task 6, versione 4)
KB/Domain/Entities/ChatThread.cs, Repositories/IChatThreadRepository.cs         (Task 7, modifica)
KB/Infrastructure/Persistence/ChatThreadRepository.cs                           (Task 7, modifica)
API/Infrastructure/Entities/KnowledgeBase/ChatThreadEntity.cs                   (Task 7, modifica)
API/Infrastructure/EntityConfigurations/KnowledgeBase/ChatThreadEntityConfiguration.cs (Task 7)
API/Infrastructure/Migrations/*_AddLiveSessionIdToChatThreads.cs                (Task 7, generata)
KB/Application/SessionAssistant/Turn/AssistantProposalDto.cs                    (Task 8: DTO e riassunto)
KB/Application/SessionAssistant/Turn/RepositoryProposalSink.cs                  (Task 8)
KB/Application/SessionAssistant/Turn/AssistantTurnEvent.cs                      (Task 9: evento e testi di ripiego)
KB/Application/SessionAssistant/Turn/AskSessionAssistantCommand.cs              (Task 9: comando e handler)
KB/Application/SessionAssistant/Turn/AssistantTurnRunner.cs                     (Task 9: il turno, senza MediatR)
API/Routing/SessionAssistantEndpoints.cs                                        (Task 10, 12)
KB/Application/SessionAssistant/Confirmation/IAssistantProposalApplier.cs       (Task 11)
KB/Application/SessionAssistant/Confirmation/ConfirmAssistantProposalCommand.cs (Task 11)
KB/Infrastructure/SessionAssistant/LiveSessionProposalApplier.cs                (Task 11)
KB/Application/SessionAssistant/Confirmation/RejectAssistantProposalCommand.cs  (Task 12)
```

I test stanno negli stessi percorsi sotto `TESTS/BoundedContexts/…`, quelli di integrazione in `TESTS/Integration/KnowledgeBase/SessionAssistant/` (con il loro host condiviso, `SessionAssistantTestHost.cs`, che nasce nel Task 6), quelli di architettura in `TESTS/Architecture/SessionAssistantArchitectureTests.cs`.

Tre confini che i task rispettano e che un revisore deve trovare intatti:

- **Chi legge la sessione**: solo `KB/Infrastructure/SessionAssistant/LiveSessionAssistantDirectory.cs`, con le query di GameManagement.
- **Chi scrive la sessione**: solo `KB/Infrastructure/SessionAssistant/LiveSessionProposalApplier.cs`, con due comandi di GameManagement, e lo riceve solo l'handler della conferma.
- **Chi parla al modello**: solo `AssistantTurnRunner`, attraverso la catena di A0.

---

## Preparazione (una volta, prima del Task 1)

- [ ] **P0: Piano su `main-dev`.** Dal worktree `D:/Repositories/wt-assistant-plan2`, branch `feature/issue-4238-assistant-plan-2` (parent `main-dev`): committa questo piano (`docs(ai): piano 2 dell'assistente in partita (#4238)`), push, PR verso `main-dev`, CI, merge. Le fette puntano al piano con un link.

- [ ] **P1: Issue.** Già create: A2 #4275, A3a #4276, A3b #4277. Aggiorna l'epic #4238 perché le nomini.

- [ ] **P2: Integrazione in locale.** Verifica che Docker risponda e che una classe di integrazione esistente giri:

```bash
docker ps --format '{{.Names}}' | head -3
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveSessionParticipantAuthzEndpointTests"
```

Expected: PASS. Annota il numero di test e il tempo. Se fallisce per l'ambiente (Testcontainers), fermati e dillo: le fette A2, A3a e A3b hanno test di integrazione che la CI delle PR non esegue.

---

# Fetta A2 — #4275

Worktree `D:/Repositories/wt-assistant-a2`, branch `feature/issue-4275-assistant-proposals` da `origin/main-dev`, `git config branch.feature/issue-4275-assistant-proposals.parent main-dev`.

### Task 1: L'aggregato `AssistantProposal`

**Files:**
- Create: `KB/Domain/Enums/AssistantProposalKind.cs`
- Create: `KB/Domain/Enums/AssistantProposalStatus.cs`
- Create: `KB/Domain/ValueObjects/AssistantProposalPayload.cs`
- Create: `KB/Domain/Entities/AssistantProposal.cs`
- Test: `TESTS/BoundedContexts/KnowledgeBase/Domain/Entities/AssistantProposalTests.cs`

**Interfaces:**
- Consumes: `AggregateRoot<Guid>` (`Api.SharedKernel.Domain.Entities`), `ConflictException(errorCode, message)` (`Api.Middleware.Exceptions`), `ValidationException(propertyName, message)` (`Api.SharedKernel.Domain.Exceptions`).
- Produces:
  - `AssistantProposalKind { Score = 0, AdvanceTurn = 1 }`, `AssistantProposalStatus { Pending = 0, Applying = 1, Accepted = 2, Edited = 3, Failed = 4, Rejected = 5, Expired = 6, Stale = 7 }`.
  - `AssistantProposalPayload(Guid? PlayerId, string? PlayerName, string? Dimension, int? Round, int? Points)`, `AssistantProposalExpectation(int? CellValue, int? TurnIndex)`, `AssistantProposalConfirmation(int Points)`.
  - `AssistantProposal.ForScore(Guid liveSessionId, Guid chatThreadId, Guid messageId, Guid profileVersionId, Guid playerId, string playerName, string dimension, int round, int points, int cellValueBefore, DateTime now)`.
  - `AssistantProposal.ForAdvanceTurn(Guid liveSessionId, Guid chatThreadId, Guid messageId, Guid profileVersionId, int currentTurnIndex, DateTime now)`.
  - `BeginApplying(Guid userId)`, `MarkAccepted(DateTime now)`, `MarkEdited(int confirmedPoints, DateTime now)`, `MarkFailed(string failureCode, DateTime now)`, `MarkStale(DateTime now)`, `Reject(Guid userId, DateTime now)`, `Expire(DateTime now)`.
  - `int ScoreValueToWrite(int? correctedPoints)`, `bool IsOpen`, costanti `AssistantProposal.NotPendingCode = "proposal_not_pending"`, `AssistantProposal.MaxPointsPerProposal = 100_000` e `AssistantProposal.MaxCellValue = 1_000_000_000`.

- [ ] **Step 1: Scrivi i test che falliscono**

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Spec 2026-10-11 §5.2 e §5.3: la proposta dell'assistente è l'unica cosa che l'assistente produce oltre
/// al testo. La macchina a stati è la garanzia che una proposta non si applichi due volte.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4275")]
public sealed class AssistantProposalTests
{
    private static readonly Guid Session = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Thread = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid Message = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
    private static readonly Guid Version = Guid.Parse("00000000-0000-0000-0000-0000000000a4");
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Host = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly DateTime Now = new(2026, 10, 12, 20, 0, 0, DateTimeKind.Utc);

    private static AssistantProposal Score(int points = 5, int cellBefore = 7) =>
        AssistantProposal.ForScore(Session, Thread, Message, Version, Marco, "Marco", "punti", 2, points, cellBefore, Now);

    private static AssistantProposal Turn() =>
        AssistantProposal.ForAdvanceTurn(Session, Thread, Message, Version, currentTurnIndex: 4, Now);

    [Fact]
    public void ForScore_IsPending_AndKeepsWhatTheHostWillConfirm()
    {
        var proposal = Score();

        proposal.Status.Should().Be(AssistantProposalStatus.Pending);
        proposal.Kind.Should().Be(AssistantProposalKind.Score);
        proposal.Payload.Should().Be(new AssistantProposalPayload(Marco, "Marco", "punti", 2, 5));
        proposal.Expected.Should().Be(new AssistantProposalExpectation(CellValue: 7, TurnIndex: null));
        proposal.LiveSessionId.Should().Be(Session);
        proposal.ChatThreadId.Should().Be(Thread);
        proposal.MessageId.Should().Be(Message);
        proposal.ProfileVersionId.Should().Be(Version);
        proposal.CreatedAt.Should().Be(Now);
        proposal.IsOpen.Should().BeTrue();
        proposal.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void ForAdvanceTurn_ExpectsTheTurnIndexItWasProposedOn()
    {
        var proposal = Turn();

        proposal.Kind.Should().Be(AssistantProposalKind.AdvanceTurn);
        proposal.Payload.Should().Be(new AssistantProposalPayload(null, null, null, null, null));
        proposal.Expected.Should().Be(new AssistantProposalExpectation(CellValue: null, TurnIndex: 4));
    }

    [Theory]
    [InlineData("", "punti", 1)]
    [InlineData("Marco", " ", 1)]
    [InlineData("Marco", "punti", 0)]
    public void ForScore_WithoutAPlayerNameADimensionOrAValidRound_IsRejected(string name, string dimension, int round)
    {
        var act = () => AssistantProposal.ForScore(Session, Thread, Message, Version, Marco, name, dimension, round, 5, 0, Now);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void NegativePoints_AreAValidDelta()
    {
        var proposal = Score(points: -2, cellBefore: 7);

        proposal.ScoreValueToWrite(correctedPoints: null).Should().Be(5);
    }

    /// <summary>
    /// I punti li scrive un modello: un numero assurdo, sommato alla cella, farebbe il giro dell'intero e
    /// finirebbe nel punteggio vero. Il limite vale per la proposta e per la correzione dell'host.
    /// </summary>
    [Theory]
    [InlineData(AssistantProposal.MaxPointsPerProposal + 1)]
    [InlineData(-AssistantProposal.MaxPointsPerProposal - 1)]
    [InlineData(int.MinValue)]
    public void PointsBeyondTheLimit_AreRejected_InTheProposalAndInTheCorrection(int points)
    {
        var propose = () => Score(points: points);
        var reserved = Score();
        reserved.BeginApplying(Host);
        var correct = () => reserved.MarkEdited(points, Now);

        propose.Should().Throw<ValidationException>();
        correct.Should().Throw<ValidationException>();
        reserved.Status.Should().Be(AssistantProposalStatus.Applying, "una correzione rifiutata non chiude la proposta");
    }

    /// <summary>Con la cella entro il suo limite e i punti entro il loro, la somma non può fare il giro dell'intero.</summary>
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    [InlineData(AssistantProposal.MaxCellValue + 1)]
    public void ACellBeyondAnyRealScore_IsRejected(int cellBefore)
    {
        var act = () => Score(points: 5, cellBefore: cellBefore);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void ScoreValueToWrite_AddsTheDeltaToTheExpectedCell_OrTheCorrectedOne()
    {
        var proposal = Score(points: 5, cellBefore: 7);

        proposal.ScoreValueToWrite(correctedPoints: null).Should().Be(12);
        proposal.ScoreValueToWrite(correctedPoints: 3).Should().Be(10);
    }

    [Fact]
    public void Confirm_GoesThroughApplying_AndEndsAccepted()
    {
        var proposal = Score();

        proposal.BeginApplying(Host);
        proposal.Status.Should().Be(AssistantProposalStatus.Applying);
        proposal.ResolvedBy.Should().Be(Host);
        proposal.IsOpen.Should().BeTrue("la prenotazione non è ancora un esito");

        proposal.MarkAccepted(Now.AddSeconds(1));

        proposal.Status.Should().Be(AssistantProposalStatus.Accepted);
        proposal.ResolvedAt.Should().Be(Now.AddSeconds(1));
        proposal.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Confirm_WithACorrection_EndsEdited_AndKeepsTheConfirmedPoints()
    {
        var proposal = Score(points: 5);
        proposal.BeginApplying(Host);

        proposal.MarkEdited(confirmedPoints: 3, Now.AddSeconds(1));

        proposal.Status.Should().Be(AssistantProposalStatus.Edited);
        proposal.ConfirmedPayload.Should().Be(new AssistantProposalConfirmation(3));
        proposal.Payload.Points.Should().Be(5, "la proposta originale resta leggibile");
    }

    [Fact]
    public void ATurnProposal_CannotBeEdited()
    {
        var proposal = Turn();
        proposal.BeginApplying(Host);

        var act = () => proposal.MarkEdited(1, Now);

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void ASecondConfirm_IsRejectedWithTheNotPendingCode()
    {
        var proposal = Score();
        proposal.BeginApplying(Host);

        var act = () => proposal.BeginApplying(Host);

        act.Should().Throw<ConflictException>().Which.ErrorCode.Should().Be(AssistantProposal.NotPendingCode);
    }

    [Fact]
    public void Failed_KeepsTheDomainCode()
    {
        var proposal = Score();
        proposal.BeginApplying(Host);

        proposal.MarkFailed("session_state_invalid", Now.AddSeconds(1));

        proposal.Status.Should().Be(AssistantProposalStatus.Failed);
        proposal.FailureCode.Should().Be("session_state_invalid");
        proposal.ResolvedAt.Should().Be(Now.AddSeconds(1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stale_IsReachableFromPendingAndFromApplying(bool reserved)
    {
        var proposal = Score();
        if (reserved)
        {
            proposal.BeginApplying(Host);
        }

        proposal.MarkStale(Now.AddSeconds(1));

        proposal.Status.Should().Be(AssistantProposalStatus.Stale);
    }

    [Fact]
    public void RejectAndExpire_OnlyFromPending()
    {
        var rejected = Score();
        rejected.Reject(Host, Now.AddSeconds(1));
        rejected.Status.Should().Be(AssistantProposalStatus.Rejected);
        rejected.ResolvedBy.Should().Be(Host);

        var expired = Score();
        expired.Expire(Now.AddSeconds(1));
        expired.Status.Should().Be(AssistantProposalStatus.Expired);

        var reserved = Score();
        reserved.BeginApplying(Host);
        var rejectReserved = () => reserved.Reject(Host, Now);
        var expireReserved = () => reserved.Expire(Now);
        rejectReserved.Should().Throw<ConflictException>();
        expireReserved.Should().Throw<ConflictException>();
    }

    public static TheoryData<string> ClosedStatuses() => new() { "Accepted", "Edited", "Failed", "Rejected", "Expired", "Stale" };

    [Theory]
    [MemberData(nameof(ClosedStatuses))]
    public void AClosedProposal_AcceptsNoFurtherTransition(string status)
    {
        var proposal = Closed(status);

        var transitions = new Action[]
        {
            () => proposal.BeginApplying(Host),
            () => proposal.MarkAccepted(Now),
            () => proposal.MarkEdited(1, Now),
            () => proposal.MarkFailed("x", Now),
            () => proposal.MarkStale(Now),
            () => proposal.Reject(Host, Now),
            () => proposal.Expire(Now),
        };

        foreach (var transition in transitions)
        {
            transition.Should().Throw<ConflictException>($"una proposta {status} è chiusa");
        }

        proposal.Status.ToString().Should().Be(status);
    }

    private static AssistantProposal Closed(string status)
    {
        var proposal = Score();
        switch (status)
        {
            case "Accepted": proposal.BeginApplying(Host); proposal.MarkAccepted(Now); break;
            case "Edited": proposal.BeginApplying(Host); proposal.MarkEdited(3, Now); break;
            case "Failed": proposal.BeginApplying(Host); proposal.MarkFailed("x", Now); break;
            case "Rejected": proposal.Reject(Host, Now); break;
            case "Expired": proposal.Expire(Now); break;
            default: proposal.MarkStale(Now); break;
        }

        return proposal;
    }
}
```

- [ ] **Step 2: Verifica che non compili per i tipi mancanti**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AssistantProposalTests"`
Expected: errori `CS0246` su `AssistantProposal`, `AssistantProposalStatus`, `AssistantProposalPayload`.

- [ ] **Step 3: Scrivi enum e value object**

`KB/Domain/Enums/AssistantProposalKind.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Domain.Enums;

/// <summary>Che cosa propone l'assistente in partita (spec 2026-10-11 §5.2). I valori sono persistiti.</summary>
public enum AssistantProposalKind
{
    Score = 0,
    AdvanceTurn = 1,
}
```

`KB/Domain/Enums/AssistantProposalStatus.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Domain.Enums;

/// <summary>
/// Stati di una proposta (spec 2026-10-11 §5.2). <c>Pending</c> → <c>Applying</c> → <c>Accepted</c>,
/// <c>Edited</c> o <c>Failed</c>; da <c>Pending</c> anche <c>Rejected</c> ed <c>Expired</c>; <c>Stale</c>
/// da <c>Pending</c> o da <c>Applying</c>. I valori sono persistiti.
/// </summary>
public enum AssistantProposalStatus
{
    Pending = 0,
    Applying = 1,
    Accepted = 2,
    Edited = 3,
    Failed = 4,
    Rejected = 5,
    Expired = 6,
    Stale = 7,
}
```

`KB/Domain/ValueObjects/AssistantProposalPayload.cs`:

```csharp
#pragma warning disable MA0048 // File name must match type name - the three jsonb documents of a proposal
namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>Ciò che la proposta scriverebbe. Per <c>AdvanceTurn</c> tutti i campi sono nulli.</summary>
public sealed record AssistantProposalPayload(Guid? PlayerId, string? PlayerName, string? Dimension, int? Round, int? Points);

/// <summary>
/// Lo stato della partita su cui la proposta è nata: il valore della cella per <c>Score</c>, l'indice di
/// turno per <c>AdvanceTurn</c>. Se alla conferma è cambiato, la proposta è superata.
/// </summary>
public sealed record AssistantProposalExpectation(int? CellValue, int? TurnIndex);

/// <summary>L'incremento che l'host ha confermato, quando ha corretto quello proposto.</summary>
public sealed record AssistantProposalConfirmation(int Points);
```

- [ ] **Step 4: Scrivi l'aggregato**

`KB/Domain/Entities/AssistantProposal.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Una proposta dell'assistente in partita (spec 2026-10-11 §5.2): un punteggio o un passaggio di turno
/// che l'host deve confermare. L'assistente non scrive mai la partita: crea questo.
/// <para>
/// La prenotazione (<see cref="BeginApplying"/>) è l'idempotenza della conferma: si salva con
/// <see cref="Xmin"/>, e un secondo tap concorrente perde sulla concorrenza invece di applicare due volte.
/// </para>
/// </summary>
public sealed class AssistantProposal : AggregateRoot<Guid>
{
    public const string NotPendingCode = "proposal_not_pending";
    public const int MaxFailureCodeLength = 64;

    /// <summary>
    /// Il massimo, in valore assoluto, dei punti di una proposta e della sua correzione. I punti li scrive
    /// un modello, e si sommano alla cella: senza un limite un numero assurdo farebbe il giro dell'intero.
    /// </summary>
    public const int MaxPointsPerProposal = 100_000;

    /// <summary>
    /// Il massimo, in valore assoluto, della cella su cui nasce una proposta. Con questo e con
    /// <see cref="MaxPointsPerProposal"/> la somma di <see cref="ScoreValueToWrite"/> sta in un intero.
    /// </summary>
    public const int MaxCellValue = 1_000_000_000;

    public Guid LiveSessionId { get; private set; }
    public Guid ChatThreadId { get; private set; }

    /// <summary>Il messaggio dell'assistente che l'ha prodotta: il suo id nasce prima del ciclo.</summary>
    public Guid MessageId { get; private set; }

    public AssistantProposalKind Kind { get; private set; }
    public AssistantProposalPayload Payload { get; private set; }
    public AssistantProposalExpectation Expected { get; private set; }
    public AssistantProposalStatus Status { get; private set; }
    public AssistantProposalConfirmation? ConfirmedPayload { get; private set; }
    public string? FailureCode { get; private set; }
    public Guid ProfileVersionId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public Guid? ResolvedBy { get; private set; }

    /// <summary>Concorrenza ottimistica sulla colonna di sistema <c>xmin</c> (ADR-060).</summary>
    public uint Xmin { get; private set; }

    /// <summary>Né accettata né chiusa in altro modo: ancora confermabile, o in corso di applicazione.</summary>
    public bool IsOpen => Status is AssistantProposalStatus.Pending or AssistantProposalStatus.Applying;

#pragma warning disable CS8618
    private AssistantProposal() : base() { } // EF Core
#pragma warning restore CS8618

    private AssistantProposal(
        Guid liveSessionId,
        Guid chatThreadId,
        Guid messageId,
        Guid profileVersionId,
        AssistantProposalKind kind,
        AssistantProposalPayload payload,
        AssistantProposalExpectation expected,
        DateTime now)
        : base(Guid.NewGuid())
    {
        Require(liveSessionId != Guid.Empty, nameof(liveSessionId), "Live session is required.");
        Require(chatThreadId != Guid.Empty, nameof(chatThreadId), "Chat thread is required.");
        Require(messageId != Guid.Empty, nameof(messageId), "Message is required.");
        Require(profileVersionId != Guid.Empty, nameof(profileVersionId), "Profile version is required.");

        LiveSessionId = liveSessionId;
        ChatThreadId = chatThreadId;
        MessageId = messageId;
        ProfileVersionId = profileVersionId;
        Kind = kind;
        Payload = payload;
        Expected = expected;
        Status = AssistantProposalStatus.Pending;
        CreatedAt = now;

        // Una riga mai persistita non ha ancora un xmin; assegnarlo qui evita S1144 sul setter (#3688).
        Xmin = 0;
    }

    public static AssistantProposal ForScore(
        Guid liveSessionId,
        Guid chatThreadId,
        Guid messageId,
        Guid profileVersionId,
        Guid playerId,
        string playerName,
        string dimension,
        int round,
        int points,
        int cellValueBefore,
        DateTime now)
    {
        Require(playerId != Guid.Empty, nameof(playerId), "Player is required.");
        Require(!string.IsNullOrWhiteSpace(playerName), nameof(playerName), "Player name is required.");
        Require(!string.IsNullOrWhiteSpace(dimension), nameof(dimension), "Dimension is required.");
        Require(round >= 1, nameof(round), "Round starts at 1.");
        Require(IsWithinTheLimit(points), nameof(points), $"Points must be between -{MaxPointsPerProposal} and {MaxPointsPerProposal}.");
        Require(Math.Abs((long)cellValueBefore) <= MaxCellValue, nameof(cellValueBefore),
            $"The current score must be between -{MaxCellValue} and {MaxCellValue}.");

        return new AssistantProposal(
            liveSessionId, chatThreadId, messageId, profileVersionId, AssistantProposalKind.Score,
            new AssistantProposalPayload(playerId, playerName.Trim(), dimension.Trim(), round, points),
            new AssistantProposalExpectation(cellValueBefore, TurnIndex: null),
            now);
    }

    public static AssistantProposal ForAdvanceTurn(
        Guid liveSessionId, Guid chatThreadId, Guid messageId, Guid profileVersionId, int currentTurnIndex, DateTime now) =>
        new(
            liveSessionId, chatThreadId, messageId, profileVersionId, AssistantProposalKind.AdvanceTurn,
            new AssistantProposalPayload(null, null, null, null, null),
            new AssistantProposalExpectation(CellValue: null, currentTurnIndex),
            now);

    /// <summary>
    /// Il valore da scrivere nella cella: quello atteso più l'incremento, proposto o corretto dall'host.
    /// <c>RecordScore</c> sostituisce la cella, quindi l'incremento va sommato qui (spec §5.1).
    /// </summary>
    public int ScoreValueToWrite(int? correctedPoints)
    {
        EnsureKind(AssistantProposalKind.Score, "compute a score for");
        return (Expected.CellValue ?? 0) + (correctedPoints ?? Payload.Points ?? 0);
    }

    /// <summary>La prenotazione: da qui in poi un'altra conferma trova la proposta non più <c>Pending</c>.</summary>
    public void BeginApplying(Guid userId)
    {
        EnsureStatus(AssistantProposalStatus.Pending, "confirm");
        Status = AssistantProposalStatus.Applying;
        ResolvedBy = userId;
    }

    public void MarkAccepted(DateTime now)
    {
        EnsureStatus(AssistantProposalStatus.Applying, "accept");
        Close(AssistantProposalStatus.Accepted, now);
    }

    public void MarkEdited(int confirmedPoints, DateTime now)
    {
        EnsureKind(AssistantProposalKind.Score, "edit");
        EnsureStatus(AssistantProposalStatus.Applying, "edit");
        Require(IsWithinTheLimit(confirmedPoints), nameof(confirmedPoints),
            $"Points must be between -{MaxPointsPerProposal} and {MaxPointsPerProposal}.");
        ConfirmedPayload = new AssistantProposalConfirmation(confirmedPoints);
        Close(AssistantProposalStatus.Edited, now);
    }

    public void MarkFailed(string failureCode, DateTime now)
    {
        Require(!string.IsNullOrWhiteSpace(failureCode) && failureCode.Length <= MaxFailureCodeLength,
            nameof(failureCode), $"Failure code is required, up to {MaxFailureCodeLength} characters.");
        EnsureStatus(AssistantProposalStatus.Applying, "fail");
        FailureCode = failureCode;
        Close(AssistantProposalStatus.Failed, now);
    }

    /// <summary>Lo stato della partita non è più quello su cui la proposta è nata.</summary>
    public void MarkStale(DateTime now)
    {
        if (!IsOpen)
        {
            throw NotOpen("mark as stale");
        }

        Close(AssistantProposalStatus.Stale, now);
    }

    public void Reject(Guid userId, DateTime now)
    {
        EnsureStatus(AssistantProposalStatus.Pending, "reject");
        ResolvedBy = userId;
        Close(AssistantProposalStatus.Rejected, now);
    }

    /// <summary>
    /// Inizia un altro turno dell'assistente (spec §5.2). Qui la transizione è definita per l'aggregato;
    /// in produzione la applica il repository a tutte le proposte in attesa di una sessione, con un solo
    /// UPDATE, perché una conferma nello stesso istante non deve far fallire il turno (Task 8 del piano 2).
    /// </summary>
    public void Expire(DateTime now)
    {
        EnsureStatus(AssistantProposalStatus.Pending, "expire");
        Close(AssistantProposalStatus.Expired, now);
    }

    private void Close(AssistantProposalStatus status, DateTime now)
    {
        Status = status;
        ResolvedAt = now;
    }

    private void EnsureStatus(AssistantProposalStatus expected, string action)
    {
        if (Status != expected)
        {
            throw NotOpen(action);
        }
    }

    private void EnsureKind(AssistantProposalKind expected, string action)
    {
        if (Kind != expected)
        {
            throw new ConflictException($"Cannot {action} assistant proposal {Id}: it is a {Kind} proposal.");
        }
    }

    private ConflictException NotOpen(string action) =>
        new(NotPendingCode, $"Cannot {action} assistant proposal {Id}: it is {Status}.");

    // Su long: Math.Abs(int.MinValue) lancerebbe.
    private static bool IsWithinTheLimit(int points) => Math.Abs((long)points) <= MaxPointsPerProposal;

    private static void Require(bool condition, string propertyName, string message)
    {
        if (!condition)
        {
            throw new ValidationException(propertyName, message);
        }
    }
}
```

- [ ] **Step 5: Verifica che i test passino**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AssistantProposalTests"`
Expected: PASS. Annota il numero di test.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Entities/AssistantProposalTests.cs
git commit -m "feat(ai): aggregato AssistantProposal e la sua macchina a stati (#4275)"
```

### Task 2: Persistenza di `AssistantProposal`

**Files:**
- Create: `KB/Domain/Repositories/IAssistantProposalRepository.cs`
- Create: `KB/Infrastructure/Persistence/AssistantProposalJson.cs`
- Create: `KB/Infrastructure/Persistence/Configurations/AssistantProposalConfiguration.cs`
- Create: `KB/Infrastructure/Persistence/AssistantProposalRepository.cs`
- Modify: `KB/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs` (accanto alla riga che registra `IAgentProfileRepository`)
- Modify: `TESTS/Infrastructure/ConcurrencyTokenWritePathInventoryTests.cs` (una voce in `DeclaredWritePaths`)
- Create (generata): `API/Infrastructure/Migrations/<timestamp>_AddAssistantProposals.cs` con il suo `Designer` e lo snapshot aggiornato
- Test: `TESTS/BoundedContexts/KnowledgeBase/Infrastructure/AssistantProposalJsonTests.cs`
- Test: `TESTS/Integration/KnowledgeBase/SessionAssistant/AssistantProposalRepositoryIntegrationTests.cs`

**Interfaces:**
- Consumes: `AssistantProposal` e i suoi record (Task 1); `MeepleAiDbContext`; `IUnitOfWork` (`Api.SharedKernel.Infrastructure.Persistence`); il precedente `AgentProfileConfiguration` per `xmin`.
- Produces:

```csharp
public interface IAssistantProposalRepository
{
    Task<AssistantProposal?> GetAsync(Guid id, CancellationToken cancellationToken = default);                // tracciata
    Task<AssistantProposal?> GetReadOnlyAsync(Guid id, CancellationToken cancellationToken = default);        // non tracciata: lo stato attuale
    Task<IReadOnlyList<AssistantProposal>> ListOpenBySessionAsync(Guid liveSessionId, CancellationToken cancellationToken = default);   // tracciate, Pending e Applying
    Task<IReadOnlyList<AssistantProposal>> ListByThreadAsync(Guid chatThreadId, CancellationToken cancellationToken = default);        // sola lettura
    Task AddAsync(AssistantProposal proposal, CancellationToken cancellationToken = default);
}
```

- Tabella `knowledge_base.assistant_proposals`, colonne: `id`, `live_session_id`, `chat_thread_id`, `message_id`, `kind`, `payload` (jsonb), `expected` (jsonb), `status`, `confirmed_payload` (jsonb, nullable), `failure_code` (nullable, 64), `profile_version_id`, `created_at`, `resolved_at`, `resolved_by`, più `xmin` di sistema. Indici `ix_assistant_proposals_session_status` (`live_session_id`, `status`) e `ix_assistant_proposals_thread` (`chat_thread_id`).

- [ ] **Step 1: Scrivi il test della forma jsonb (unit)**

`TESTS/BoundedContexts/KnowledgeBase/Infrastructure/AssistantProposalJsonTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure;

/// <summary>
/// La forma jsonb delle tre colonne di <c>assistant_proposals</c>. È un contratto persistito: rinominare una
/// proprietà del dominio non deve cambiare ciò che è scritto nel database.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4275")]
public sealed class AssistantProposalJsonTests
{
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Fact]
    public void Payload_IsCamelCase_AndSurvivesARoundTrip()
    {
        var payload = new AssistantProposalPayload(Marco, "Marco", "punti", 2, -3);

        var json = AssistantProposalJson.Serialize(payload);

        json.Should().Be("""{"playerId":"00000000-0000-0000-0000-000000000001","playerName":"Marco","dimension":"punti","round":2,"points":-3}""");
        AssistantProposalJson.DeserializePayload(json).Should().Be(payload);
    }

    [Fact]
    public void Expectation_AndConfirmation_SurviveARoundTrip()
    {
        var expected = new AssistantProposalExpectation(CellValue: 7, TurnIndex: null);
        var confirmed = new AssistantProposalConfirmation(3);

        AssistantProposalJson.DeserializeExpectation(AssistantProposalJson.Serialize(expected)).Should().Be(expected);
        AssistantProposalJson.DeserializeConfirmation(AssistantProposalJson.Serialize(confirmed)).Should().Be(confirmed);
        AssistantProposalJson.Serialize(expected).Should().Be("""{"cellValue":7,"turnIndex":null}""");
    }
}
```

- [ ] **Step 2: Verifica che fallisca**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AssistantProposalJsonTests"`
Expected: `CS0103` su `AssistantProposalJson`.

- [ ] **Step 3: Scrivi il serializzatore, la configurazione EF, il repository e la registrazione**

`KB/Infrastructure/Persistence/AssistantProposalJson.cs`:

```csharp
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Forma jsonb di <c>payload</c>, <c>expected</c> e <c>confirmed_payload</c> in
/// <c>knowledge_base.assistant_proposals</c> (spec 2026-10-11 §5.2). Nomi in camelCase, espliciti: è un
/// contratto persistito.
/// </summary>
internal static class AssistantProposalJson
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Serialize(AssistantProposalPayload payload) => JsonSerializer.Serialize(payload, Options);

    public static string Serialize(AssistantProposalExpectation expected) => JsonSerializer.Serialize(expected, Options);

    public static string Serialize(AssistantProposalConfirmation confirmed) => JsonSerializer.Serialize(confirmed, Options);

    public static AssistantProposalPayload DeserializePayload(string json) =>
        JsonSerializer.Deserialize<AssistantProposalPayload>(json, Options)
        ?? throw new JsonException("Assistant proposal payload is null.");

    public static AssistantProposalExpectation DeserializeExpectation(string json) =>
        JsonSerializer.Deserialize<AssistantProposalExpectation>(json, Options)
        ?? throw new JsonException("Assistant proposal expectation is null.");

    public static AssistantProposalConfirmation DeserializeConfirmation(string json) =>
        JsonSerializer.Deserialize<AssistantProposalConfirmation>(json, Options)
        ?? throw new JsonException("Assistant proposal confirmation is null.");
}
```

`KB/Infrastructure/Persistence/Configurations/AssistantProposalConfiguration.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>knowledge_base.assistant_proposals</c> (spec 2026-10-11 §5.2). Nessuna chiave esterna verso la
/// sessione live o il thread: sono aggregati di altri confini, citati per id.
/// </summary>
public sealed class AssistantProposalConfiguration : IEntityTypeConfiguration<AssistantProposal>
{
    public void Configure(EntityTypeBuilder<AssistantProposal> builder)
    {
        builder.ToTable("assistant_proposals", "knowledge_base");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.LiveSessionId).HasColumnName("live_session_id").IsRequired();
        builder.Property(p => p.ChatThreadId).HasColumnName("chat_thread_id").IsRequired();
        builder.Property(p => p.MessageId).HasColumnName("message_id").IsRequired();
        builder.Property(p => p.Kind).HasColumnName("kind").IsRequired();
        builder.Property(p => p.Status).HasColumnName("status").IsRequired();

        builder.Property(p => p.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .HasConversion(payload => AssistantProposalJson.Serialize(payload), json => AssistantProposalJson.DeserializePayload(json))
            .IsRequired();

        builder.Property(p => p.Expected)
            .HasColumnName("expected")
            .HasColumnType("jsonb")
            .HasConversion(expected => AssistantProposalJson.Serialize(expected), json => AssistantProposalJson.DeserializeExpectation(json))
            .IsRequired();

        builder.Property(p => p.ConfirmedPayload)
            .HasColumnName("confirmed_payload")
            .HasColumnType("jsonb")
            .HasConversion(
                confirmed => confirmed == null ? null : AssistantProposalJson.Serialize(confirmed),
                json => json == null ? null : AssistantProposalJson.DeserializeConfirmation(json));

        builder.Property(p => p.FailureCode).HasColumnName("failure_code").HasMaxLength(AssistantProposal.MaxFailureCodeLength);
        builder.Property(p => p.ProfileVersionId).HasColumnName("profile_version_id").IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(p => p.ResolvedBy).HasColumnName("resolved_by");

        // Pattern xmin di ADR-060: la prenotazione della conferma si regge su questo controllo.
        builder.Property(p => p.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(p => new { p.LiveSessionId, p.Status }).HasDatabaseName("ix_assistant_proposals_session_status");
        builder.HasIndex(p => p.ChatThreadId).HasDatabaseName("ix_assistant_proposals_thread");

        builder.Ignore(p => p.IsOpen);
    }
}
```

`KB/Domain/Repositories/IAssistantProposalRepository.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository di <see cref="AssistantProposal"/> (spec 2026-10-11 §5.2). Pattern UoW di ADR-056: i metodi
/// che scrivono preparano soltanto le modifiche; il chiamante invoca
/// <see cref="Api.SharedKernel.Infrastructure.Persistence.IUnitOfWork.SaveChangesAsync"/>.
/// </summary>
public interface IAssistantProposalRepository
{
    /// <summary>La proposta, tracciata: le transizioni fatte dall'aggregato si salvano con la unit of work.</summary>
    Task<AssistantProposal?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// La proposta com'è adesso nel database, non tracciata: per rispondere con lo stato attuale dopo aver
    /// perso una corsa sulla concorrenza, quando l'entità tracciata è ormai vecchia.
    /// </summary>
    Task<AssistantProposal?> GetReadOnlyAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Le proposte ancora aperte di una sessione (<c>Pending</c> e <c>Applying</c>), tracciate.</summary>
    Task<IReadOnlyList<AssistantProposal>> ListOpenBySessionAsync(Guid liveSessionId, CancellationToken cancellationToken = default);

    /// <summary>Tutte le proposte nate in un thread, in sola lettura: servono alla storia del turno.</summary>
    Task<IReadOnlyList<AssistantProposal>> ListByThreadAsync(Guid chatThreadId, CancellationToken cancellationToken = default);

    Task AddAsync(AssistantProposal proposal, CancellationToken cancellationToken = default);
}
```

`KB/Infrastructure/Persistence/AssistantProposalRepository.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF implementation of <see cref="IAssistantProposalRepository"/>. Le proposte da modificare si caricano
/// tracciate e non si riattaccano mai con <c>Update()</c>: su un'entità detached con <c>xmin</c> l'Update
/// rompe le scritture (ADR-060).
/// </summary>
internal sealed class AssistantProposalRepository : IAssistantProposalRepository
{
    private readonly MeepleAiDbContext _db;

    public AssistantProposalRepository(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    // AsTracking esplicito: MeepleAiDbContext è NoTracking per default (PERF-06, #3866).
    public Task<AssistantProposal?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Set<AssistantProposal>().AsTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<AssistantProposal?> GetReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Set<AssistantProposal>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AssistantProposal>> ListOpenBySessionAsync(
        Guid liveSessionId, CancellationToken cancellationToken = default) =>
        await _db.Set<AssistantProposal>()
            .AsTracking()
            .Where(p => p.LiveSessionId == liveSessionId
                && (p.Status == AssistantProposalStatus.Pending || p.Status == AssistantProposalStatus.Applying))
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<AssistantProposal>> ListByThreadAsync(
        Guid chatThreadId, CancellationToken cancellationToken = default) =>
        await _db.Set<AssistantProposal>()
            .AsNoTracking()
            .Where(p => p.ChatThreadId == chatThreadId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(AssistantProposal proposal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        await _db.Set<AssistantProposal>().AddAsync(proposal, cancellationToken).ConfigureAwait(false);
    }
}
```

In `KnowledgeBaseServiceExtensions.cs`, subito dopo la riga `services.AddScoped<IAgentProfileRepository, AgentProfileRepository>();`:

```csharp
        services.AddScoped<IAssistantProposalRepository, AssistantProposalRepository>(); // #4275: proposte dell'assistente in partita
```

In `ConcurrencyTokenWritePathInventoryTests.cs`, nel dizionario `DeclaredWritePaths`, sotto la voce `["AgentProfile"]`: ogni entità con un token `xmin` deve dichiarare come il suo percorso di scrittura lo preserva, altrimenti `EveryXminTokenDeclaresHowItsWritePathPreservesIt` fallisce.

```csharp
            ["AssistantProposal"] = (WritePathTechnique.TrackedMutation, "AssistantProposalRepository.GetAsync / ListOpenBySessionAsync — AsTracking esplicito; l'aggregato si muta e si salva con la UoW, mai Update() su detached. Provato su Postgres in AssistantProposalRepositoryIntegrationTests (#4275)"),
```

- [ ] **Step 4: Verifica che il test della forma jsonb passi**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AssistantProposalJsonTests"`
Expected: PASS, 2 test.

- [ ] **Step 5: Genera la migrazione**

```bash
cd apps/api/src/Api
dotnet restore
dotnet ef migrations add AddAssistantProposals
```

Expected: tre file toccati in `Infrastructure/Migrations/` (la migrazione, il suo `Designer`, lo snapshot). In `Up` ci devono essere **solo** `CreateTable(name: "assistant_proposals", schema: "knowledge_base", …)` con le colonne elencate sopra e i due `CreateIndex`; in `Down` il `DropTable`. Nel `CreateTable` compare anche `xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)`: **lascialo**. È una colonna di sistema di Postgres, e il provider Npgsql non la crea; il precedente è `20261010200230_AddAgentProfile.cs`, che ha la stessa riga. Non serve altro: nessun `EnsureSchema` nuovo (lo schema `knowledge_base` esiste già).

Se lo strumento ha generato altro (colonne di altre tabelle, rinomine), **fermati**: lo snapshot era fuori sincrono con il modello e va capito prima di proseguire.

- [ ] **Step 6: Scrivi il test di integrazione del repository**

`TESTS/Integration/KnowledgeBase/SessionAssistant/AssistantProposalRepositoryIntegrationTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase.SessionAssistant;

/// <summary>
/// Host e database una volta per classe: ogni test usa una sessione live con un id nuovo e legge solo le
/// proprie proposte, quindi condividere il database è sicuro.
/// </summary>
public sealed class AssistantProposalRepositoryHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "assistant_proposals");

/// <summary>
/// Spec 2026-10-11 §5.2 e §5.3, sul database vero: la proposta sopravvive al salvataggio con le sue tre
/// colonne jsonb, e la prenotazione della conferma perde sulla concorrenza quando arriva seconda.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4275")]
public sealed class AssistantProposalRepositoryIntegrationTests(AssistantProposalRepositoryHostFixture host)
    : IClassFixture<AssistantProposalRepositoryHostFixture>
{
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid HostUser = Guid.Parse("00000000-0000-0000-0000-0000000000b1");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AssistantProposal NewScore(Guid session, Guid thread) =>
        AssistantProposal.ForScore(session, thread, Guid.NewGuid(), Guid.NewGuid(), Marco, "Marco", "punti", 2, 5, 7, DateTime.UtcNow);

    private async Task<Guid> SaveAsync(AssistantProposal proposal)
    {
        await using var scope = host.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().AddAsync(proposal, Ct);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return proposal.Id;
    }

    [Fact]
    public async Task AProposal_SurvivesTheRoundTrip_WithItsJsonColumns()
    {
        var session = Guid.NewGuid();
        var id = await SaveAsync(NewScore(session, Guid.NewGuid()));

        await using var scope = host.Factory.Services.CreateAsyncScope();
        var loaded = await scope.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().GetAsync(id, Ct);

        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(AssistantProposalStatus.Pending);
        loaded.Payload.Should().Be(new AssistantProposalPayload(Marco, "Marco", "punti", 2, 5));
        loaded.Expected.Should().Be(new AssistantProposalExpectation(7, null));
        loaded.ConfirmedPayload.Should().BeNull();
        loaded.LiveSessionId.Should().Be(session);
    }

    [Fact]
    public async Task AnEditedProposal_KeepsTheConfirmedPoints_AndLeavesTheOpenList()
    {
        var session = Guid.NewGuid();
        var id = await SaveAsync(NewScore(session, Guid.NewGuid()));
        await SaveAsync(NewScore(session, Guid.NewGuid()));

        await using (var scope = host.Factory.Services.CreateAsyncScope())
        {
            var proposal = await scope.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().GetAsync(id, Ct);
            proposal!.BeginApplying(HostUser);
            proposal.MarkEdited(3, DateTime.UtcNow);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        }

        await using var verify = host.Factory.Services.CreateAsyncScope();
        var repository = verify.ServiceProvider.GetRequiredService<IAssistantProposalRepository>();
        var edited = await repository.GetAsync(id, Ct);
        var open = await repository.ListOpenBySessionAsync(session, Ct);

        edited!.Status.Should().Be(AssistantProposalStatus.Edited);
        edited.ConfirmedPayload.Should().Be(new AssistantProposalConfirmation(3));
        edited.ResolvedBy.Should().Be(HostUser);
        open.Should().ContainSingle().Which.Id.Should().NotBe(id);
    }

    [Fact]
    public async Task SecondConcurrentSave_FailsOnXmin()
    {
        // La prenotazione della conferma (spec §5.3): due richieste caricano la stessa proposta Pending,
        // entrambe la portano ad Applying, e solo la prima che salva vince.
        var id = await SaveAsync(NewScore(Guid.NewGuid(), Guid.NewGuid()));

        await using var first = host.Factory.Services.CreateAsyncScope();
        await using var second = host.Factory.Services.CreateAsyncScope();
        var a = await first.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().GetAsync(id, Ct);
        var b = await second.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().GetAsync(id, Ct);
        a!.BeginApplying(HostUser);
        b!.BeginApplying(Guid.NewGuid());

        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        var act = () => second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task ListByThread_ReturnsEveryProposalOfTheThread_InOrder()
    {
        var thread = Guid.NewGuid();
        var session = Guid.NewGuid();
        var firstId = await SaveAsync(NewScore(session, thread));
        var secondId = await SaveAsync(AssistantProposal.ForAdvanceTurn(session, thread, Guid.NewGuid(), Guid.NewGuid(), 4, DateTime.UtcNow.AddSeconds(1)));

        await using var scope = host.Factory.Services.CreateAsyncScope();
        var proposals = await scope.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().ListByThreadAsync(thread, Ct);

        proposals.Select(p => p.Id).Should().Equal(firstId, secondId);
    }
}
```

- [ ] **Step 7: Esegui il test di integrazione**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AssistantProposalRepositoryIntegrationTests"`
Expected: PASS, 4 test. Il database del test nasce applicando le migrazioni, quindi questo run verifica anche la migrazione del passo 5. Se `SecondConcurrentSave_FailsOnXmin` fallisce perché il secondo salvataggio **non** lancia, la colonna `xmin` non è mappata come token di concorrenza: ricontrolla la configurazione.

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ConcurrencyTokenWritePathInventoryTests|FullyQualifiedName~MeepleAiDbContextModelCanaryTests"`
Expected: PASS. Il primo dice che il token nuovo è dichiarato; il secondo che il modello EF si costruisce con l'entità nuova.

- [ ] **Step 8: Build Release e commit**

Run: `dotnet build apps/api/src/Api/Api.csproj -c Release` — Expected: 0 avvisi, 0 errori.

```bash
git add apps/api/src/Api apps/api/tests/Api.Tests
git commit -m "feat(ai): persistenza delle proposte dell'assistente (#4275)"
```

### Task 3: La sezione «Assistente in partita» del profilo

**Files:**
- Create: `KB/Domain/ValueObjects/SessionAssistantSettings.cs`
- Modify: `KB/Domain/ValueObjects/AgentProfileContent.cs`
- Modify: `KB/Infrastructure/Persistence/AgentProfileContentJson.cs`
- Modify: `TESTS/BoundedContexts/KnowledgeBase/Domain/Entities/AgentProfileTests.cs` (helper `Content`, nuovo helper `Assistant`)
- Modify: `TESTS/Integration/KnowledgeBase/AgentProfileFieldEffectTests.cs` (helper `Modified`)
- Test: `TESTS/BoundedContexts/KnowledgeBase/Domain/ValueObjects/SessionAssistantSettingsTests.cs`
- Test: `TESTS/BoundedContexts/KnowledgeBase/Domain/ValueObjects/AgentProfileContentSessionAssistantTests.cs`
- Test: `TESTS/BoundedContexts/KnowledgeBase/Infrastructure/AgentProfileContentJsonTests.cs` (tre test in più)

**Interfaces:**
- Consumes: `ValueObject` (`Api.SharedKernel.Domain.ValueObjects`), `ValidationException(propertyName, message)`, `AgentProfileContent.MaxResponseTokensLimit`.
- Produces:
  - `SessionAssistantSettings.Disabled` (statico), `SessionAssistantSettings.Create(bool enabled, IReadOnlyDictionary<string, string> systemPrompts, decimal temperature = 0.2m, int maxResponseTokens = 600, int maxToolIterations = 3, int turnTimeoutSeconds = 15, bool useReserveModel = false)`.
  - Proprietà: `Enabled`, `SystemPrompts`, `Temperature`, `MaxResponseTokens`, `MaxToolIterations`, `TurnTimeoutSeconds`, `UseReserveModel`; metodo `string? PromptFor(string? language)`.
  - `AgentProfileContent.SessionAssistant` (mai nullo); `AgentProfileContent.Create(..., IReadOnlyCollection<DocumentCategory> allowedCategories, SessionAssistantSettings? sessionAssistant = null)`.
  - Nel jsonb: chiave opzionale `sessionAssistant` con `enabled`, `systemPrompts`, `temperature`, `maxResponseTokens`, `maxToolIterations`, `turnTimeoutSeconds`, `useReserveModel`. Assente quando la sezione è `Disabled`.
  - Nei test: `AgentProfileTests.Content(decimal temperature = 0.3m, string model = "deepseek-chat", SessionAssistantSettings? assistant = null, string? fallbackModel = AgentProfileTests.ReserveModel)`, `AgentProfileTests.ReserveModel` e `AgentProfileTests.Assistant(bool enabled = true, bool useReserveModel = false)`.

- [ ] **Step 1: Scrivi i test del value object**

`TESTS/BoundedContexts/KnowledgeBase/Domain/ValueObjects/SessionAssistantSettingsTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Spec 2026-10-11 §3.2: la sezione «Assistente in partita» di una versione del profilo. I valori di
/// default sono quelli misurati dallo spike (§7.1): temperatura 0,2, 600 token, tre giri, 15 secondi.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4275")]
public sealed class SessionAssistantSettingsTests
{
    private static Dictionary<string, string> Prompts() => new(StringComparer.Ordinal)
    {
        [" IT "] = "Sei l'assistente al tavolo.",
        ["en"] = "You are the table assistant.",
    };

    [Fact]
    public void Disabled_IsOff_WithTheMeasuredDefaults()
    {
        var disabled = SessionAssistantSettings.Disabled;

        disabled.Enabled.Should().BeFalse();
        disabled.SystemPrompts.Should().BeEmpty();
        disabled.Temperature.Should().Be(0.2m);
        disabled.MaxResponseTokens.Should().Be(600);
        disabled.MaxToolIterations.Should().Be(3);
        disabled.TurnTimeoutSeconds.Should().Be(15);
        disabled.UseReserveModel.Should().BeFalse();
    }

    [Fact]
    public void Create_NormalizesTheLanguages_AndKeepsTheValues()
    {
        var settings = SessionAssistantSettings.Create(
            enabled: true, Prompts(), temperature: 0.4m, maxResponseTokens: 800, maxToolIterations: 4,
            turnTimeoutSeconds: 20, useReserveModel: true);

        settings.Enabled.Should().BeTrue();
        settings.SystemPrompts.Keys.Should().Equal("en", "it");
        settings.Temperature.Should().Be(0.4m);
        settings.MaxResponseTokens.Should().Be(800);
        settings.MaxToolIterations.Should().Be(4);
        settings.TurnTimeoutSeconds.Should().Be(20);
        settings.UseReserveModel.Should().BeTrue();
    }

    [Theory]
    [InlineData(2.1, 600, 3, 15)]
    [InlineData(-0.1, 600, 3, 15)]
    [InlineData(0.2, 0, 3, 15)]
    [InlineData(0.2, 600, 0, 15)]
    [InlineData(0.2, 600, 9, 15)]
    [InlineData(0.2, 600, 3, 4)]
    [InlineData(0.2, 600, 3, 121)]
    public void Create_OutOfRange_IsRejected(double temperature, int maxResponseTokens, int maxToolIterations, int turnTimeoutSeconds)
    {
        var act = () => SessionAssistantSettings.Create(
            true, Prompts(), (decimal)temperature, maxResponseTokens, maxToolIterations, turnTimeoutSeconds);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Create_Enabled_WithoutPrompts_IsRejected()
    {
        var act = () => SessionAssistantSettings.Create(true, new Dictionary<string, string>(StringComparer.Ordinal));

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Create_Disabled_WithoutPrompts_IsAllowed()
    {
        var settings = SessionAssistantSettings.Create(false, new Dictionary<string, string>(StringComparer.Ordinal));

        settings.Should().Be(SessionAssistantSettings.Disabled);
    }

    [Fact]
    public void Create_TheSameLanguageTwice_IsRejected()
    {
        var prompts = new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = "uno", ["IT "] = "due" };

        var act = () => SessionAssistantSettings.Create(true, prompts);

        act.Should().Throw<ValidationException>();
    }

    [Theory]
    [InlineData("it", "Sei l'assistente al tavolo.")]
    [InlineData("en-US", "You are the table assistant.")]
    [InlineData("EN_gb", "You are the table assistant.")]
    [InlineData("de", null)]
    [InlineData(null, null)]
    public void PromptFor_FallsBackFromTheRegionalVariantToTheLanguage(string? language, string? expected)
    {
        var settings = SessionAssistantSettings.Create(true, Prompts());

        settings.PromptFor(language).Should().Be(expected);
    }

    [Fact]
    public void TwoSettings_DifferingInOneField_AreNotEqual()
    {
        var a = SessionAssistantSettings.Create(true, Prompts());
        var b = SessionAssistantSettings.Create(true, Prompts(), maxToolIterations: 4);

        a.Should().NotBe(b);
        a.Should().Be(SessionAssistantSettings.Create(true, Prompts()));
    }
}
```

- [ ] **Step 2: Scrivi i test del contenuto e della forma jsonb**

In `AgentProfileTests.cs` sostituisci l'helper `Content` e aggiungi `Assistant` subito sotto:

```csharp
    internal const string ReserveModel = "meta-llama/llama-3.3-70b-instruct:free";

    internal static AgentProfileContent Content(
        decimal temperature = 0.3m,
        string model = "deepseek-chat",
        SessionAssistantSettings? assistant = null,
        string? fallbackModel = ReserveModel) =>
        AgentProfileContent.Create(
            name: "MeepleAI",
            description: "Assistente per le regole dei giochi da tavolo",
            persona: "a precise board game rules assistant",
            primaryModelId: model,
            fallbackModelId: fallbackModel,
            temperature: temperature,
            maxResponseTokens: 1500,
            citationStyle: CitationStyle.PageReferencesInText,
            systemPrompts: new Dictionary<string, string>(StringComparer.Ordinal) { ["en"] = "You are MeepleAI." },
            fallbackLanguage: "en",
            candidatePoolSize: 20,
            finalTopK: 5,
            minScore: 0.55m,
            vectorWeight: 0.7m,
            keywordWeight: 0.3m,
            rerankerEnabled: true,
            allowedCategories: [DocumentCategory.Rulebook, DocumentCategory.Errata],
            sessionAssistant: assistant);

    /// <summary>Una sezione «Assistente in partita» valida per <see cref="Content"/>, che ha «en» come lingua di riserva.</summary>
    internal static SessionAssistantSettings Assistant(bool enabled = true, bool useReserveModel = false) =>
        SessionAssistantSettings.Create(
            enabled,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["en"] = "You are the table assistant.",
                ["it"] = "Sei l'assistente al tavolo.",
            },
            useReserveModel: useReserveModel);
```

`TESTS/BoundedContexts/KnowledgeBase/Domain/ValueObjects/AgentProfileContentSessionAssistantTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Enums;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>Spec 2026-10-11 §3.2: la sezione dell'assistente dentro il contenuto di una versione del profilo.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4275")]
public sealed class AgentProfileContentSessionAssistantTests
{
    [Fact]
    public void Content_WithoutTheSection_HasTheAssistantDisabled()
    {
        AgentProfileTests.Content().SessionAssistant.Should().Be(SessionAssistantSettings.Disabled);
    }

    [Fact]
    public void Content_KeepsTheSectionItWasGiven()
    {
        var assistant = AgentProfileTests.Assistant();

        AgentProfileTests.Content(assistant: assistant).SessionAssistant.Should().Be(assistant);
    }

    [Fact]
    public void TwoContents_DifferingOnlyInTheAssistant_AreNotEqual()
    {
        var plain = AgentProfileTests.Content();
        var withAssistant = AgentProfileTests.Content(assistant: AgentProfileTests.Assistant());

        withAssistant.Should().NotBe(plain);
        withAssistant.Should().Be(AgentProfileTests.Content(assistant: AgentProfileTests.Assistant()));
    }

    [Fact]
    public void EnabledAssistant_WithoutAPromptForTheFallbackLanguage_IsRejected()
    {
        // Il contenuto di prova ha «en» come lingua di riserva: un assistente con il solo prompt italiano
        // lascerebbe senza prompt una sessione in una lingua non prevista.
        var onlyItalian = SessionAssistantSettings.Create(
            true, new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = "Sei l'assistente al tavolo." });

        var act = () => AgentProfileTests.Content(assistant: onlyItalian);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void DisabledAssistant_WithoutAPromptForTheFallbackLanguage_IsAllowed()
    {
        var disabled = SessionAssistantSettings.Create(
            false, new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = "Sei l'assistente al tavolo." });

        var content = AgentProfileTests.Content(assistant: disabled);

        content.SessionAssistant.Enabled.Should().BeFalse();
    }
}
```

In `AgentProfileContentJsonTests.cs` aggiungi `using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;` e, in fondo alla classe:

```csharp
    /// <summary>
    /// Review Focus 2: una versione salvata prima di questa fetta non ha la chiave. Deve caricarsi come
    /// «disabilitato», e un contenuto senza assistente non deve cominciare a scriverla: il jsonb delle
    /// versioni esistenti resta byte per byte quello di prima.
    /// </summary>
    [Fact]
    public void LegacyContentWithoutTheSection_LoadsAsDisabled()
    {
        var json = AgentProfileContentJson.Serialize(AgentProfileTests.Content());

        JsonNode.Parse(json)!.AsObject().ContainsKey("sessionAssistant").Should().BeFalse();
        AgentProfileContentJson.Deserialize(json).SessionAssistant.Should().Be(SessionAssistantSettings.Disabled);
    }

    [Fact]
    public void Content_WithTheAssistant_SurvivesARoundTrip()
    {
        var content = AgentProfileTests.Content(assistant: AgentProfileTests.Assistant(useReserveModel: true));

        var json = AgentProfileContentJson.Serialize(content);

        AgentProfileContentJson.Deserialize(json).Should().Be(content);
        var section = JsonNode.Parse(json)!["sessionAssistant"]!;
        section["enabled"]!.GetValue<bool>().Should().BeTrue();
        section["systemPrompts"]!["it"]!.GetValue<string>().Should().Be("Sei l'assistente al tavolo.");
        section["maxToolIterations"]!.GetValue<int>().Should().Be(3);
        section["turnTimeoutSeconds"]!.GetValue<int>().Should().Be(15);
        section["useReserveModel"]!.GetValue<bool>().Should().BeTrue();
    }

    /// <summary>
    /// La migrazione che abiliterà l'assistente scrive questo jsonb a mano: un campo numerico dimenticato
    /// deve valere il default misurato, non zero (zero giri vorrebbe dire un assistente senza strumenti).
    /// </summary>
    [Fact]
    public void SectionWithOnlyEnabledAndPrompts_TakesTheDefaults()
    {
        var json = JsonNode.Parse(AgentProfileContentJson.Serialize(AgentProfileTests.Content()))!;
        json["sessionAssistant"] = new JsonObject
        {
            ["enabled"] = true,
            ["systemPrompts"] = new JsonObject { ["en"] = "You are the table assistant." },
        };

        var assistant = AgentProfileContentJson.Deserialize(json.ToJsonString()).SessionAssistant;

        assistant.Enabled.Should().BeTrue();
        assistant.Temperature.Should().Be(0.2m);
        assistant.MaxResponseTokens.Should().Be(600);
        assistant.MaxToolIterations.Should().Be(3);
        assistant.TurnTimeoutSeconds.Should().Be(15);
        assistant.UseReserveModel.Should().BeFalse();
    }
```

- [ ] **Step 3: Verifica che non compili per i tipi mancanti**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SessionAssistantSettingsTests|FullyQualifiedName~AgentProfileContentSessionAssistantTests|FullyQualifiedName~AgentProfileContentJsonTests"`
Expected: errori `CS0246` su `SessionAssistantSettings` e `CS1739` sul parametro `sessionAssistant`.

- [ ] **Step 4: Scrivi il value object**

`KB/Domain/ValueObjects/SessionAssistantSettings.cs`:

```csharp
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// La sezione «Assistente in partita» di una versione del profilo (spec 2026-10-11 §3.2): se l'assistente
/// è abilitato, con quali prompt e con quali limiti gira un suo turno. Il modello è quello del profilo; la
/// riserva del profilo si usa solo se <see cref="UseReserveModel"/> lo dice.
/// <para>
/// I valori di default sono quelli misurati dallo spike (§7.1). Una versione senza questa sezione vale
/// <see cref="Disabled"/>.
/// </para>
/// </summary>
public sealed class SessionAssistantSettings : ValueObject
{
    public const decimal DefaultTemperature = 0.2m;
    public const int DefaultMaxResponseTokens = 600;
    public const int DefaultMaxToolIterations = 3;
    public const int DefaultTurnTimeoutSeconds = 15;
    public const int MaxToolIterationsLimit = 8;
    public const int MinTurnTimeoutSeconds = 5;
    public const int MaxTurnTimeoutSeconds = 120;

    private static readonly IReadOnlyDictionary<string, string> NoPrompts =
        new SortedDictionary<string, string>(StringComparer.Ordinal).AsReadOnly();

    /// <summary>L'assistente spento: è ciò che vale una versione del profilo senza la sezione.</summary>
    public static SessionAssistantSettings Disabled { get; } = new(
        enabled: false, NoPrompts, DefaultTemperature, DefaultMaxResponseTokens, DefaultMaxToolIterations,
        DefaultTurnTimeoutSeconds, useReserveModel: false);

    public bool Enabled { get; }
    public IReadOnlyDictionary<string, string> SystemPrompts { get; }
    public decimal Temperature { get; }
    public int MaxResponseTokens { get; }

    /// <summary>I giri con strumenti di un turno; dopo, una chiamata finale senza strumenti (ADR-097).</summary>
    public int MaxToolIterations { get; }

    public int TurnTimeoutSeconds { get; }

    /// <summary>Se un turno può scendere alla riserva del profilo. Spento finché la riserva non supera il gate (§7.1).</summary>
    public bool UseReserveModel { get; }

    private SessionAssistantSettings(
        bool enabled,
        IReadOnlyDictionary<string, string> systemPrompts,
        decimal temperature,
        int maxResponseTokens,
        int maxToolIterations,
        int turnTimeoutSeconds,
        bool useReserveModel)
    {
        Enabled = enabled;
        SystemPrompts = systemPrompts;
        Temperature = temperature;
        MaxResponseTokens = maxResponseTokens;
        MaxToolIterations = maxToolIterations;
        TurnTimeoutSeconds = turnTimeoutSeconds;
        UseReserveModel = useReserveModel;
    }

    public static SessionAssistantSettings Create(
        bool enabled,
        IReadOnlyDictionary<string, string> systemPrompts,
        decimal temperature = DefaultTemperature,
        int maxResponseTokens = DefaultMaxResponseTokens,
        int maxToolIterations = DefaultMaxToolIterations,
        int turnTimeoutSeconds = DefaultTurnTimeoutSeconds,
        bool useReserveModel = false)
    {
        ArgumentNullException.ThrowIfNull(systemPrompts);

        var prompts = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (lang, prompt) in systemPrompts)
        {
            Require(!string.IsNullOrWhiteSpace(lang), nameof(systemPrompts), "Prompt language is required.");
            Require(!string.IsNullOrWhiteSpace(prompt), nameof(systemPrompts), $"Prompt for '{lang}' is empty.");
            Require(prompts.TryAdd(Normalize(lang), prompt), nameof(systemPrompts), $"Prompt for '{lang}' is declared twice.");
        }

        Require(!enabled || prompts.Count > 0, nameof(systemPrompts),
            "An enabled session assistant needs at least one system prompt.");
        Require(temperature is >= 0m and <= 2m, nameof(temperature), "Temperature must be between 0 and 2.");
        Require(maxResponseTokens is >= 1 and <= AgentProfileContent.MaxResponseTokensLimit, nameof(maxResponseTokens),
            $"Max response tokens must be between 1 and {AgentProfileContent.MaxResponseTokensLimit}.");
        Require(maxToolIterations is >= 1 and <= MaxToolIterationsLimit, nameof(maxToolIterations),
            $"Max tool iterations must be between 1 and {MaxToolIterationsLimit}.");
        Require(turnTimeoutSeconds is >= MinTurnTimeoutSeconds and <= MaxTurnTimeoutSeconds, nameof(turnTimeoutSeconds),
            $"Turn timeout must be between {MinTurnTimeoutSeconds} and {MaxTurnTimeoutSeconds} seconds.");

        return new SessionAssistantSettings(
            enabled, prompts.AsReadOnly(), temperature, maxResponseTokens, maxToolIterations, turnTimeoutSeconds, useReserveModel);
    }

    /// <summary>
    /// Ricostruisce una sezione già persistita <b>senza rivalidarla</b>, come
    /// <see cref="AgentProfileContent.Restore"/>. Un campo assente vale il default misurato.
    /// </summary>
    internal static SessionAssistantSettings Restore(
        bool enabled,
        IReadOnlyDictionary<string, string>? systemPrompts,
        decimal? temperature,
        int? maxResponseTokens,
        int? maxToolIterations,
        int? turnTimeoutSeconds,
        bool useReserveModel) =>
        new(
            enabled,
            systemPrompts is null
                ? NoPrompts
                : new SortedDictionary<string, string>(systemPrompts.ToDictionary(StringComparer.Ordinal), StringComparer.Ordinal).AsReadOnly(),
            temperature ?? DefaultTemperature,
            maxResponseTokens ?? DefaultMaxResponseTokens,
            maxToolIterations ?? DefaultMaxToolIterations,
            turnTimeoutSeconds ?? DefaultTurnTimeoutSeconds,
            useReserveModel);

    /// <summary>
    /// Il prompt per una lingua: quello esatto, o quello della lingua senza la regione («en-US» → «en»).
    /// <c>null</c> se non c'è: chi chiama ricade sulla lingua di riserva del profilo.
    /// </summary>
    public string? PromptFor(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var key = Normalize(language);
        if (SystemPrompts.TryGetValue(key, out var exact))
        {
            return exact;
        }

        var separator = key.IndexOfAny(['-', '_']);
        return separator > 0 && SystemPrompts.TryGetValue(key[..separator], out var primary) ? primary : null;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Enabled;
        // Il conteggio tiene allineate le sequenze, come in AgentProfileContent.
        yield return SystemPrompts.Count;
        foreach (var (lang, prompt) in SystemPrompts)
        {
            yield return lang;
            yield return prompt;
        }

        yield return Temperature;
        yield return MaxResponseTokens;
        yield return MaxToolIterations;
        yield return TurnTimeoutSeconds;
        yield return UseReserveModel;
    }

    private static string Normalize(string language) => language.Trim().ToLowerInvariant();

    private static void Require(bool condition, string propertyName, string message)
    {
        if (!condition)
        {
            throw new ValidationException(propertyName, message);
        }
    }
}
```

- [ ] **Step 5: Porta la sezione nel contenuto e nel jsonb**

In `AgentProfileContent.cs`, cinque modifiche.

1. Dopo la proprietà `AllowedCategories`:

```csharp
    /// <summary>La sezione «Assistente in partita» (spec 2026-10-11 §3.2). Mai nulla: assente vale spenta.</summary>
    public SessionAssistantSettings SessionAssistant { get; }
```

2. Nel costruttore privato, un parametro in coda (`IReadOnlyList<DocumentCategory> allowedCategories, SessionAssistantSettings sessionAssistant)`) e l'assegnazione `SessionAssistant = sessionAssistant;` dopo `AllowedCategories = allowedCategories;`.

3. In `Create`, il parametro in coda `SessionAssistantSettings? sessionAssistant = null` dopo `allowedCategories`; subito prima del `return`:

```csharp
        var assistant = sessionAssistant ?? SessionAssistantSettings.Disabled;
        Require(!assistant.Enabled || assistant.PromptFor(language) is not null, nameof(sessionAssistant),
            $"The session assistant needs a system prompt for the fallback language '{language}'.");
```

e `assistant` come ultimo argomento del `new AgentProfileContent(...)`.

4. In `Restore`, il parametro in coda `SessionAssistantSettings? sessionAssistant = null` e, come ultimo argomento del `new(...)`, `sessionAssistant ?? SessionAssistantSettings.Disabled`.

5. In fondo a `GetEqualityComponents`:

```csharp
        yield return SessionAssistant;
```

In `AgentProfileContentJson.cs`:

1. In `Serialize`, dopo `content.AllowedCategories.ToArray()` aggiungi l'argomento `ToDocument(content.SessionAssistant)`.
2. In `Deserialize`, dopo `d.AllowedCategories` aggiungi l'argomento `ToSettings(d.SessionAssistant)`.
3. In coda al record `Document`, dopo `DocumentCategory[] AllowedCategories`:

```csharp
        DocumentCategory[] AllowedCategories,
        // Opzionale e in coda: le versioni scritte prima della fetta A2 non hanno la chiave, e una sezione
        // spenta non la scrive, così il jsonb delle versioni esistenti non cambia.
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SessionAssistantDocument? SessionAssistant = null);
```

4. Prima del record `Document`:

```csharp
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
```

In `AgentProfileFieldEffectTests.cs`, nell'helper `Modified`, aggiungi `c.SessionAssistant` come ultimo argomento di `AgentProfileContent.Create(...)`, dopo `c.AllowedCategories.ToList()`: una versione ripubblicata dal test non deve perdere la sezione.

- [ ] **Step 6: Verifica che i test passino**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SessionAssistantSettingsTests|FullyQualifiedName~AgentProfileContentSessionAssistantTests|FullyQualifiedName~AgentProfileContentJsonTests|FullyQualifiedName~AgentProfileTests|FullyQualifiedName~AgentProfileContentTests"`
Expected: PASS. Annota il numero. I test preesistenti di `AgentProfileContentJsonTests` e `AgentProfileTests` devono restare verdi: il contenuto senza sezione è uguale a prima.

- [ ] **Step 7: Verifica per perturbazione**

Copia `AgentProfileContent.cs` fuori dal repository, poi togli la riga `yield return SessionAssistant;` e riesegui `--filter "FullyQualifiedName~AgentProfileContentSessionAssistantTests"`.
Expected: `TwoContents_DifferingOnlyInTheAssistant_AreNotEqual` FAIL. Ripristina il file per copia, `touch`, e riesegui: PASS.

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase apps/api/tests/Api.Tests
git commit -m "feat(ai): sezione Assistente in partita nel profilo (#4275)"
```

### Task 4: `SupportsToolCalling` nel catalogo e la porta verso `KnowledgeBase`

**Files:**
- Modify: `API/BoundedContexts/SystemConfiguration/Domain/ValueObjects/ModelSettings.cs`
- Modify: `API/BoundedContexts/SystemConfiguration/Domain/Entities/AiModelConfiguration.cs` (`UpdateSettings`, `UpdatePricing`, `ApplyVerifiedPricing`, nuovo `DeclareToolCalling`)
- Modify: `API/BoundedContexts/SystemConfiguration/Application/Commands/SeedAiModelsCommandHandler.cs`
- Modify: `API/BoundedContexts/SystemConfiguration/Application/Commands/CreateAiModelCommandHandler.cs`
- Create: `KB/Domain/Services/LlmManagement/IToolCallingModelCatalog.cs`
- Create: `KB/Infrastructure/Services/CatalogToolCallingModels.cs`
- Modify: `KB/Infrastructure/DependencyInjection/SessionAssistantServiceExtensions.cs`
- Test: `TESTS/BoundedContexts/SystemConfiguration/Domain/Entities/AiModelConfigurationToolCallingTests.cs`
- Test: `TESTS/BoundedContexts/SystemConfiguration/Application/Commands/SeedAiModelsCommandHandlerTests.cs` (tre test in più, due adeguati)
- Test: `TESTS/BoundedContexts/SystemConfiguration/Application/Handlers/AiModelCatalogAdminCommandsTests.cs` (un test in più)
- Test: `TESTS/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/SessionAssistantRegistrationTests.cs` (la porta nuova)
- Test: `TESTS/BoundedContexts/KnowledgeBase/Infrastructure/Services/CatalogToolCallingModelsTests.cs`
- Test: `TESTS/Integration/SystemConfiguration/SeedAiModelsIntegrationTests.cs` (un test in più)

**Interfaces:**
- Consumes: `ModelSettings(int maxTokens, decimal temperature, ModelPricing pricing)`, `AiModelConfiguration`, `IAiModelConfigurationRepository.GetAllAsync`, `SeedAiModelsCommandHandler.SeedModel`.
- Produces:
  - `ModelSettings.SupportsToolCalling` (`bool`, `init`, default `false`; nel jsonb `"SupportsToolCalling"`).
  - `bool AiModelConfiguration.DeclareToolCalling(bool supported)` (vero se è cambiato).
  - `SeedModel(..., bool IsActive, bool IsPrimary, bool SupportsToolCalling = false)`; nel seed solo `deepseek-flash` lo dichiara.
  - `internal interface IToolCallingModelCatalog { Task<IReadOnlySet<string>> GetToolCallingModelIdsAsync(CancellationToken cancellationToken); }` nel namespace `Api.BoundedContexts.KnowledgeBase.Domain.Services`, registrata `Scoped` da `AddSessionAssistant()`.

Nessuna migrazione: `settings_json` è jsonb e una chiave assente vale `false` (spec §3.2). Sui database esistenti la capacità arriva dal seed, che gira a ogni avvio (`CoreSeedLayer`).

- [ ] **Step 1: Scrivi i test del dominio del catalogo**

`TESTS/BoundedContexts/SystemConfiguration/Domain/Entities/AiModelConfigurationToolCallingTests.cs`:

```csharp
using System.Text.Json;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Spec 2026-10-11 §3.2: il catalogo dice quali modelli supportano il tool calling. Review Focus 1: i
/// tre metodi che ricostruiscono <see cref="ModelSettings"/> non devono azzerare la capacità, o un
/// aggiornamento del prezzo spegnerebbe l'assistente.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "4275")]
public sealed class AiModelConfigurationToolCallingTests
{
    private static AiModelConfiguration FlashWithTools()
    {
        var model = AiModelConfiguration.Create(
            "deepseek-flash", "DeepSeek Flash", "DeepSeek", priority: 8,
            new ModelSettings(4096, 0.7m, new ModelPricing(0.30m, 1.20m)), isActive: false);
        model.DeclareToolCalling(true);
        return model;
    }

    [Fact]
    public void SettingsJson_WrittenBeforeThisChange_HasNoToolCalling()
    {
        const string legacy =
            """{"MaxTokens":4096,"Temperature":0.7,"Pricing":{"InputPricePerMillion":0.30,"OutputPricePerMillion":1.20,"Currency":"USD"}}""";

        JsonSerializer.Deserialize<ModelSettings>(legacy)!.SupportsToolCalling.Should().BeFalse();
    }

    [Fact]
    public void SettingsJson_RoundTripsTheCapability()
    {
        var settings = new ModelSettings(4096, 0.7m, new ModelPricing(0.30m, 1.20m)) { SupportsToolCalling = true };

        var json = JsonSerializer.Serialize(settings);

        json.Should().Contain("\"SupportsToolCalling\":true");
        JsonSerializer.Deserialize<ModelSettings>(json)!.SupportsToolCalling.Should().BeTrue();
    }

    [Fact]
    public void DeclareToolCalling_ChangesTheCapability_AndSaysWhetherItChanged()
    {
        var model = AiModelConfiguration.Create(
            "deepseek-flash", "DeepSeek Flash", "DeepSeek", priority: 8,
            new ModelSettings(4096, 0.7m, new ModelPricing(0.30m, 1.20m)));

        model.DeclareToolCalling(true).Should().BeTrue();
        model.Settings.SupportsToolCalling.Should().BeTrue();
        model.UpdatedAt.Should().NotBeNull();
        model.DeclareToolCalling(true).Should().BeFalse("era già dichiarata");
    }

    [Fact]
    public void UpdatePricing_KeepsTheToolCallingCapability()
    {
        var model = FlashWithTools();

        model.UpdatePricing(0.35m, 1.30m);

        model.Settings.SupportsToolCalling.Should().BeTrue();
        model.Settings.Pricing.InputPricePerMillion.Should().Be(0.35m);
    }

    [Fact]
    public void ApplyVerifiedPricing_KeepsTheToolCallingCapability()
    {
        var model = FlashWithTools();
        var verified = new ModelPricing(0.28m, 1.10m, verifiedOn: new DateOnly(2026, 10, 12), source: "https://example.test/pricing");

        model.ApplyVerifiedPricing(verified).Should().BeTrue();

        model.Settings.SupportsToolCalling.Should().BeTrue();
        model.Settings.Pricing.InputPricePerMillion.Should().Be(0.28m);
    }

    [Fact]
    public void UpdateSettings_KeepsTheToolCallingCapability()
    {
        var model = FlashWithTools();

        model.UpdateSettings(2048, 0.5m);

        model.Settings.SupportsToolCalling.Should().BeTrue();
        model.Settings.MaxTokens.Should().Be(2048);
    }
}
```

- [ ] **Step 2: Scrivi i test del seed e della porta**

In `SeedAiModelsCommandHandlerTests.cs` due test esistenti costruiscono un catalogo «già aggiornato» con `Row(...)`, che non porta la capacità: con il seed nuovo quel catalogo non sarebbe più aggiornato, e i due test diventerebbero rossi. Aggiungi l'helper sotto `Row`:

```csharp
    /// <summary>Il catalogo come lo lascia il seed: prezzi verificati e capacità dichiarate.</summary>
    private static AiModelConfiguration[] UpToDateCatalog() =>
        SeedAiModelsCommandHandler.Models.Select(m =>
        {
            var row = Row(m.ModelId, m.Provider, m.Priority, m.Pricing, m.IsActive, m.IsPrimary);
            row.DeclareToolCalling(m.SupportsToolCalling);
            return row;
        }).ToArray();
```

e usalo nei due test: in `UpToDateCatalog_SavesNothing` sostituisci l'espressione assegnata a `upToDate` con `UpToDateCatalog()`; in `RetiredModels_AlreadyInactive_AreLeftAlone` sostituisci `SeedAiModelsCommandHandler.Models.Select(m => Row(m.ModelId, m.Provider, m.Priority, m.Pricing, m.IsActive, m.IsPrimary))` con `UpToDateCatalog()`, lasciando `.Append(...)` e `.ToArray()`.

Poi, in fondo alla classe:

```csharp
    /// <summary>
    /// Spec 2026-10-11 §7.1: il catalogo dichiara il tool calling solo per il modello che ha superato lo
    /// spike. Un modello non misurato non riceve l'assistente per omissione.
    /// </summary>
    [Fact]
    public async Task EmptyCatalog_DeclaresToolCallingOnlyForTheMeasuredModel()
    {
        await SeedAsync();

        _added.Where(m => m.Settings.SupportsToolCalling).Select(m => m.ModelId).Should().Equal("deepseek-flash");
    }

    [Fact]
    public async Task ExistingModelWithoutTheCapability_GetsIt()
    {
        var flash = Row("deepseek-flash", "DeepSeek", 8, new ModelPricing(0.30m, 1.20m), isActive: false);

        await SeedAsync(flash);

        _updated.Should().ContainSingle(m => m.ModelId == "deepseek-flash")
            .Which.Settings.SupportsToolCalling.Should().BeTrue();
        flash.IsActive.Should().BeFalse("l'abilitazione resta una scelta dell'admin");
    }

    [Fact]
    public async Task SeedingACatalogThatAlreadyDeclaresIt_WritesNothing()
    {
        await SeedAsync();
        var catalog = _added.ToArray();
        _unitOfWork.Invocations.Clear();

        await SeedAsync(catalog);

        _updated.Should().BeEmpty();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
```

`TESTS/BoundedContexts/KnowledgeBase/Infrastructure/Services/CatalogToolCallingModelsTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>Spec 2026-10-11 §3.2: la porta con cui KnowledgeBase legge dal catalogo i modelli con tool calling.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4275")]
public sealed class CatalogToolCallingModelsTests
{
    private static AiModelConfiguration Model(string id, bool tools, bool isActive)
    {
        var model = AiModelConfiguration.Create(
            id, id, "DeepSeek", priority: 1, new ModelSettings(4096, 0.7m, ModelPricing.Free), isActive: isActive);
        model.DeclareToolCalling(tools);
        return model;
    }

    [Fact]
    public async Task ReturnsTheModelsThatDeclareToolCalling_EvenWhenInactive()
    {
        // deepseek-flash è disattivato nel catalogo ed è il modello principale del profilo: la capacità è
        // una proprietà del modello, non della sua abilitazione (come il prezzo, #4164).
        var repository = new Mock<IAiModelConfigurationRepository>();
        repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            Model("deepseek-flash", tools: true, isActive: false),
            Model("deepseek-chat", tools: false, isActive: true),
        ]);

        var models = await new CatalogToolCallingModels(repository.Object)
            .GetToolCallingModelIdsAsync(TestContext.Current.CancellationToken);

        models.Should().BeEquivalentTo(["deepseek-flash"]);
        models.Contains("DEEPSEEK-FLASH").Should().BeFalse("gli id dei modelli si confrontano in modo esatto");
    }
}
```

In `AiModelCatalogAdminCommandsTests.cs`, in fondo alla classe:

```csharp
    /// <summary>
    /// #4275: la capacità di tool calling la dichiara solo il seed, per i modelli misurati. Il corpo della
    /// richiesta di creazione è lo stesso tipo che finisce nel jsonb, quindi un client potrebbe mandarla a
    /// «vero»: la creazione la ignora, altrimenti basterebbe una POST per far passare l'invariante di
    /// pubblicazione dell'assistente su un modello mai misurato.
    /// </summary>
    [Fact]
    public async Task ANewModel_NeverDeclaresToolCalling_WhateverTheRequestSays()
    {
        AiModelConfiguration? created = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<AiModelConfiguration>(), It.IsAny<CancellationToken>()))
            .Callback<AiModelConfiguration, CancellationToken>((model, _) => created = model)
            .Returns(Task.CompletedTask);
        var settings = new ModelSettings(4096, 0.3m, new ModelPricing(0.3m, 1.2m)) { SupportsToolCalling = true };

        var dto = await new CreateAiModelCommandHandler(_repository.Object, _unitOfWork.Object)
            .Handle(new CreateAiModelCommand("deepseek-reasoner", "DeepSeek Reasoner", "DeepSeek", 9, false, false, settings), Ct);

        created!.Settings.SupportsToolCalling.Should().BeFalse();
        dto.Settings.SupportsToolCalling.Should().BeFalse();
    }
```

In `SessionAssistantRegistrationTests.cs` il contenitore di prova ha solo il logging e due doppi, e `ValidateOnBuild` fallisce su ogni dipendenza che non trova. Dopo la riga che registra `IPlayerNameResolutionService` aggiungi:

```csharp
        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.SystemConfiguration.Domain.Repositories.IAiModelConfigurationRepository>());
```

e, in fondo al test:

```csharp
        scope.ServiceProvider.GetRequiredService<Api.BoundedContexts.KnowledgeBase.Domain.Services.IToolCallingModelCatalog>()
            .Should().NotBeNull();
```

In `SeedAiModelsIntegrationTests.cs` aggiungi `using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;` e, in fondo alla classe:

```csharp
    /// <summary>
    /// #4275: su un catalogo che ha già <c>deepseek-flash</c> senza la capacità (ogni database esistente),
    /// il seed la dichiara, e la porta di KnowledgeBase la legge dal jsonb vero.
    /// </summary>
    [Fact]
    public async Task AFlashRowWrittenBeforeTheCapability_GetsIt_AndThePortSeesIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var context = _fixture.CreateDbContext(_connectionString))
        {
            var repository = new EfAiModelConfigurationRepository(context, new DomainEventCollector());
            await repository.AddRangeAsync(
                [Row("deepseek-flash", "DeepSeek", 8, new ModelPricing(0.30m, 1.20m), isActive: false)], ct);
            await context.SaveChangesAsync(ct);
        }

        await SeedAsync(ct);

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var port = new CatalogToolCallingModels(new EfAiModelConfigurationRepository(verification, new DomainEventCollector()));
        (await port.GetToolCallingModelIdsAsync(ct)).Should().BeEquivalentTo(["deepseek-flash"]);
    }
```

- [ ] **Step 3: Verifica che non compili**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AiModelConfigurationToolCallingTests|FullyQualifiedName~CatalogToolCallingModelsTests|FullyQualifiedName~SeedAiModelsCommandHandlerTests"`
Expected: errori `CS0117`/`CS1061` su `SupportsToolCalling` e `DeclareToolCalling`, `CS0246` su `CatalogToolCallingModels` e `IToolCallingModelCatalog`.

- [ ] **Step 4: Implementa**

In `ModelSettings.cs`, dopo la proprietà `Pricing`:

```csharp
    /// <summary>
    /// Spec 2026-10-11 §3.2: il modello accetta definizioni di strumenti e restituisce chiamate. Una riga
    /// scritta prima di questa proprietà non ha la chiave e vale <c>false</c>. Si dichiara solo per i
    /// modelli misurati sul set di scenari dell'assistente (§7.1).
    /// </summary>
    public bool SupportsToolCalling { get; init; }
```

In `AiModelConfiguration.cs`:

```csharp
    public void UpdateSettings(int maxTokens, decimal temperature)
    {
        // Il costruttore valida i due valori; il resto delle impostazioni si porta dietro a mano, perché
        // sta fuori dal costruttore (#4275: senza, ogni modifica azzererebbe SupportsToolCalling).
        Settings = new ModelSettings(maxTokens, temperature, Settings.Pricing)
        {
            SupportsToolCalling = Settings.SupportsToolCalling,
        };
        UpdatedAt = DateTime.UtcNow;
    }
```

In `UpdatePricing` sostituisci `Settings = new ModelSettings(Settings.MaxTokens, Settings.Temperature, newPricing);` con:

```csharp
        // `with` e non il costruttore: copia tutte le impostazioni, anche quelle aggiunte dopo (#4275).
        Settings = Settings with { Pricing = newPricing };
```

In `ApplyVerifiedPricing` sostituisci `Settings = new ModelSettings(Settings.MaxTokens, Settings.Temperature, verified);` con:

```csharp
        Settings = Settings with { Pricing = verified };
```

Dopo `ApplyVerifiedPricing`:

```csharp
    /// <summary>
    /// #4275: dichiara se il modello supporta il tool calling. È un fatto del modello, misurato, non una
    /// scelta di instradamento: abilitazione e priorità non cambiano.
    /// </summary>
    /// <returns>Vero se la capacità è cambiata.</returns>
    public bool DeclareToolCalling(bool supported)
    {
        if (Settings.SupportsToolCalling == supported)
        {
            return false;
        }

        Settings = Settings with { SupportsToolCalling = supported };
        UpdatedAt = DateTime.UtcNow;
        return true;
    }
```

In `SeedAiModelsCommandHandler.cs`:

1. In `SeedModel`, un parametro in coda: `bool IsPrimary, bool SupportsToolCalling = false);`.
2. Nella voce `deepseek-flash` di `Models`, dopo `IsActive: false, IsPrimary: false` aggiungi `, SupportsToolCalling: true`, e sopra la voce una riga di commento: `// #4275: è il solo modello misurato sul set di scenari dell'assistente (spec 2026-10-11 §7.1).`
3. Nel ramo che inserisce un modello nuovo, `settings: new ModelSettings(seed.MaxTokens, temperature: 0.7m, seed.Pricing) { SupportsToolCalling = seed.SupportsToolCalling },`.
4. Nel ciclo, dopo il blocco `if (seed.Pricing.VerifiedOn is not null && model.ApplyVerifiedPricing(seed.Pricing))`:

```csharp
            // #4275: la capacità misurata vale anche per una riga già presente. Il seed la dichiara e non
            // la toglie mai: se un giorno un modello la perde, lo dice chi l'ha misurato.
            if (seed.SupportsToolCalling && model.DeclareToolCalling(true))
            {
                changed.Add(model);
                declared++;
            }
```

5. Dichiara `var declared = 0;` accanto a `var repriced = 0;`, aggiungi `&& declared == 0` alla condizione di uscita anticipata, e `{Declared} tool-calling capabilities declared` con `declared` al messaggio di log finale.
6. Nel commento XML della classe, un terzo punto elenco: `<item><description>se il seed dichiara il tool calling (#4275), lo dichiara anche sulla riga già presente.</description></item>`.

`KB/Domain/Services/LlmManagement/IToolCallingModelCatalog.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Spec 2026-10-11 §3.2: i modelli del catalogo che supportano il tool calling. KnowledgeBase lo chiede
/// per due motivi: rifiutare la pubblicazione di un profilo con l'assistente su un modello senza
/// strumenti, e ricontrollarlo a ogni turno (una migrazione di dati scavalca l'aggregato).
/// </summary>
internal interface IToolCallingModelCatalog
{
    /// <summary>Gli id dei modelli con tool calling, attivi o no: la capacità è del modello, non della sua abilitazione.</summary>
    Task<IReadOnlySet<string>> GetToolCallingModelIdsAsync(CancellationToken cancellationToken);
}
```

`KB/Infrastructure/Services/CatalogToolCallingModels.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// <see cref="IToolCallingModelCatalog"/> letto dal catalogo <c>AiModelConfiguration</c>, come
/// <see cref="CatalogModelPriceList"/> legge i prezzi. Senza istantanea: è una lettura per turno, su una
/// tabella di poche righe, e la risposta deve seguire subito una modifica del catalogo.
/// </summary>
internal sealed class CatalogToolCallingModels(IAiModelConfigurationRepository repository) : IToolCallingModelCatalog
{
    public async Task<IReadOnlySet<string>> GetToolCallingModelIdsAsync(CancellationToken cancellationToken)
    {
        var models = await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return models
            .Where(m => m.Settings.SupportsToolCalling)
            .Select(m => m.ModelId)
            .ToHashSet(StringComparer.Ordinal);
    }
}
```

In `CreateAiModelCommandHandler.cs`, nella chiamata ad `AiModelConfiguration.Create`, la riga `settings: command.Settings,` diventa:

```csharp
            // #4275: la capacità di tool calling la dichiara il seed, per i modelli misurati; non una richiesta.
            settings: command.Settings with { SupportsToolCalling = false },
```

In `SessionAssistantServiceExtensions.cs`, con i suoi `using` (`Api.BoundedContexts.KnowledgeBase.Domain.Services`, `Api.BoundedContexts.KnowledgeBase.Infrastructure.Services`):

```csharp
        services.AddScoped<IToolCallingModelCatalog, CatalogToolCallingModels>();
```

- [ ] **Step 5: Verifica che i test passino**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AiModelConfigurationToolCallingTests|FullyQualifiedName~CatalogToolCallingModelsTests|FullyQualifiedName~SeedAiModelsCommandHandlerTests|FullyQualifiedName~AiModelConfigurationVerifiedPricingTests|FullyQualifiedName~ModelSettingsTests|FullyQualifiedName~AiModelConfigurationTests|FullyQualifiedName~AiModelCatalogAdminCommandsTests|FullyQualifiedName~SessionAssistantRegistrationTests"`
Expected: PASS. Annota il numero.

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SeedAiModelsIntegrationTests"`
Expected: PASS, compreso `SeedingTwice_ChangesNothingTheSecondTime` (il secondo seed non deve riscrivere la capacità).

- [ ] **Step 6: Verifica per perturbazione (Review Focus 1)**

Copia `AiModelConfiguration.cs` fuori dal repository. In `UpdatePricing` rimetti `Settings = new ModelSettings(Settings.MaxTokens, Settings.Temperature, newPricing);` e riesegui `--filter "FullyQualifiedName~AiModelConfigurationToolCallingTests"`.
Expected: `UpdatePricing_KeepsTheToolCallingCapability` FAIL. Ripristina per copia, `touch`, riesegui: PASS.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api apps/api/tests/Api.Tests
git commit -m "feat(ai): tool calling nel catalogo modelli e porta verso KB (#4275)"
```

### Task 5: L'invariante di pubblicazione

**Files:**
- Modify: `KB/Domain/ValueObjects/AgentProfileContent.cs` (nuovo metodo `SessionAssistantModelProblem`)
- Modify: `KB/Domain/Entities/AgentProfile.cs` (`PublishDraft`, `RollbackTo`, costante)
- Modify: `TESTS/BoundedContexts/KnowledgeBase/Domain/Entities/AgentProfileTests.cs`
- Modify: `TESTS/Integration/KnowledgeBase/AgentProfileFieldEffectTests.cs` (una chiamata)
- Modify: `TESTS/Integration/KnowledgeBase/AgentProfilePersistenceIntegrationTests.cs` (due chiamate)
- Modify: `docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md` (scostamenti S3, S4, S5)

**Interfaces:**
- Consumes: `AgentProfileContent.SessionAssistant` (Task 3); `IReadOnlySet<string>` dei modelli con tool calling (Task 4, costruito con `StringComparer.Ordinal`).
- Produces:
  - `string? AgentProfileContent.SessionAssistantModelProblem(IReadOnlySet<string> toolCallingModels)`: `null` se l'assistente può girare con i modelli del contenuto, altrimenti il motivo.
  - `AgentProfile.PublishDraft(IReadOnlySet<string> toolCallingModels)` e `AgentProfile.RollbackTo(int versionNumber, IReadOnlySet<string> toolCallingModels)`: lanciano `ConflictException` con `ErrorCode == AgentProfile.AssistantModelWithoutToolsCode` (`"assistant_model_without_tools"`).
  - Nei test: `AgentProfileTests.NoToolCallingModels` e `AgentProfileTests.FlashHasTools`.

`PublishDraft` non ha chiamanti in produzione (la pagina admin è la fetta 6c di ADR-095): cambiano solo i test.

- [ ] **Step 1: Scrivi i test che falliscono**

In `AgentProfileTests.cs`, sotto l'helper `Assistant`:

```csharp
    internal static readonly IReadOnlySet<string> NoToolCallingModels = new HashSet<string>(StringComparer.Ordinal);

    internal static readonly IReadOnlySet<string> FlashHasTools =
        new HashSet<string>(StringComparer.Ordinal) { "deepseek-flash" };
```

Nello stesso file sostituisci ogni `profile.PublishDraft()` con `profile.PublishDraft(NoToolCallingModels)` e ogni `profile.RollbackTo(<n>)` con `profile.RollbackTo(<n>, NoToolCallingModels)`: i contenuti di quei test non hanno l'assistente, quindi l'insieme non conta. Poi, in fondo alla classe:

```csharp
    // ─── Spec 2026-10-11 §3.2: l'assistente in partita vuole un modello con tool calling ───

    [Fact]
    public void PublishDraft_WithTheAssistantOnAModelWithoutToolCalling_IsRejected_AndNothingChanges()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(model: "deepseek-flash", assistant: Assistant()));

        var act = () => profile.PublishDraft(NoToolCallingModels);

        act.Should().Throw<ConflictException>().Which.ErrorCode.Should().Be(AgentProfile.AssistantModelWithoutToolsCode);
        profile.Published.VersionNumber.Should().Be(1);
        profile.Draft.Should().NotBeNull("la bozza resta, da correggere");
    }

    [Fact]
    public void PublishDraft_WithTheAssistantOnAToolCallingModel_Publishes()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(model: "deepseek-flash", assistant: Assistant()));

        profile.PublishDraft(FlashHasTools);

        profile.Published.Content.SessionAssistant.Enabled.Should().BeTrue();
    }

    [Fact]
    public void PublishDraft_WithTheAssistantUsingTheReserve_NeedsTheReserveToSupportToolsToo()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(model: "deepseek-flash", assistant: Assistant(useReserveModel: true)));

        var act = () => profile.PublishDraft(FlashHasTools);

        act.Should().Throw<ConflictException>().Which.ErrorCode.Should().Be(AgentProfile.AssistantModelWithoutToolsCode);

        var both = new HashSet<string>(StringComparer.Ordinal) { "deepseek-flash", ReserveModel };
        profile.PublishDraft(both);
        profile.Published.Content.SessionAssistant.UseReserveModel.Should().BeTrue();
    }

    [Fact]
    public void PublishDraft_WithTheAssistantUsingAReserveTheProfileDoesNotHave_IsRejected()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(model: "deepseek-flash", assistant: Assistant(useReserveModel: true), fallbackModel: null));

        var act = () => profile.PublishDraft(FlashHasTools);

        act.Should().Throw<ConflictException>().Which.ErrorCode.Should().Be(AgentProfile.AssistantModelWithoutToolsCode);
    }

    [Fact]
    public void PublishDraft_WithTheAssistantDisabled_IgnoresTheCatalog()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(model: "un-modello-qualunque", assistant: Assistant(enabled: false)));

        profile.PublishDraft(NoToolCallingModels);

        profile.Published.Content.PrimaryModelId.Should().Be("un-modello-qualunque");
    }

    [Fact]
    public void RollbackTo_AVersionWhoseModelLostToolCalling_IsRejected()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content(model: "deepseek-flash", assistant: Assistant()));
        profile.CreateDraft(Content());
        profile.PublishDraft(NoToolCallingModels);

        var act = () => profile.RollbackTo(1, NoToolCallingModels);

        act.Should().Throw<ConflictException>().Which.ErrorCode.Should().Be(AgentProfile.AssistantModelWithoutToolsCode);
        profile.Published.VersionNumber.Should().Be(2);

        profile.RollbackTo(1, FlashHasTools);
        profile.Published.VersionNumber.Should().Be(3);
    }
```

In `AgentProfileFieldEffectTests.cs` (helper `PublishAsync`) e in `AgentProfilePersistenceIntegrationTests.cs` (due punti) sostituisci `PublishDraft()` con `PublishDraft(new HashSet<string>(StringComparer.Ordinal))`.

- [ ] **Step 2: Verifica che non compili**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AgentProfileTests"`
Expected: `CS1501` (nessun overload di `PublishDraft` con un argomento) e `CS0117` su `AssistantModelWithoutToolsCode`.

- [ ] **Step 3: Implementa**

In `AgentProfileContent.cs`, dopo `Restore`:

```csharp
    /// <summary>
    /// Spec 2026-10-11 §3.2: perché l'assistente in partita non può girare con i modelli di questo
    /// contenuto, o <c>null</c> se può (o se è spento). La regola è una sola e ha due chiamanti: la
    /// pubblicazione del profilo e il turno, che la ricontrolla perché una migrazione di dati scavalca
    /// l'aggregato.
    /// </summary>
    /// <param name="toolCallingModels">Gli id dei modelli del catalogo con tool calling, confronto esatto.</param>
    public string? SessionAssistantModelProblem(IReadOnlySet<string> toolCallingModels)
    {
        ArgumentNullException.ThrowIfNull(toolCallingModels);
        if (!SessionAssistant.Enabled)
        {
            return null;
        }

        if (!toolCallingModels.Contains(PrimaryModelId))
        {
            return $"The session assistant is enabled, but the primary model '{PrimaryModelId}' does not support tool calling.";
        }

        if (!SessionAssistant.UseReserveModel)
        {
            return null;
        }

        if (FallbackModelId is null)
        {
            return "The session assistant is set to use the reserve model, but the profile has none.";
        }

        return toolCallingModels.Contains(FallbackModelId)
            ? null
            : $"The session assistant is set to use the reserve model '{FallbackModelId}', which does not support tool calling.";
    }
```

In `AgentProfile.cs`:

```csharp
    /// <summary>Il codice del 409 quando l'assistente in partita è abilitato su un modello senza tool calling.</summary>
    public const string AssistantModelWithoutToolsCode = "assistant_model_without_tools";
```

```csharp
    /// <param name="toolCallingModels">
    /// Gli id dei modelli del catalogo con tool calling: l'aggregato non legge il catalogo, quindi chi
    /// pubblica glieli passa (spec 2026-10-11 §3.2).
    /// </param>
    public AgentProfileVersion PublishDraft(IReadOnlySet<string> toolCallingModels)
    {
        var draft = Draft ?? throw new ConflictException("Agent profile has no draft to publish.");
        EnsureTheAssistantCanRun(draft.Content, toolCallingModels);

        var now = Touch();
        Published.Archive(now);
        draft.Publish(now);
        return draft;
    }
```

In `RollbackTo` il commento e la firma diventano (il commento documenta **entrambi** i parametri: documentarne uno solo non compila, CS1573):

```csharp
    /// <summary>
    /// Ripubblica il contenuto di una versione archiviata come nuova versione. Un'eventuale bozza resta
    /// com'è: il rollback non cancella lavoro in corso.
    /// </summary>
    /// <param name="versionNumber">Il numero della versione archiviata da ripubblicare.</param>
    /// <param name="toolCallingModels">
    /// Gli id dei modelli del catalogo con tool calling: il catalogo può essere cambiato da quando quella
    /// versione era pubblicata.
    /// </param>
    public AgentProfileVersion RollbackTo(int versionNumber, IReadOnlySet<string> toolCallingModels)
```

e subito dopo il controllo `target.Status != AgentProfileVersionStatus.Archived`:

```csharp
        // Il catalogo può essere cambiato da quando quella versione era pubblicata.
        EnsureTheAssistantCanRun(target.Content, toolCallingModels);
```

In fondo alla classe:

```csharp
    private static void EnsureTheAssistantCanRun(AgentProfileContent content, IReadOnlySet<string> toolCallingModels)
    {
        if (content.SessionAssistantModelProblem(toolCallingModels) is { } problem)
        {
            throw new ConflictException(AssistantModelWithoutToolsCode, problem);
        }
    }
```

- [ ] **Step 4: Verifica che i test passino**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AgentProfileTests"`
Expected: PASS. Annota il numero.

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AgentProfilePersistenceIntegrationTests|FullyQualifiedName~AgentProfileFieldEffectTests"`
Expected: PASS (integrazione, in locale). Annota numero e tempo.

- [ ] **Step 5: Riporta gli scostamenti nella spec**

In `docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md`:

1. §3.2, riga «Profilo»: dopo «uso della riserva del profilo (spento finché la riserva non supera il gate, §7.1)» aggiungi «, temperatura e lunghezza massima della risposta (i valori misurati dallo spike, 0,2 e 600: quelli del resto del profilo sono pensati per le risposte sulle regole)». Nella stessa riga, dopo «assente vale disabilitato» aggiungi «; un assistente abilitato deve avere un prompt per la lingua di riserva del profilo», e sostituisci «`AgentProfileFieldEffectTests` riceve le sue righe campo → effetto» con «le righe campo → effetto della sezione stanno nei test del turno, perché l’effetto si osserva solo sul suo endpoint».
2. §3.2, riga «Catalogo»: sostituisci «esposto a `KnowledgeBase` da una porta» con «esposto a `KnowledgeBase` dalla porta `IToolCallingModelCatalog`. Lo dichiara il seed, solo per i modelli misurati (§7.1), anche sulle righe già presenti».
3. §3.2, riga «Pubblicazione»: sostituisci la prima frase con «`AgentProfile.PublishDraft` e `RollbackTo` ricevono l'insieme dei modelli con tool calling e rifiutano (`409 assistant_model_without_tools`) un contenuto con l'assistente abilitato se il modello principale del profilo non è nell'insieme, o se l'assistente usa la riserva e la riserva non c'è o non è nell'insieme. Il turno ricontrolla la stessa regola a ogni richiesta, perché la migrazione di dati qui sotto scavalca l'aggregato.»
4. §8, riga A2: sostituisci «migrazione di dati che pubblica la versione con l'assistente abilitato» con «la migrazione di dati che pubblica la versione con l'assistente abilitato è un passo a sé, dopo #4270».

- [ ] **Step 6: Suite unit della fetta, build Release, commit**

```bash
dotnet build apps/api/src/Api/Api.csproj -c Release
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "(BoundedContext=KnowledgeBase|BoundedContext=Architecture|BoundedContext=SystemConfiguration)&Category=Unit" > "$TEMP/a2-unit.log" 2>&1; tail -5 "$TEMP/a2-unit.log"
```

Expected: build con 0 avvisi e 0 errori; nella suite falliscono solo i cinque test noti dei Global Constraints (confronta i nomi con `grep -E "^\s+Failed " "$TEMP/a2-unit.log"`).

```bash
git add apps/api docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md
git commit -m "feat(ai): la pubblicazione rifiuta l'assistente senza tool calling (#4275)"
```

### Chiusura della fetta A2

- [ ] **Review dell'intero branch** con un revisore nuovo sul modello più capace (`superpowers:requesting-code-review`), a cui passare questo piano, la spec, il Review Focus e le righe `Ruling:` del ledger. Correzioni Critical e Important con un test rosso prima.
- [ ] **PR verso `main-dev`** (`gh pr create --base main-dev`), titolo `feat(ai): proposte, sezione del profilo e tool calling nel catalogo (#4275)`. Nel corpo: cosa c'è, cosa **non** c'è (l'assistente resta spento: nessuna migrazione lo abilita, #4270), e i test di integrazione eseguiti in locale con il loro numero.
- [ ] **CI verde**, squash merge, branch e worktree rimossi.
- [ ] **Issue #4275**: DoD spuntata e chiusa; epic #4238 aggiornato.

---

# Fetta A3a — #4276

Worktree `D:/Repositories/wt-assistant-a3a`, branch `feature/issue-4276-assistant-turn` da `origin/main-dev` **dopo il merge di A2**, `git config branch.feature/issue-4276-assistant-turn.parent main-dev`.

### Task 6: Round a base 1 e lettore vero della sessione

**Files:**
- Modify: `KB/Application/SessionAssistant/AssistantRound.cs`
- Modify: `TESTS/BoundedContexts/KnowledgeBase/Application/SessionAssistant/SessionAssistantToolsetTests.cs` (dati della theory del round)
- Modify: `TESTS/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Evaluation/SessionAssistantEvaluatorTests.cs` (un test in più)
- Modify: `tests/llm-eval/session-assistant/scenarios.json` (indici di turno, versione 4)
- Create: `KB/Application/SessionAssistant/IAssistantSessionDirectory.cs`
- Create: `KB/Infrastructure/SessionAssistant/LiveSessionAssistantDirectory.cs`
- Modify: `KB/Infrastructure/DependencyInjection/SessionAssistantServiceExtensions.cs`
- Test: `TESTS/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/SessionAssistantRegistrationTests.cs` (la porta nuova)
- Test: `TESTS/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/LiveSessionAssistantReaderTests.cs`
- Create: `TESTS/Integration/KnowledgeBase/SessionAssistant/SessionAssistantTestHost.cs` (fixture e helper condivisi con i Task 10 e 13)
- Test: `TESTS/Integration/KnowledgeBase/SessionAssistant/LiveSessionAssistantDirectoryIntegrationTests.cs`
- Modify: `docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md` (§5.1, scostamento S1)

**Interfaces:**
- Consumes: `GetLiveSessionQuery(Guid) : IQuery<LiveSessionDto>`, `GetTurnPhasesQuery(Guid) : IQuery<TurnPhasesDto>` (`Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions`); `GetGameMemoryQuery(Guid GameId, Guid OwnerId) : IQuery<GameMemoryDto?>` (`Api.BoundedContexts.AgentMemory.Application.Queries`); `AssistantSessionState`, `ISessionAssistantReader` (A1).
- Produces:

```csharp
internal sealed record AssistantSessionAccess(Guid OwnerUserId, string Status, bool AcceptsQuestions);

internal interface IAssistantSessionDirectory
{
    Task<AssistantSessionAccess> GetAccessAsync(Guid liveSessionId, CancellationToken cancellationToken);   // NotFoundException se non esiste
    ISessionAssistantReader CreateReader(Guid liveSessionId, Guid ownerUserId);
}
```

  - `AssistantRound.Current(int currentTurnIndex, int playersInTurnOrder)`: indice a base 1.
  - Per i test di integrazione: `SessionAssistantHostFixture` (con `Llm` e `Hub`), `SessionAssistantTestData.SeedHostAsync`, `CreateTableAsync`, `SendAsync`, `EnableAssistantAsync`, `AskAsync`, `LiveTable(Guid SessionId, IReadOnlyDictionary<string, Guid> Players)`.

- [ ] **Step 1: Porta il test del round sull'indice a base 1 e aggiungi il test di coerenza degli scenari**

In `SessionAssistantToolsetTests.cs` sostituisci i dati della theory `Round_IsAFullTurnOfTheTable`:

```csharp
    /// <summary>
    /// L'indice di turno del dominio vale 0 prima dell'avvio e parte da 1 con <c>Start()</c>
    /// (<c>LiveGameSession.GetCurrentTurnPlayerId</c> usa <c>(indice - 1) % n</c>). Con tre giocatori i
    /// turni 1, 2 e 3 sono il primo giro: con la formula a base 0 l'ultimo giocatore cadeva nel round 2.
    /// </summary>
    [Theory]
    [InlineData(0, 3, 1)]
    [InlineData(1, 3, 1)]
    [InlineData(3, 3, 1)]
    [InlineData(4, 3, 2)]
    [InlineData(6, 3, 2)]
    [InlineData(7, 3, 3)]
    [InlineData(4, 0, 1)]
    [InlineData(-2, 3, 1)]
    public void Round_IsAFullTurnOfTheTable(int turnIndex, int activePlayers, int expected) =>
        AssistantRound.Current(turnIndex, activePlayers).Should().Be(expected);
```

In `SessionAssistantEvaluatorTests.cs`, sotto `ScenarioSet_InTheRepository_IsValidJson`:

```csharp
    /// <summary>
    /// Uno scenario deve descrivere uno stato che la sessione vera può produrre: con l'indice di turno a
    /// base 1 del dominio, il giocatore di turno è <c>players[(indice - 1) % n]</c>. Dalla versione 4 del
    /// set è così; prima gli indici erano scritti a base 0.
    /// </summary>
    [Fact]
    public void ScenarioSet_TurnIndexAndCurrentPlayer_AgreeWithTheDomain()
    {
        var path = Path.Combine(FindRepoRoot(), "tests", "llm-eval", "session-assistant", "scenarios.json");
        var set = JsonSerializer.Deserialize<AssistantScenarioSet>(File.ReadAllText(path), AssistantScenarioSet.Json)!;

        set.Version.Should().BeGreaterThanOrEqualTo(4);
        foreach (var scenario in set.Scenarios.Where(s => s.State.CurrentPlayer is not null))
        {
            var players = scenario.State.Players;
            var index = scenario.State.CurrentTurnIndex;

            index.Should().BeGreaterThan(0, $"{scenario.Id}: una partita con un giocatore di turno è avviata");
            players[(index - 1) % players.Count].Should().Be(scenario.State.CurrentPlayer,
                $"{scenario.Id}: all'indice {index} il dominio dà il turno a un altro giocatore");
        }
    }
```

- [ ] **Step 2: Verifica che falliscano**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Round_IsAFullTurnOfTheTable|FullyQualifiedName~ScenarioSet_TurnIndexAndCurrentPlayer"`
Expected: FAIL. Le righe `(3, 3, 1)` e `(6, 3, 2)` ricevono 2 e 3 dalla formula a base 0; le altre passano già. Il test degli scenari fallisce sulla versione del set (3).

- [ ] **Step 3: Correggi la formula**

`KB/Application/SessionAssistant/AssistantRound.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Il round derivato (spec §5.1): un giro completo del tavolo. <c>LiveGameSession</c> ha solo l'indice
/// di turno, che vale 0 prima dell'avvio, 1 con <c>Start()</c>, e cresce a ogni turno di ogni giocatore.
/// </summary>
internal static class AssistantRound
{
    /// <param name="currentTurnIndex">L'indice di turno del dominio, a base 1 (0 prima dell'avvio).</param>
    /// <param name="playersInTurnOrder">I giocatori attivi non spettatori: quelli fra cui gira il turno.</param>
    public static int Current(int currentTurnIndex, int playersInTurnOrder) =>
        playersInTurnOrder <= 0 || currentTurnIndex <= 1 ? 1 : ((currentTurnIndex - 1) / playersInTurnOrder) + 1;
}
```

- [ ] **Step 4: Riallinea gli scenari**

Scrivi questo script **fuori dal repository** (per esempio `$TEMP/fix-turn-index.mjs`) con lo strumento di scrittura dei file, non con un heredoc. Per ogni scenario conserva il round con cui era stato scritto e il suo giocatore di turno, e ricalcola l'indice a base 1; tocca solo il numero, così la formattazione del file resta quella.

```js
import fs from 'node:fs';

const path = process.argv[2];
let text = fs.readFileSync(path, 'utf8');
const set = JSON.parse(text);
let changed = 0;

for (const scenario of set.scenarios) {
  const players = scenario.state.players;
  const n = players.length;
  const old = scenario.state.currentTurnIndex;
  // Il round con cui lo scenario è stato scritto (formula a base 0) e la posizione del giocatore di turno.
  const round = n <= 0 ? 1 : Math.floor(Math.max(old, 0) / n) + 1;
  const position = Math.max(players.indexOf(scenario.state.currentPlayer), 0);
  const next = (round - 1) * n + position + 1;
  if (next === old) continue;

  const idAt = text.indexOf(`"id": "${scenario.id}"`);
  const needle = `"currentTurnIndex": ${old}`;
  const from = text.indexOf(needle, idAt);
  const nextId = text.indexOf('"id": "', idAt + 1);
  if (idAt < 0 || from < 0 || (nextId >= 0 && from > nextId)) {
    throw new Error(`cannot locate currentTurnIndex of ${scenario.id}`);
  }

  text = text.slice(0, from) + `"currentTurnIndex": ${next}` + text.slice(from + needle.length);
  console.log(`${scenario.id}: ${old} -> ${next}`);
  changed++;
}

text = text.replace(`"version": ${set.version}`, '"version": 4');
fs.writeFileSync(path, text);
console.log(`${changed} scenarios updated, version ${set.version} -> 4`);
```

Run: `node "$TEMP/fix-turn-index.mjs" tests/llm-eval/session-assistant/scenarios.json && git diff --stat tests/llm-eval/session-assistant/scenarios.json`
Expected: una riga per scenario (per esempio `it-score-simple: 4 -> 6`, `it-score-dimension-explicit: 2 -> 3`, `it-rules-no-game: 0 -> 1`, `it-score-completed: 15 -> 16`), poi `… version 3 -> 4`. Nel diff cambiano solo `currentTurnIndex` e `version`: nessuna riga di `expected`. Cancella lo script.

- [ ] **Step 5: Verifica che i test dell'assistente passino**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "Category=Unit&FullyQualifiedName~SessionAssistant"`
Expected: PASS. Annota il numero. Se un test dell'evaluator costruisce in codice uno stato il cui indice ora dà un altro round, correggi l'**indice** con la stessa regola (`(round − 1) × n + posizione + 1`), non l'attesa.

- [ ] **Step 6: Scrivi i test del lettore (unit, con il mediator finto)**

`TESTS/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/LiveSessionAssistantReaderTests.cs`:

```csharp
using Api.BoundedContexts.AgentMemory.Application.DTOs;
using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// Spec 2026-10-11 §3.2 e §5.1: la porta di sola lettura degli strumenti, sopra le query della sessione
/// live. Qui la traduzione dal DTO di GameManagement allo stato che vede l'assistente.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4276")]
public sealed class LiveSessionAssistantReaderTests
{
    private static readonly Guid SessionId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Owner = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid Game = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Giulia = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Luca = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid Spettatrice = Guid.Parse("00000000-0000-0000-0000-000000000004");
    private static readonly Guid Uscito = Guid.Parse("00000000-0000-0000-0000-000000000005");
    private static readonly DateTime Now = new(2026, 10, 12, 20, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMediator> _mediator = new();

    private static LiveSessionPlayerDto Player(Guid id, string name, int total, PlayerRole role = PlayerRole.Player, bool active = true) =>
        new(id, UserId: null, name, AvatarUrl: null, PlayerColor.Red, role, TeamId: null, total, CurrentRank: 1, Now, active);

    private static LiveSessionDto Session(
        LiveSessionStatus status = LiveSessionStatus.InProgress, int turnIndex = 4, Guid? currentPlayer = null, Guid? gameId = null) =>
        new(
            Id: SessionId,
            SessionCode: "ABC123",
            GameId: gameId,
            GameName: "Azul",
            GameSlug: "azul",
            CreatedByUserId: Owner,
            Status: status,
            Visibility: PlayRecordVisibility.Private,
            GroupId: null,
            CreatedAt: Now,
            StartedAt: Now,
            PausedAt: null,
            CompletedAt: null,
            UpdatedAt: Now,
            LastSavedAt: null,
            CurrentTurnIndex: turnIndex,
            CurrentTurnPlayerId: currentPlayer ?? Marco,
            AgentMode: AgentSessionMode.None,
            Notes: null,
            GameState: null,
            Players:
            [
                Player(Marco, "Marco", 7, PlayerRole.Host),
                Player(Giulia, "Giulia", 12),
                Player(Luca, "Luca", 0),
                Player(Spettatrice, "Anna", 0, PlayerRole.Spectator),
                Player(Uscito, "Paolo", 3, active: false),
            ],
            Teams: [],
            RoundScores: [new LiveSessionRoundScoreDto(Marco, 1, "punti", 7, Unit: null, Now)],
            ScoringConfig: new LiveSessionScoringConfigDto(["punti"], new Dictionary<string, string>(StringComparer.Ordinal)));

    private LiveSessionAssistantReader Reader(LiveSessionDto session, string? phase = null)
    {
        _mediator.Setup(m => m.Send(It.Is<GetLiveSessionQuery>(q => q.SessionId == SessionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mediator.Setup(m => m.Send(It.Is<GetTurnPhasesQuery>(q => q.SessionId == SessionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TurnPhasesDto(
                session.CurrentTurnIndex, 0, phase, phase is null ? Array.Empty<string>() : new[] { phase }, phase is null ? 0 : 1, phase is not null));
        return new LiveSessionAssistantReader(_mediator.Object, SessionId, Owner);
    }

    [Fact]
    public async Task State_HasTheActivePlayers_AndTheRoundDerivedFromThem()
    {
        // Indice 4 con tre giocatori nel giro: è il primo turno del secondo giro. La spettatrice e chi è
        // uscito non contano, altrimenti il round resterebbe 1. (L'ordine di turno impostato a mano non
        // arriva nel DTO: è il limite dichiarato nello scostamento S1.)
        var state = await Reader(Session(turnIndex: 4), phase: "Rifornimento").GetStateAsync(TestContext.Current.CancellationToken);

        state.Players.Should().Equal(
            new AssistantPlayer(Marco, "Marco", 7, true),
            new AssistantPlayer(Giulia, "Giulia", 12, true),
            new AssistantPlayer(Luca, "Luca", 0, true));
        state.CurrentTurnIndex.Should().Be(4);
        state.CurrentRound.Should().Be(2);
        state.CurrentTurnPlayerId.Should().Be(Marco);
        state.CurrentPhase.Should().Be("Rifornimento");
        state.Dimensions.Should().Equal("punti");
        state.Scores.Should().Equal(new AssistantScoreCell(Marco, 1, "punti", 7));
        state.Status.Should().Be("InProgress");
        state.CanRecordScores.Should().BeTrue();
        state.CanAdvanceTurn.Should().BeTrue();
        state.GameName.Should().Be("Azul");
    }

    /// <summary>Review Focus 6: il primo turno di una partita non ancora avviata.</summary>
    [Fact]
    public async Task Round_IsOneBeforeTheSessionStarts_AndNothingCanBeWritten()
    {
        // Com'è una sessione appena creata con i suoi giocatori: indice 0, e il dominio indica già il
        // primo in ordine di turno.
        var state = await Reader(Session(LiveSessionStatus.Created, turnIndex: 0))
            .GetStateAsync(TestContext.Current.CancellationToken);

        state.CurrentRound.Should().Be(1);
        state.CurrentTurnPlayerId.Should().Be(Marco);
        state.Status.Should().Be("Created");
        state.CanRecordScores.Should().BeFalse();
        state.CanAdvanceTurn.Should().BeFalse();
    }

    [Theory]
    [InlineData(LiveSessionStatus.Paused, true, false)]
    [InlineData(LiveSessionStatus.Completed, false, false)]
    public async Task WhatCanBeWritten_FollowsTheSessionStatus(LiveSessionStatus status, bool scores, bool turn)
    {
        var state = await Reader(Session(status)).GetStateAsync(TestContext.Current.CancellationToken);

        state.CanRecordScores.Should().Be(scores);
        state.CanAdvanceTurn.Should().Be(turn);
    }

    [Fact]
    public async Task HouseRules_AreTheOnesOfTheGame_ForTheOwner()
    {
        _mediator.Setup(m => m.Send(It.Is<GetGameMemoryQuery>(q => q.GameId == Game && q.OwnerId == Owner), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameMemoryDto(Guid.NewGuid(), Game, Owner,
                [new HouseRuleDto(Guid.NewGuid(), "Si gioca fino a 5 round", Now, "manual")], []));

        var state = await Reader(Session(gameId: Game)).GetStateAsync(TestContext.Current.CancellationToken);

        state.GameId.Should().Be(Game);
        state.HouseRules.Should().Equal("Si gioca fino a 5 round");
    }

    [Fact]
    public async Task WithoutAGame_ThereAreNoHouseRules_AndTheMemoryIsNotAsked()
    {
        var state = await Reader(Session(gameId: null)).GetStateAsync(TestContext.Current.CancellationToken);

        state.GameId.Should().BeNull();
        state.HouseRules.Should().BeEmpty();
        _mediator.Verify(m => m.Send(It.IsAny<GetGameMemoryQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(LiveSessionStatus.Created, true)]
    [InlineData(LiveSessionStatus.Setup, true)]
    [InlineData(LiveSessionStatus.InProgress, true)]
    [InlineData(LiveSessionStatus.Paused, true)]
    [InlineData(LiveSessionStatus.Completed, false)]
    public async Task Access_SaysWhoOwnsTheSession_AndWhetherTheAssistantAnswers(LiveSessionStatus status, bool accepts)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetLiveSessionQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(Session(status));

        var access = await new LiveSessionAssistantDirectory(_mediator.Object)
            .GetAccessAsync(SessionId, TestContext.Current.CancellationToken);

        access.Should().Be(new AssistantSessionAccess(Owner, status.ToString(), accepts));
    }
}
```

- [ ] **Step 7: Verifica che non compili**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveSessionAssistantReaderTests"`
Expected: `CS0246` su `LiveSessionAssistantReader`, `LiveSessionAssistantDirectory`, `AssistantSessionAccess`.

- [ ] **Step 8: Scrivi la porta e gli adattatori**

`KB/Application/SessionAssistant/IAssistantSessionDirectory.cs`:

```csharp
#pragma warning disable MA0048 // File name must match type name - the directory port and what it returns
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Chi possiede una sessione live e se l'assistente può risponderle (spec §6.2, passo 1).
/// <c>AcceptsQuestions</c> è vero finché la sessione non è conclusa: anche prima dell'avvio, quando gli
/// strumenti di proposta rifiutano e resta solo la lettura.
/// </summary>
internal sealed record AssistantSessionAccess(Guid OwnerUserId, string Status, bool AcceptsQuestions);

/// <summary>
/// La porta verso la sessione live per il turno e per la conferma (spec §3.2). L'implementazione sta in
/// Infrastructure e passa dalle query di GameManagement: qui non compare nessun suo tipo.
/// </summary>
internal interface IAssistantSessionDirectory
{
    /// <summary>Lancia <see cref="Api.Middleware.Exceptions.NotFoundException"/> se la sessione non esiste.</summary>
    Task<AssistantSessionAccess> GetAccessAsync(Guid liveSessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Il lettore degli strumenti, legato a questa sessione dal server: il modello non può sceglierne
    /// un'altra. <paramref name="ownerUserId"/> serve alle house rules, che sono del proprietario.
    /// </summary>
    ISessionAssistantReader CreateReader(Guid liveSessionId, Guid ownerUserId);
}
```

`KB/Infrastructure/SessionAssistant/LiveSessionAssistantDirectory.cs`:

```csharp
using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using MediatR;

#pragma warning disable MA0048 // File name must match type name - the directory, its reader and their mapping
namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// <see cref="IAssistantSessionDirectory"/> sopra le query di GameManagement (spec §3.2). È qui, e non
/// negli strumenti, che vive il mediator: e solo per leggere.
/// </summary>
internal sealed class LiveSessionAssistantDirectory(IMediator mediator) : IAssistantSessionDirectory
{
    public async Task<AssistantSessionAccess> GetAccessAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var session = await mediator.Send(new GetLiveSessionQuery(liveSessionId), cancellationToken).ConfigureAwait(false);
        return new AssistantSessionAccess(
            session.CreatedByUserId,
            session.Status.ToString(),
            // Anche Created: MoveToSetup non ha chiamanti, quindi senza Created l'assistente non
            // risponderebbe mai prima dell'avvio (scostamento S15 del piano 2).
            AcceptsQuestions: session.Status != LiveSessionStatus.Completed);
    }

    public ISessionAssistantReader CreateReader(Guid liveSessionId, Guid ownerUserId) =>
        new LiveSessionAssistantReader(mediator, liveSessionId, ownerUserId);
}

/// <summary>Lo stato vivo della sessione, riletto a ogni chiamata: gli strumenti non lo tengono in memoria.</summary>
internal sealed class LiveSessionAssistantReader(IMediator mediator, Guid liveSessionId, Guid ownerUserId) : ISessionAssistantReader
{
    public async Task<AssistantSessionState> GetStateAsync(CancellationToken cancellationToken)
    {
        var session = await mediator.Send(new GetLiveSessionQuery(liveSessionId), cancellationToken).ConfigureAwait(false);
        var phases = await mediator.Send(new GetTurnPhasesQuery(liveSessionId), cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> houseRules = [];
        if (session.GameId is { } gameId)
        {
            var memory = await mediator.Send(new GetGameMemoryQuery(gameId, ownerUserId), cancellationToken).ConfigureAwait(false);
            houseRules = memory?.HouseRules.Select(rule => rule.Description).ToList() ?? [];
        }

        return LiveSessionAssistantState.From(session, phases.CurrentPhaseName, houseRules);
    }

    /// <summary>
    /// <c>cerca_regole</c> arriva con la fetta A5 e fino ad allora non è offerto al modello: una chiamata
    /// qui è un errore di cablaggio, e fallisce invece di rispondere «nessun passaggio trovato».
    /// </summary>
    public Task<IReadOnlyList<AssistantRulePassage>> SearchRulesAsync(string question, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Rule search is not available to the session assistant yet (slice A5).");
}

internal static class LiveSessionAssistantState
{
    public static AssistantSessionState From(LiveSessionDto session, string? phase, IReadOnlyList<string> houseRules)
    {
        // I giocatori fra cui gira il turno: gli attivi non spettatori. Il dominio conta sull'ordine di
        // turno, che il DTO non espone: con l'ordine predefinito (quello di ingresso) coincidono.
        var inTurnOrder = session.Players.Where(p => p.IsActive && p.Role != PlayerRole.Spectator).ToList();
        return new AssistantSessionState(
            session.GameId,
            string.IsNullOrWhiteSpace(session.GameName) ? null : session.GameName,
            session.Status.ToString(),
            CanRecordScores: session.Status is LiveSessionStatus.InProgress or LiveSessionStatus.Paused,
            CanAdvanceTurn: session.Status == LiveSessionStatus.InProgress,
            inTurnOrder.Select(p => new AssistantPlayer(p.Id, p.DisplayName, p.TotalScore, p.IsActive)).ToList(),
            session.CurrentTurnPlayerId,
            session.CurrentTurnIndex,
            AssistantRound.Current(session.CurrentTurnIndex, inTurnOrder.Count),
            phase,
            session.ScoringConfig.EnabledDimensions,
            session.RoundScores.Select(s => new AssistantScoreCell(s.PlayerId, s.Round, s.Dimension, s.Value)).ToList(),
            houseRules);
    }
}
```

In `SessionAssistantServiceExtensions.cs`:

```csharp
        services.AddScoped<IAssistantSessionDirectory, LiveSessionAssistantDirectory>();
```

In `SessionAssistantRegistrationTests.cs`, accanto agli altri doppi:

```csharp
        services.AddScoped(_ => Mock.Of<MediatR.IMediator>());
```

e, in fondo al test:

```csharp
        scope.ServiceProvider.GetRequiredService<IAssistantSessionDirectory>().Should().NotBeNull();
```

- [ ] **Step 9: Verifica che i test del lettore passino**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveSessionAssistantReaderTests|FullyQualifiedName~SessionAssistantRegistrationTests"`
Expected: PASS. Annota il numero.

- [ ] **Step 10: Scrivi il supporto dei test di integrazione e il test sul database vero**

`TESTS/Integration/KnowledgeBase/SessionAssistant/SessionAssistantTestHost.cs`:

```csharp
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.Hubs;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

#pragma warning disable MA0048 // File name must match type name - the shared host of the session assistant tests
namespace Api.Tests.Integration.KnowledgeBase.SessionAssistant;

/// <summary>
/// Host condiviso fra i test di una classe, database per test: il profilo dell'agente è un singleton, e
/// ogni test lo pubblica a modo suo. Doppi solo ai margini: il gateway LLM (copione) e l'hub SignalR
/// (registra). Sono con stato e vivono quanto la classe: ogni test li azzera nel suo InitializeAsync.
/// </summary>
public sealed class SessionAssistantHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "session_assistant")
{
    internal ScriptedChatGateway Llm { get; } = new();

    internal RecordingSessionHub Hub { get; } = new();

    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILlmService>();
            services.AddSingleton(Llm.Service);
            // SignalR registra IHubContext<> aperto: una registrazione chiusa per questo hub vince.
            services.AddSingleton(Hub.Context);
        }));
}

/// <summary>Il gateway con un copione: una risposta per chiamata, nell'ordine dato, e le richieste ricevute.</summary>
internal sealed class ScriptedChatGateway
{
    private readonly ConcurrentQueue<LlmCompletionResult> _script = new();
    private readonly ConcurrentQueue<LlmChatRequest> _requests = new();
    private readonly Mock<ILlmService> _mock = new();

    public ScriptedChatGateway()
    {
        _mock.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmChatRequest request, CancellationToken _) =>
            {
                _requests.Enqueue(request);
                return _script.TryDequeue(out var next)
                    ? next
                    : LlmCompletionResult.CreateFailure("The test script has no more responses.");
            });
    }

    public ILlmService Service => _mock.Object;

    public IReadOnlyList<LlmChatRequest> Requests => [.. _requests];

    public void Reset(params LlmCompletionResult[] script)
    {
        _requests.Clear();
        _script.Clear();
        foreach (var response in script)
        {
            _script.Enqueue(response);
        }
    }

    public static LlmCompletionResult Calls(string tool, string argumentsJson) =>
        LlmCompletionResult.CreateSuccess(string.Empty) with
        {
            ToolCalls = [new LlmToolCall($"call_{Guid.NewGuid():N}", tool, argumentsJson)],
        };

    public static LlmCompletionResult Says(string text) => LlmCompletionResult.CreateSuccess(text);
}

/// <summary>L'hub della sessione, finto: conta i messaggi trasmessi ai gruppi, per nome del metodo.</summary>
internal sealed class RecordingSessionHub
{
    private readonly ConcurrentQueue<string> _methods = new();
    private readonly Mock<IHubContext<GameStateHub>> _context = new();

    public RecordingSessionHub()
    {
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((method, _, _) => _methods.Enqueue(method))
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients> { DefaultValue = DefaultValue.Mock };
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        clients.Setup(c => c.All).Returns(proxy.Object);
        _context.Setup(c => c.Clients).Returns(clients.Object);
        _context.Setup(c => c.Groups).Returns(Mock.Of<IGroupManager>());
    }

    public IHubContext<GameStateHub> Context => _context.Object;

    public int Count(string method) => _methods.Count(m => string.Equals(m, method, StringComparison.Ordinal));

    public void Reset() => _methods.Clear();
}

internal sealed record LiveTable(Guid SessionId, IReadOnlyDictionary<string, Guid> Players);

internal static class SessionAssistantTestData
{
    public const string ItalianPrompt = "Sei l'assistente al tavolo. Non scrivi mai la partita: proponi.";
    public const string EnglishPrompt = "You are the table assistant. You never write the game: you propose.";

    public static async Task<(Guid UserId, string Token)> SeedHostAsync(WebApplicationFactory<Program> factory, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: ct);
    }

    /// <summary>Un comando per scope, come una richiesta HTTP: ogni comando della sessione live salva da sé.</summary>
    public static async Task<T> SendAsync<T>(WebApplicationFactory<Program> factory, IRequest<T> request, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request, ct);
    }

    public static async Task SendAsync(WebApplicationFactory<Program> factory, IRequest request, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request, ct);
    }

    /// <summary>Una sessione senza gioco (niente quota) con la dimensione «punti» e i giocatori dati, nell'ordine.</summary>
    public static async Task<LiveTable> CreateTableAsync(
        WebApplicationFactory<Program> factory, Guid ownerId, bool start, CancellationToken ct, params string[] players)
    {
        var sessionId = await SendAsync(factory, new CreateLiveSessionCommand(
            UserId: ownerId, GameName: "Azul", GameId: null, ScoringDimensions: ["punti"]), ct);
        var ids = new Dictionary<string, Guid>(StringComparer.Ordinal);
        for (var i = 0; i < players.Length; i++)
        {
            ids[players[i]] = await SendAsync(factory, new AddPlayerToLiveSessionCommand(sessionId, players[i], (PlayerColor)i), ct);
        }

        if (start)
        {
            await SendAsync(factory, new StartLiveSessionCommand(sessionId, ownerId, UserTier.Free, Role.User), ct);
        }

        return new LiveTable(sessionId, ids);
    }

    /// <summary>
    /// Pubblica, come farà l'admin, una versione del profilo con l'assistente abilitato. Il catalogo viene
    /// dal seed, che dichiara il tool calling di <c>deepseek-flash</c>, il modello principale del profilo.
    /// </summary>
    /// <returns>L'id della versione pubblicata.</returns>
    public static async Task<Guid> EnableAssistantAsync(
        WebApplicationFactory<Program> factory,
        CancellationToken ct,
        Func<SessionAssistantSettings, SessionAssistantSettings>? change = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IMediator>().Send(new SeedAiModelsCommand(), ct);

        var profile = await services.GetRequiredService<IAgentProfileRepository>().GetAsync(ct)
            ?? throw new InvalidOperationException("The migrations publish the first version of the agent profile.");
        var c = profile.Published.Content;
        var assistant = SessionAssistantSettings.Create(
            enabled: true,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = ItalianPrompt, ["en"] = EnglishPrompt });
        profile.CreateDraft(AgentProfileContent.Create(
            c.Name, c.Description, c.Persona, c.PrimaryModelId, c.FallbackModelId, c.Temperature, c.MaxResponseTokens,
            c.CitationStyle, c.SystemPrompts, c.FallbackLanguage, c.CandidatePoolSize, c.FinalTopK, c.MinScore,
            c.VectorWeight, c.KeywordWeight, c.RerankerEnabled, c.AllowedCategories.ToList(),
            change?.Invoke(assistant) ?? assistant));
        var toolCallingModels = await services.GetRequiredService<IToolCallingModelCatalog>().GetToolCallingModelIdsAsync(ct);
        var version = profile.PublishDraft(toolCallingModels);
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        return version.Id;
    }

    /// <summary>Una domanda all'assistente: lo stato HTTP, gli eventi SSE (se la risposta è uno stream) e il corpo.</summary>
    public static async Task<(HttpStatusCode Status, List<JsonElement> Events, string Body)> AskAsync(
        HttpClient client, string token, Guid sessionId, string question, CancellationToken ct, string? language = "it")
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/live-sessions/{sessionId}/assistant", token, new { question, language });
        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        var events = new List<JsonElement>();
        if (string.Equals(response.Content.Headers.ContentType?.MediaType, "text/event-stream", StringComparison.Ordinal))
        {
            foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("data:", StringComparison.Ordinal))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(trimmed["data:".Length..].Trim());
                events.Add(document.RootElement.Clone());
            }
        }

        return (response.StatusCode, events, body);
    }
}
```

`TESTS/Integration/KnowledgeBase/SessionAssistant/LiveSessionAssistantDirectoryIntegrationTests.cs`:

```csharp
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase.SessionAssistant;

/// <summary>
/// Scostamento S1 del piano 2, contro il dominio vero: il round che il lettore deriva segue l'indice di
/// turno a base 1 di <c>LiveGameSession</c>, e il giocatore di turno è quello che dice il dominio.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4276")]
public sealed class LiveSessionAssistantDirectoryIntegrationTests(SessionAssistantHostFixture host)
    : IClassFixture<SessionAssistantHostFixture>, IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await host.BeginTestAsync();
        host.Llm.Reset();
        host.Hub.Reset();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<AssistantSessionState> ReadAsync(Guid sessionId, Guid ownerId)
    {
        await using var scope = host.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAssistantSessionDirectory>()
            .CreateReader(sessionId, ownerId).GetStateAsync(Ct);
    }

    [Fact]
    public async Task TheRound_FollowsTheTurnOrderOfTheRealSession()
    {
        var (owner, _) = await SessionAssistantTestData.SeedHostAsync(host.Factory, Ct);
        var table = await SessionAssistantTestData.CreateTableAsync(host.Factory, owner, start: true, Ct, "Marco", "Giulia", "Luca");
        await SessionAssistantTestData.SendAsync(
            host.Factory, new RecordLiveSessionScoreCommand(table.SessionId, table.Players["Marco"], 1, "punti", 7), Ct);

        // Appena avviata: indice 1, tocca al primo, primo giro.
        var first = await ReadAsync(table.SessionId, owner);
        first.CurrentTurnIndex.Should().Be(1);
        first.CurrentRound.Should().Be(1);
        first.CurrentTurnPlayerId.Should().Be(table.Players["Marco"]);

        // Due passaggi: tocca all'ultimo, ed è ancora il primo giro (con la formula a base 0 era il secondo).
        await SessionAssistantTestData.SendAsync(host.Factory, new AdvanceLiveSessionTurnCommand(table.SessionId), Ct);
        await SessionAssistantTestData.SendAsync(host.Factory, new AdvanceLiveSessionTurnCommand(table.SessionId), Ct);
        var last = await ReadAsync(table.SessionId, owner);
        last.CurrentTurnIndex.Should().Be(3);
        last.CurrentRound.Should().Be(1);
        last.CurrentTurnPlayerId.Should().Be(table.Players["Luca"]);

        // Un altro passaggio: si ricomincia dal primo, secondo giro.
        await SessionAssistantTestData.SendAsync(host.Factory, new AdvanceLiveSessionTurnCommand(table.SessionId), Ct);
        var second = await ReadAsync(table.SessionId, owner);
        second.CurrentTurnIndex.Should().Be(4);
        second.CurrentRound.Should().Be(2);
        second.CurrentTurnPlayerId.Should().Be(table.Players["Marco"]);

        second.Players.Select(p => p.Name).Should().BeEquivalentTo("Marco", "Giulia", "Luca");
        second.Players.Single(p => p.Name == "Marco").Total.Should().Be(7);
        second.Dimensions.Should().Equal("punti");
        second.Scores.Should().Equal(new AssistantScoreCell(table.Players["Marco"], 1, "punti", 7));
        second.Status.Should().Be("InProgress");
        second.GameId.Should().BeNull();
        second.HouseRules.Should().BeEmpty();
    }

    [Fact]
    public async Task Access_FollowsTheLifeOfTheSession()
    {
        var (owner, _) = await SessionAssistantTestData.SeedHostAsync(host.Factory, Ct);
        var table = await SessionAssistantTestData.CreateTableAsync(host.Factory, owner, start: false, Ct, "Marco");

        async Task<AssistantSessionAccess> AccessAsync()
        {
            await using var scope = host.Factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IAssistantSessionDirectory>().GetAccessAsync(table.SessionId, Ct);
        }

        (await AccessAsync()).Should().Be(new AssistantSessionAccess(owner, "Created", true));

        await SessionAssistantTestData.SendAsync(
            host.Factory,
            new StartLiveSessionCommand(table.SessionId, owner, Api.SharedKernel.Domain.ValueObjects.UserTier.Free, Api.SharedKernel.Domain.ValueObjects.Role.User),
            Ct);
        (await AccessAsync()).Should().Be(new AssistantSessionAccess(owner, "InProgress", true));

        await SessionAssistantTestData.SendAsync(host.Factory, new CompleteLiveSessionCommand(table.SessionId), Ct);
        (await AccessAsync()).Should().Be(new AssistantSessionAccess(owner, "Completed", false));
    }

    [Fact]
    public async Task Access_ToAnUnknownSession_IsNotFound()
    {
        await using var scope = host.Factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IAssistantSessionDirectory>();

        var act = () => directory.GetAccessAsync(Guid.NewGuid(), Ct);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

- [ ] **Step 11: Esegui il test di integrazione**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveSessionAssistantDirectoryIntegrationTests"`
Expected: PASS, 3 test. Annota il tempo. Se `TheRound_FollowsTheTurnOrderOfTheRealSession` fallisce sul giocatore di turno, **fermati**: vuol dire che l'ordine di turno del dominio non è quello di ingresso dei giocatori, e la formula del round va rivista con quel dato.

- [ ] **Step 12: Spec, #4270 e commit**

Nella spec, §5.1, sostituisci il capoverso «**Round.**» con:

> **Round.** `LiveGameSession` non ha un round: ha `CurrentTurnIndex`, che vale 0 prima dell'avvio, 1 con `Start()` e cresce a ogni turno di ogni giocatore. Il precursore S4 usa l'indice di turno come round, quindi ogni passaggio di turno apre una nuova cella. L'assistente usa invece un **round derivato**: `(CurrentTurnIndex − 1) / n + 1` per un indice ≥ 1, e 1 prima dell'avvio, dove `n` sono i giocatori attivi non spettatori, cioè quelli fra cui gira il turno. È un giro completo del tavolo, coerente con `GetCurrentTurnPlayerId`, che usa `(CurrentTurnIndex − 1) % n`. Questa definizione va allineata con l'editor della scheda Score di #4156, che lavora su «giocatore × dimensione × round» (§13). Dalla versione 4 il set di scenari usa lo stesso indice a base 1.

Nella stessa spec, §6.2 passo 1, sostituisci «che la sessione sia `Setup`, `InProgress` o `Paused`» con «che la sessione non sia `Completed` (anche `Created`: `MoveToSetup` non ha chiamanti, quindi una sessione passa da `Created` a `InProgress`, e senza `Created` l'assistente non risponderebbe mai prima dell'avvio)».

Commenta #4270: «Il set di scenari è alla versione 4 (indice di turno a base 1, #4276): la misura di conferma va fatta su quella.»

```bash
git add apps/api tests/llm-eval/session-assistant/scenarios.json docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md
git commit -m "feat(ai): lettore vero della sessione e round a base 1 (#4276)"
```

### Task 7: `ChatThread.LiveSessionId`

**Files:**
- Modify: `KB/Domain/Entities/ChatThread.cs`
- Modify: `KB/Domain/Repositories/IChatThreadRepository.cs`
- Modify: `KB/Infrastructure/Persistence/ChatThreadRepository.cs`
- Modify: `KB/Application/Queries/GetMyChatHistoryQueryHandler.cs`
- Modify: `API/Infrastructure/Entities/KnowledgeBase/ChatThreadEntity.cs`
- Create: `API/Infrastructure/EntityConfigurations/KnowledgeBase/ChatThreadEntityConfiguration.cs`
- Create (generata): `API/Infrastructure/Migrations/<timestamp>_AddLiveSessionIdToChatThreads.cs`
- Test: `TESTS/BoundedContexts/KnowledgeBase/Domain/Entities/ChatThreadLiveSessionTests.cs`
- Test: `TESTS/Integration/KnowledgeBase/ChatThreadRepositoryIntegrationTests.cs` (tre test in più)
- Test: `TESTS/BoundedContexts/KnowledgeBase/Application/Handlers/GetMyChatHistoryQueryHandlerTests.cs` (un test in più)
- Modify: `docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md` (§6.2, scostamenti S2 e S9)

**Interfaces:**
- Consumes: `ChatThread(Guid id, Guid userId, Guid? gameId = null, string? title = null, Guid? agentId = null, string? agentType = null)`; `ChatMessage(string content, string role, int sequenceNumber, DateTime? timestamp = null, Guid? id = null)`.
- Produces:
  - `ChatThread(..., string? agentType = null, Guid? liveSessionId = null)` e `ChatThread.LiveSessionId`.
  - `Task<ChatThread?> IChatThreadRepository.FindByLiveSessionIdAsync(Guid liveSessionId, CancellationToken cancellationToken = default)`: il thread più vecchio della sessione, **senza eventi di dominio pendenti**.
  - I thread con `LiveSessionId` non compaiono più fra le chat dell'utente: né nell'elenco (`FindByUserIdFilteredAsync`), né nello storico e nel cruscotto (`GetMyChatHistoryQueryHandler`, `CountByUserIdAsync`). `FindByUserIdAsync` continua a restituirli, perché lo usa l'export dei dati dell'utente.
  - Colonna `"ChatThreads"."LiveSessionId"` (`uuid`, nullable) con indice `IX_ChatThreads_LiveSessionId`.

- [ ] **Step 1: Scrivi i test che falliscono**

`TESTS/BoundedContexts/KnowledgeBase/Domain/Entities/ChatThreadLiveSessionTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>Spec 2026-10-11 §6.2: il thread dell'assistente è legato alla sessione live, non a un gioco.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4276")]
public sealed class ChatThreadLiveSessionTests
{
    [Fact]
    public void AThread_CanBelongToALiveSession_WithoutAGame()
    {
        var session = Guid.NewGuid();

        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid(), gameId: null, title: null, agentId: null,
            agentType: "session-assistant", liveSessionId: session);

        thread.LiveSessionId.Should().Be(session);
        thread.GameId.Should().BeNull();
    }

    [Fact]
    public void AnOrdinaryThread_HasNoLiveSession()
    {
        new ChatThread(Guid.NewGuid(), Guid.NewGuid()).LiveSessionId.Should().BeNull();
    }
}
```

In `ChatThreadRepositoryIntegrationTests.cs`, in fondo alla classe (il messaggio ha il nome completo, per non aggiungere un `using` che in quel file potrebbe creare ambiguità):

```csharp
    #region LiveSessionId (#4276)

    private static readonly Guid TestLiveSessionId = new("50000000-0000-0000-0000-000000000001");

    private static ChatThread CreateAssistantThread(Guid threadId, Guid userId, Guid liveSessionId) =>
        new(id: threadId, userId: userId, gameId: null, title: null, agentId: null,
            agentType: "session-assistant", liveSessionId: liveSessionId);

    [Fact]
    public async Task FindByLiveSessionIdAsync_ReturnsTheThreadOfTheSession_WithItsMessageIds()
    {
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var thread = CreateAssistantThread(TestThreadId1, TestUserId1, TestLiveSessionId);
        thread.AddUserMessage("Marco fa 5 punti");
        var answerId = Guid.NewGuid();
        thread.AddMessage(new Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.ChatMessage(
            "Ho preparato la proposta.", "assistant", thread.Messages.Count, id: answerId));
        await _repository!.AddAsync(thread, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var found = await _repository.FindByLiveSessionIdAsync(TestLiveSessionId, TestCancellationToken);

        found.Should().NotBeNull();
        found!.Id.Should().Be(TestThreadId1);
        found.LiveSessionId.Should().Be(TestLiveSessionId);
        found.GameId.Should().BeNull("un gioco privato violerebbe la chiave esterna verso shared_games");
        found.Messages.Should().HaveCount(2);
        found.Messages[1].Id.Should().Be(answerId, "le proposte puntano al messaggio dell'assistente per id");
        found.DomainEvents.Should().BeEmpty(
            "ricaricare un thread non è un evento: senza, ogni turno riscriverebbe nell'outbox tutta la sua storia");
        (await _repository.FindByLiveSessionIdAsync(Guid.NewGuid(), TestCancellationToken)).Should().BeNull();
    }

    /// <summary>
    /// <c>UpdateAsync</c> riscrive l'intera riga da <c>MapToPersistence</c>: una colonna che la mappatura
    /// dimentica viene azzerata al secondo turno, e il thread smette di appartenere alla sessione.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_KeepsTheLiveSessionId()
    {
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        await _repository!.AddAsync(CreateAssistantThread(TestThreadId1, TestUserId1, TestLiveSessionId), TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var loaded = await _repository.FindByLiveSessionIdAsync(TestLiveSessionId, TestCancellationToken);
        loaded!.AddUserMessage("a chi tocca?");
        await _repository.UpdateAsync(loaded, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        var persisted = await _dbContext!.ChatThreads.AsNoTracking()
            .SingleAsync(t => t.Id == TestThreadId1, TestCancellationToken);
        persisted.LiveSessionId.Should().Be(TestLiveSessionId);
        (await _repository.FindByLiveSessionIdAsync(TestLiveSessionId, TestCancellationToken))!.Messages.Should().HaveCount(1);
    }

    [Fact]
    public async Task TheUserChatLists_LeaveOutTheAssistantThreads_ButTheExportStillSeesThem()
    {
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        await _repository!.AddAsync(CreateTestChatThread(TestThreadId1, TestUserId1, null, "Regole di Azul"), TestCancellationToken);
        await _repository.AddAsync(CreateAssistantThread(TestThreadId2, TestUserId1, TestLiveSessionId), TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var (items, total) = await _repository.FindByUserIdFilteredAsync(TestUserId1, cancellationToken: TestCancellationToken);
        var counted = await _repository.CountByUserIdAsync(TestUserId1, TestCancellationToken);
        var exported = await _repository.FindByUserIdAsync(TestUserId1, TestCancellationToken);

        items.Select(t => t.Id).Should().Equal(TestThreadId1);
        total.Should().Be(1);
        counted.Should().Be(1, "è il totale dello storico delle chat");
        exported.Should().HaveCount(2, "l'export dei dati dell'utente comprende ogni sua conversazione");
    }

    #endregion
```

In `GetMyChatHistoryQueryHandlerTests.cs`, in fondo alla classe (prima dell'helper `CreateThread`):

```csharp
    /// <summary>
    /// #4276: lo storico delle chat (e il cruscotto, che lo usa) non mostra il thread dell'assistente in
    /// partita. Il repository glielo restituisce comunque, perché lo stesso metodo serve all'export.
    /// </summary>
    [Fact]
    public async Task Handle_LeavesOutTheAssistantThreads()
    {
        var userId = Guid.NewGuid();
        var ordinary = CreateThread(userId, Guid.NewGuid(), "Regole di Azul");
        var assistant = new ChatThread(Guid.NewGuid(), userId, gameId: null, title: null, agentId: null,
            agentType: "session-assistant", liveSessionId: Guid.NewGuid());
        _mockRepository.Setup(r => r.CountByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockRepository.Setup(r => r.FindByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatThread> { assistant, ordinary });

        var result = await _handler.Handle(
            new GetMyChatHistoryQuery(userId, Skip: 0, Take: 10), TestContext.Current.CancellationToken);

        result.Chats.Select(chat => chat.Id).Should().Equal(ordinary.Id);
        result.TotalCount.Should().Be(1);
    }
```

- [ ] **Step 2: Verifica che non compili**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ChatThreadLiveSessionTests"`
Expected: `CS1739` sul parametro `liveSessionId` e `CS1061` su `LiveSessionId` e `FindByLiveSessionIdAsync`.

- [ ] **Step 3: Implementa dominio, entità, mappatura e query**

In `ChatThread.cs`, dopo la proprietà `AgentType`:

```csharp
    /// <summary>
    /// Spec 2026-10-11 §6.2: la sessione live a cui appartiene il thread dell'assistente in partita. Quel
    /// thread nasce senza <see cref="GameId"/>, perché la chiave esterna del gioco punta al catalogo
    /// condiviso e una sessione può essere su un gioco privato.
    /// </summary>
    public Guid? LiveSessionId { get; private set; }
```

Nel costruttore pubblico, il parametro in coda `Guid? liveSessionId = null` dopo `string? agentType = null`, e l'assegnazione `LiveSessionId = liveSessionId;` dopo `AgentType = agentType?.Trim();`.

In `ChatThreadEntity.cs`, dopo `AgentType`:

```csharp
    public Guid? LiveSessionId { get; set; } // #4276: il thread dell'assistente in partita
```

`API/Infrastructure/EntityConfigurations/KnowledgeBase/ChatThreadEntityConfiguration.cs`:

```csharp
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// La tabella <c>ChatThreads</c> resta mappata per convenzione; qui solo l'indice sulla sessione live
/// (#4276), su cui il turno dell'assistente cerca il suo thread. Senza chiave esterna: la sessione è un
/// aggregato di un altro confine, citato per id.
/// </summary>
internal class ChatThreadEntityConfiguration : IEntityTypeConfiguration<ChatThreadEntity>
{
    public void Configure(EntityTypeBuilder<ChatThreadEntity> builder)
    {
        builder.HasIndex(t => t.LiveSessionId).HasDatabaseName("IX_ChatThreads_LiveSessionId");
    }
}
```

In `IChatThreadRepository.cs`, dopo `FindActiveByAgentIdAsync`:

```csharp
    /// <summary>
    /// Il thread dell'assistente in partita di una sessione live (#4276), o <c>null</c> al primo turno.
    /// Se due primi turni concorrenti ne hanno creati due, vale il più vecchio.
    /// </summary>
    Task<ChatThread?> FindByLiveSessionIdAsync(Guid liveSessionId, CancellationToken cancellationToken = default);
```

In `ChatThreadRepository.cs`:

1. Dopo `FindActiveByAgentIdAsync`:

```csharp
    public async Task<ChatThread?> FindByLiveSessionIdAsync(Guid liveSessionId, CancellationToken cancellationToken = default)
    {
        var threadEntity = await DbContext.ChatThreads
            .AsNoTracking()
            .Where(t => t.LiveSessionId == liveSessionId)
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (threadEntity is null)
        {
            return null;
        }

        // La reidratazione passa dal costruttore e da AddMessage, che sollevano eventi di dominio. Questo
        // thread viene aggiornato a ogni turno: senza azzerarli, ogni UpdateAsync riscriverebbe
        // nell'outbox e nell'audit l'intera storia della conversazione.
        var thread = MapToDomain(threadEntity);
        thread.ClearDomainEvents();
        return thread;
    }
```

2. In `FindByUserIdFilteredAsync`, la prima `Where` diventa:

```csharp
            // #4276: il thread dell'assistente in partita vive nella pagina della sessione, non
            // nell'elenco delle chat: continuarlo da lì mescolerebbe le due conversazioni.
            .Where(t => t.UserId == userId && t.LiveSessionId == null);
```

3. In `CountByUserIdAsync` il predicato diventa `t => t.UserId == userId && t.LiveSessionId == null`: è il totale dello storico delle chat.
4. In `MapToDomain`, nella costruzione del thread, la riga `agentType: entity.AgentType // Issue #4362` diventa due righe (la virgola va prima del commento):

```csharp
            agentType: entity.AgentType, // Issue #4362
            liveSessionId: entity.LiveSessionId // #4276
```

5. In `MapToPersistence`, dopo `AgentType = domainEntity.AgentType,`: `LiveSessionId = domainEntity.LiveSessionId, // #4276`.

In `GetMyChatHistoryQueryHandler.cs`, la paginazione parte dai thread visibili:

```csharp
        // #4276: il thread dell'assistente in partita vive nella pagina della sessione, non fra le chat.
        var paginatedThreads = threads
            .Where(thread => thread.LiveSessionId is null)
            .Skip(request.Skip)
            .Take(request.Take)
            .ToList();
```

- [ ] **Step 4: Genera la migrazione**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddLiveSessionIdToChatThreads
```

Expected: in `Up` solo `AddColumn<Guid>(name: "LiveSessionId", table: "ChatThreads", type: "uuid", nullable: true)` e `CreateIndex(name: "IX_ChatThreads_LiveSessionId", table: "ChatThreads", column: "LiveSessionId")`; in `Down` il contrario. Se compare altro, fermati: lo snapshot era fuori sincrono.

- [ ] **Step 5: Verifica che i test passino**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ChatThreadLiveSessionTests|FullyQualifiedName~GetMyChatHistoryQueryHandlerTests"` — Expected: PASS. Annota il numero.

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ChatThreadRepositoryIntegrationTests"` — Expected: PASS (integrazione, in locale). Annota numero e tempo.

- [ ] **Step 6: Verifica per perturbazione**

Copia `ChatThreadRepository.cs` fuori dal repository, togli da `MapToPersistence` la riga `LiveSessionId = domainEntity.LiveSessionId,` e riesegui `--filter "FullyQualifiedName~UpdateAsync_KeepsTheLiveSessionId"`.
Expected: FAIL (la colonna torna nulla). Ripristina per copia, `touch`, riesegui: PASS.

- [ ] **Step 7: Spec e commit**

Nella spec, §6.2 passo 2, sostituisci da «`ChatThread` riceve un `LiveSessionId` nullable» fino a «senza migrazione.» con:

> `ChatThread` riceve un `LiveSessionId` nullable (migrazione additiva) e il thread dell'assistente nasce **senza** `GameId`: la chiave esterna del gioco punta al catalogo condiviso, e una sessione può essere su un gioco privato. Quel thread non compare nell'elenco delle chat dell'utente (resta nell'export dei suoi dati). Il legame fra un messaggio e le sue proposte sta nella proposta (`message_id`): le `parts` in `MessagesJson` arrivano con le citazioni, nella fetta A5.

```bash
git add apps/api docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md
git commit -m "feat(ai): il thread dell'assistente appartiene alla sessione live (#4276)"
```

### Task 8: Il sink che salva le proposte e il loro DTO

**Files:**
- Modify: `KB/Domain/Repositories/IAssistantProposalRepository.cs` (un metodo)
- Modify: `KB/Infrastructure/Persistence/AssistantProposalRepository.cs` (un metodo)
- Create: `KB/Application/SessionAssistant/Turn/AssistantProposalDto.cs` (DTO e riassunto)
- Create: `KB/Application/SessionAssistant/Turn/RepositoryProposalSink.cs`
- Modify: `KB/Application/SessionAssistant/Tools/SessionAssistantToolset.cs` (limite dei punti come errore tipizzato)
- Test: `TESTS/BoundedContexts/KnowledgeBase/Application/SessionAssistant/SessionAssistantToolsetTests.cs` (un test in più)
- Test: `TESTS/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Turn/RepositoryProposalSinkTests.cs`
- Test: `TESTS/Integration/KnowledgeBase/SessionAssistant/AssistantProposalRepositoryIntegrationTests.cs` (un test in più)

**Interfaces:**
- Consumes: `IAssistantProposalSink`, `ScoreProposalDraft`, `TurnProposalDraft`, `ProposalReceipt` (A1); `AssistantProposal` e `IAssistantProposalRepository` (A2).
- Produces:
  - `Task<int> IAssistantProposalRepository.ExpirePendingBySessionAsync(Guid liveSessionId, DateTime now, CancellationToken cancellationToken = default)`: porta a `Expired` le proposte `Pending` della sessione con un solo `UPDATE`, subito, fuori dalla unit of work.
  - `internal sealed record AssistantProposalDto(Guid Id, string Kind, string Status, Guid MessageId, Guid? PlayerId, string? PlayerName, string? Dimension, int? Round, int? Points, int? CellValueBefore, int? ResultingValue, int? ExpectedTurnIndex, int? ConfirmedPoints, string? FailureCode, string Summary)` con `static AssistantProposalDto From(AssistantProposal proposal)`.
  - `AssistantProposalSummary.For(AssistantProposal)` e `AssistantProposalSummary.Outcome(AssistantProposalStatus)`.
  - `RepositoryProposalSink(IAssistantProposalRepository repository, Guid liveSessionId, Guid chatThreadId, Guid messageId, Guid profileVersionId, TimeProvider timeProvider)` con `IReadOnlyList<AssistantProposal> Created`. Aggiunge al repository e **non salva**: salva il turno.

La scadenza passa da un `UPDATE` di insieme, e non da `AssistantProposal.Expire` su entità caricate, per un motivo preciso: l'host può confermare una card mentre manda un'altra domanda. Con le entità tracciate una delle due richieste perderebbe sulla concorrenza al salvataggio finale del turno, dopo aver già pagato la chiamata al modello. Con l'`UPDATE` la corsa si chiude da sola: chi arriva secondo trova la proposta non più `Pending`.

- [ ] **Step 1: Scrivi i test che falliscono**

`TESTS/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Turn/RepositoryProposalSinkTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;

/// <summary>
/// Spec 2026-10-11 §5.2: l'unica uscita degli strumenti di proposta è una proposta salvata. Il sink la
/// lega alla sessione, al thread, al messaggio e alla versione del profilo del turno.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4276")]
public sealed class RepositoryProposalSinkTests
{
    private static readonly Guid Session = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Thread = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid Message = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
    private static readonly Guid Version = Guid.Parse("00000000-0000-0000-0000-0000000000a4");
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly Mock<IAssistantProposalRepository> _repository = new();
    private readonly List<AssistantProposal> _added = [];
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 20, 0, 0, TimeSpan.Zero));

    public RepositoryProposalSinkTests()
    {
        _repository.Setup(r => r.AddAsync(It.IsAny<AssistantProposal>(), It.IsAny<CancellationToken>()))
            .Callback<AssistantProposal, CancellationToken>((p, _) => _added.Add(p))
            .Returns(Task.CompletedTask);
    }

    private RepositoryProposalSink Sink() => new(_repository.Object, Session, Thread, Message, Version, _time);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ProposeScore_AddsAPendingProposal_BoundToTheTurn()
    {
        var sink = Sink();

        var receipt = await sink.ProposeScoreAsync(new ScoreProposalDraft(Marco, "Marco", "punti", 2, 5, 7), Ct);

        var proposal = _added.Should().ContainSingle().Subject;
        proposal.Status.Should().Be(AssistantProposalStatus.Pending);
        proposal.Payload.Should().Be(new AssistantProposalPayload(Marco, "Marco", "punti", 2, 5));
        proposal.Expected.Should().Be(new AssistantProposalExpectation(7, null));
        (proposal.LiveSessionId, proposal.ChatThreadId, proposal.MessageId, proposal.ProfileVersionId)
            .Should().Be((Session, Thread, Message, Version));
        proposal.CreatedAt.Should().Be(_time.GetUtcNow().UtcDateTime);
        receipt.Should().Be(new ProposalReceipt(proposal.Id, "+5 punti a Marco, round 2"));
        sink.Created.Should().Equal(proposal);
    }

    [Fact]
    public async Task ProposeTurn_AddsAProposalThatExpectsTheCurrentTurnIndex()
    {
        var receipt = await Sink().ProposeTurnAsync(new TurnProposalDraft(4, Marco), Ct);

        var proposal = _added.Should().ContainSingle().Subject;
        proposal.Kind.Should().Be(AssistantProposalKind.AdvanceTurn);
        proposal.Expected.Should().Be(new AssistantProposalExpectation(null, 4));
        receipt.Summary.Should().Be("Passare il turno al giocatore successivo");
    }

    /// <summary>
    /// Il sink non unisce due chiamate uguali. Un modello che ripete una chiamata lascia due card, e a
    /// vederlo dev'essere il set di valutazione (che conta le proposte di troppo, spec §7.1): se la
    /// produzione le unisse, il gate misurerebbe un difetto che l'host non vede più. La seconda non può
    /// comunque applicarsi: dopo la prima conferma la cella non è più quella attesa.
    /// </summary>
    [Fact]
    public async Task EveryCall_IsItsOwnProposal()
    {
        var sink = Sink();
        var draft = new ScoreProposalDraft(Marco, "Marco", "punti", 2, 5, 7);

        var first = await sink.ProposeScoreAsync(draft, Ct);
        var second = await sink.ProposeScoreAsync(draft, Ct);

        second.ProposalId.Should().NotBe(first.ProposalId);
        sink.Created.Should().HaveCount(2);
        _added.Should().HaveCount(2);
    }

    [Fact]
    public void TheDto_ShowsWhatTheHostConfirms_AndTheResultingCell()
    {
        var proposal = AssistantProposal.ForScore(Session, Thread, Message, Version, Marco, "Marco", "punti", 2, -2, 7, _time.GetUtcNow().UtcDateTime);

        var dto = AssistantProposalDto.From(proposal);

        dto.Should().Be(new AssistantProposalDto(
            proposal.Id, "Score", "Pending", Message, Marco, "Marco", "punti", 2, -2,
            CellValueBefore: 7, ResultingValue: 5, ExpectedTurnIndex: null, ConfirmedPoints: null, FailureCode: null,
            Summary: "-2 punti a Marco, round 2"));
    }

    [Fact]
    public void TheDto_OfAnEditedProposal_UsesTheConfirmedPoints()
    {
        var proposal = AssistantProposal.ForScore(Session, Thread, Message, Version, Marco, "Marco", "punti", 2, 5, 7, _time.GetUtcNow().UtcDateTime);
        proposal.BeginApplying(Guid.NewGuid());
        proposal.MarkEdited(3, _time.GetUtcNow().UtcDateTime);

        var dto = AssistantProposalDto.From(proposal);

        dto.Status.Should().Be("Edited");
        dto.Points.Should().Be(5);
        dto.ConfirmedPoints.Should().Be(3);
        dto.ResultingValue.Should().Be(10);
        dto.Summary.Should().Be("+3 punti a Marco, round 2");
    }

    [Fact]
    public void TheDto_OfATurnProposal_HasNoScoreFields()
    {
        var proposal = AssistantProposal.ForAdvanceTurn(Session, Thread, Message, Version, 4, _time.GetUtcNow().UtcDateTime);

        var dto = AssistantProposalDto.From(proposal);

        dto.Kind.Should().Be("AdvanceTurn");
        dto.ExpectedTurnIndex.Should().Be(4);
        dto.ResultingValue.Should().BeNull();
        dto.PlayerId.Should().BeNull();
    }

    [Theory]
    [InlineData(AssistantProposalStatus.Pending, "in attesa di conferma")]
    [InlineData(AssistantProposalStatus.Applying, "in applicazione")]
    [InlineData(AssistantProposalStatus.Accepted, "confermata dall'host")]
    [InlineData(AssistantProposalStatus.Edited, "confermata dall'host con una correzione")]
    [InlineData(AssistantProposalStatus.Rejected, "rifiutata dall'host")]
    [InlineData(AssistantProposalStatus.Expired, "scaduta senza conferma")]
    [InlineData(AssistantProposalStatus.Stale, "superata: la partita era cambiata")]
    [InlineData(AssistantProposalStatus.Failed, "non applicata")]
    public void EveryStatus_HasAnOutcomeTheModelCanRead(AssistantProposalStatus status, string expected) =>
        AssistantProposalSummary.Outcome(status).Should().Be(expected);
}
```

In `SessionAssistantToolsetTests.cs`, dopo `ProposeScore_NegativePoints_IsAValidDelta`:

```csharp
    /// <summary>
    /// Spec §6.3: argomenti non validi sono un errore tipizzato che il modello può leggere, non
    /// un'eccezione dentro lo strumento. Il limite è quello dell'aggregato (scostamento S14).
    /// </summary>
    [Theory]
    [InlineData(100_001)]
    [InlineData(-100_001)]
    [InlineData(int.MinValue)]
    public async Task ProposeScore_PointsBeyondTheLimit_IsATypedError(int points)
    {
        var toolset = Toolset();

        var result = await toolset.ProposeScoreAsync("Marco", points, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeOfType<AssistantToolError>().Which.Error.Should().Be("punti_fuori_limite");
        toolset.ErrorCount.Should().Be(1);
        _scores.Should().BeEmpty();
    }
```

In `AssistantProposalRepositoryIntegrationTests.cs`, in fondo alla classe:

```csharp
    /// <summary>
    /// #4276: la scadenza all'inizio di un turno è un solo UPDATE. Tocca le proposte Pending di quella
    /// sessione e nient'altro: non quella che l'host sta confermando, non quelle di un'altra sessione.
    /// </summary>
    [Fact]
    public async Task ExpirePendingBySession_ExpiresOnlyThePendingProposalsOfThatSession()
    {
        var session = Guid.NewGuid();
        var pendingId = await SaveAsync(NewScore(session, Guid.NewGuid()));
        var reserved = NewScore(session, Guid.NewGuid());
        reserved.BeginApplying(HostUser);
        var reservedId = await SaveAsync(reserved);
        var otherSessionId = await SaveAsync(NewScore(Guid.NewGuid(), Guid.NewGuid()));
        var now = new DateTime(2026, 10, 12, 21, 0, 0, DateTimeKind.Utc);

        int expired;
        await using (var scope = host.Factory.Services.CreateAsyncScope())
        {
            expired = await scope.ServiceProvider.GetRequiredService<IAssistantProposalRepository>()
                .ExpirePendingBySessionAsync(session, now, Ct);
        }

        expired.Should().Be(1);
        await using var verify = host.Factory.Services.CreateAsyncScope();
        var repository = verify.ServiceProvider.GetRequiredService<IAssistantProposalRepository>();
        var pending = await repository.GetReadOnlyAsync(pendingId, Ct);
        pending!.Status.Should().Be(AssistantProposalStatus.Expired);
        pending.ResolvedAt.Should().Be(now);
        (await repository.GetReadOnlyAsync(reservedId, Ct))!.Status.Should().Be(AssistantProposalStatus.Applying);
        (await repository.GetReadOnlyAsync(otherSessionId, Ct))!.Status.Should().Be(AssistantProposalStatus.Pending);
    }
```

- [ ] **Step 2: Verifica che non compili**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~RepositoryProposalSinkTests|FullyQualifiedName~SessionAssistantToolsetTests"`
Expected: `CS0246` su `RepositoryProposalSink`, `AssistantProposalDto`, `AssistantProposalSummary`. Quando compilerà, `ProposeScore_PointsBeyondTheLimit_IsATypedError` sarà rosso finché lo strumento non controlla il limite: senza, i punti fuori limite arrivano al sink e l'aggregato lancia.

- [ ] **Step 3: Implementa**

In `IAssistantProposalRepository.cs`:

```csharp
    /// <summary>
    /// Porta a <c>Expired</c> le proposte ancora <c>Pending</c> di una sessione: è l'inizio di un altro
    /// turno dell'assistente (spec §5.2). Un solo <c>UPDATE</c>, eseguito subito e fuori dalla unit of
    /// work: una conferma concorrente non fa fallire il turno, trova la proposta non più <c>Pending</c>.
    /// </summary>
    /// <returns>Quante proposte sono scadute.</returns>
    Task<int> ExpirePendingBySessionAsync(Guid liveSessionId, DateTime now, CancellationToken cancellationToken = default);
```

In `AssistantProposalRepository.cs`:

```csharp
    public Task<int> ExpirePendingBySessionAsync(Guid liveSessionId, DateTime now, CancellationToken cancellationToken = default) =>
        _db.Set<AssistantProposal>()
            .Where(p => p.LiveSessionId == liveSessionId && p.Status == AssistantProposalStatus.Pending)
            .ExecuteUpdateAsync(
                update => update
                    .SetProperty(p => p.Status, AssistantProposalStatus.Expired)
                    .SetProperty(p => p.ResolvedAt, now),
                cancellationToken);
```

`KB/Application/SessionAssistant/Turn/AssistantProposalDto.cs`:

```csharp
using System.Globalization;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;

#pragma warning disable MA0048 // File name must match type name - the proposal as the client sees it, and its summary
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;

/// <summary>
/// Una proposta come la vede il client: nell'evento <c>proposal</c> del turno e nella risposta di conferma
/// e rifiuto. <c>Kind</c> e <c>Status</c> sono stringhe, perché le opzioni JSON degli eventi SSE scrivono
/// gli enum come numeri. Per una proposta <c>Score</c>, <c>ResultingValue</c> è il valore che la cella
/// avrà (o ha) dopo la conferma.
/// </summary>
internal sealed record AssistantProposalDto(
    Guid Id,
    string Kind,
    string Status,
    Guid MessageId,
    Guid? PlayerId,
    string? PlayerName,
    string? Dimension,
    int? Round,
    int? Points,
    int? CellValueBefore,
    int? ResultingValue,
    int? ExpectedTurnIndex,
    int? ConfirmedPoints,
    string? FailureCode,
    string Summary)
{
    public static AssistantProposalDto From(AssistantProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var confirmed = proposal.ConfirmedPayload?.Points;
        return new AssistantProposalDto(
            proposal.Id,
            proposal.Kind.ToString(),
            proposal.Status.ToString(),
            proposal.MessageId,
            proposal.Payload.PlayerId,
            proposal.Payload.PlayerName,
            proposal.Payload.Dimension,
            proposal.Payload.Round,
            proposal.Payload.Points,
            proposal.Expected.CellValue,
            proposal.Kind == AssistantProposalKind.Score ? proposal.ScoreValueToWrite(confirmed) : null,
            proposal.Expected.TurnIndex,
            confirmed,
            proposal.FailureCode,
            AssistantProposalSummary.For(proposal));
    }
}

/// <summary>
/// Il testo di una proposta e del suo esito, per il modello: la ricevuta dello strumento e la storia dei
/// turni passati (spec §6.2). In italiano, come le descrizioni degli strumenti.
/// </summary>
internal static class AssistantProposalSummary
{
    public static string For(AssistantProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (proposal.Kind == AssistantProposalKind.AdvanceTurn)
        {
            return "Passare il turno al giocatore successivo";
        }

        var points = proposal.ConfirmedPayload?.Points ?? proposal.Payload.Points ?? 0;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{points:+0;-0;+0} {proposal.Payload.Dimension} a {proposal.Payload.PlayerName}, round {proposal.Payload.Round}");
    }

    public static string Outcome(AssistantProposalStatus status) => status switch
    {
        AssistantProposalStatus.Pending => "in attesa di conferma",
        AssistantProposalStatus.Applying => "in applicazione",
        AssistantProposalStatus.Accepted => "confermata dall'host",
        AssistantProposalStatus.Edited => "confermata dall'host con una correzione",
        AssistantProposalStatus.Failed => "non applicata",
        AssistantProposalStatus.Rejected => "rifiutata dall'host",
        AssistantProposalStatus.Expired => "scaduta senza conferma",
        AssistantProposalStatus.Stale => "superata: la partita era cambiata",
        _ => status.ToString(),
    };
}
```

`KB/Application/SessionAssistant/Turn/RepositoryProposalSink.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;

/// <summary>
/// Il sink vero degli strumenti di proposta (spec §5.2): crea una <see cref="AssistantProposal"/> e la
/// passa al repository. Non salva: lo fa il turno, insieme ai messaggi. Un'istanza per turno, legata alla
/// sessione, al thread e al messaggio dell'assistente dal server.
/// </summary>
internal sealed class RepositoryProposalSink(
    IAssistantProposalRepository repository,
    Guid liveSessionId,
    Guid chatThreadId,
    Guid messageId,
    Guid profileVersionId,
    TimeProvider timeProvider) : IAssistantProposalSink
{
    private readonly List<AssistantProposal> _created = [];

    /// <summary>Le proposte di questo turno, nell'ordine in cui il modello le ha chieste.</summary>
    public IReadOnlyList<AssistantProposal> Created => _created;

    public Task<ProposalReceipt> ProposeScoreAsync(ScoreProposalDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return AddAsync(
            AssistantProposal.ForScore(
                liveSessionId, chatThreadId, messageId, profileVersionId, draft.PlayerId, draft.PlayerName,
                draft.Dimension, draft.Round, draft.Points, draft.CellValueBefore, Now()),
            cancellationToken);
    }

    public Task<ProposalReceipt> ProposeTurnAsync(TurnProposalDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return AddAsync(
            AssistantProposal.ForAdvanceTurn(
                liveSessionId, chatThreadId, messageId, profileVersionId, draft.CurrentTurnIndex, Now()),
            cancellationToken);
    }

    private async Task<ProposalReceipt> AddAsync(AssistantProposal proposal, CancellationToken cancellationToken)
    {
        await repository.AddAsync(proposal, cancellationToken).ConfigureAwait(false);
        _created.Add(proposal);
        return Receipt(proposal);
    }

    private static ProposalReceipt Receipt(AssistantProposal proposal) => new(proposal.Id, AssistantProposalSummary.For(proposal));

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
```

In `SessionAssistantToolset.cs` aggiungi `using Api.BoundedContexts.KnowledgeBase.Domain.Entities;` e, in `ProposeScoreAsync`, subito dopo il controllo sul nome vuoto:

```csharp
        // Su long: Math.Abs(int.MinValue) lancerebbe. Il limite è quello dell'aggregato, che resta l'ultima
        // difesa: qui il modello riceve un errore che può leggere e riportare all'host.
        if (Math.Abs((long)punti) > AssistantProposal.MaxPointsPerProposal)
        {
            return Error(
                "punti_fuori_limite",
                $"I punti devono stare fra -{AssistantProposal.MaxPointsPerProposal} e {AssistantProposal.MaxPointsPerProposal}: chiedi all'host di ripetere il numero.");
        }
```

- [ ] **Step 4: Verifica che i test passino**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~RepositoryProposalSinkTests|FullyQualifiedName~SessionAssistantToolsetTests"` — Expected: PASS. Annota il numero.

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AssistantProposalRepositoryIntegrationTests"` — Expected: PASS, compreso il test nuovo.

- [ ] **Step 5: Commit**

```bash
git add apps/api
git commit -m "feat(ai): sink delle proposte, DTO e scadenza a inizio turno (#4276)"
```

### Task 9: Il turno

**Files:**
- Modify: `KB/Application/SessionAssistant/ChatTurnMeter.cs` (osservatore delle chiamate a strumenti)
- Create: `KB/Application/SessionAssistant/Turn/AssistantTurnEvent.cs` (evento e testi di ripiego)
- Create: `KB/Application/SessionAssistant/Turn/AskSessionAssistantCommand.cs` (comando e handler in streaming)
- Create: `KB/Application/SessionAssistant/Turn/AssistantTurnRunner.cs`
- Modify: `KB/Infrastructure/DependencyInjection/SessionAssistantServiceExtensions.cs`
- Test: `TESTS/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/SessionAssistantRegistrationTests.cs` (il runner)
- Test: `TESTS/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Turn/AssistantTurnTests.cs`

**Interfaces:**
- Consumes: `IAssistantSessionDirectory` e `AssistantSessionAccess` (Task 6); `IPublishedAgentProfileReader.GetAsync` → `PublishedAgentProfile(VersionId, VersionNumber, Content)`; `IToolCallingModelCatalog` (Task 4); `AgentProfileContent.SessionAssistantModelProblem` e `AgentProfile.AssistantModelWithoutToolsCode` (Task 5); `SessionAssistantSettings.PromptFor` (Task 3); `IChatThreadRepository.FindByLiveSessionIdAsync` (Task 7); `IAssistantProposalRepository.ExpirePendingBySessionAsync`, `ListOpenBySessionAsync`, `ListByThreadAsync`, `AddAsync`; `RepositoryProposalSink`, `AssistantProposalDto`, `AssistantProposalSummary` (Task 8); `IAssistantChatClientFactory.Create(AssistantModelSettings, ChatTurnMeter)`, `SessionAssistantToolset`, `AssistantConversation.Build(AssistantPromptSet, string, AssistantSessionState, IReadOnlyList<AssistantHistoryTurn>, string)`, `AssistantGatewayException.ErrorCode`, `ClaimedActionDetector` (A0, A1); `LlmBudgetAdmission.DailyBudgetErrorCode` e `MonthlyBudgetErrorCode`.
- Produces:
  - `internal sealed record AskSessionAssistantCommand(Guid LiveSessionId, Guid UserId, string Question, string? Language) : IStreamingQuery<AssistantTurnEvent>`.
  - `internal sealed record AssistantTurnEvent(string Type, string? Tool = null, AssistantProposalDto? Proposal = null, string? Text = null, Guid? ResponseId = null, Guid? ProfileVersionId = null, int? ProfileVersionNumber = null, string? Code = null)`, con `Type` fra `status`, `proposal`, `answer`, `done`, `error`.
  - Ordine degli eventi. Riuscito: `status` (senza strumento), uno `status` per ogni strumento chiamato, poi — **dopo il salvataggio** — un `proposal` per proposta, `answer`, `done`. Fallito a metà (tetto, scadenza, provider): come sopra, ma `error` al posto di `done`, e il testo di `answer` è quello di ripiego. Fallito in modo imprevisto: `status`, `error assistant_unavailable`, niente salvato.
  - Eccezioni **prima** del primo evento, che l'endpoint traduce in stato HTTP: `ValidationException` (400), `ForbiddenException` (403), `NotFoundException` (404), `ConflictException` con codice `session_state_invalid`, `assistant_disabled` o `assistant_model_without_tools` (409).
  - `ChatTurnMeter.ToolCalled` (`Action<string>?`, `init`).
  - Costanti su `AssistantTurnRunner`: `DisabledCode`, `SessionStateInvalidCode`, `BudgetPausedCode`, `UnavailableCode`, `TimeoutCode`, `ThreadAgentType = "session-assistant"`, `MaxQuestionLength = 2000`, `MaxLanguageLength = 16`.
  - `AssistantTurnTexts.ForError(string code, bool english, bool hasProposals)`, `NoAnswer(bool english, bool hasProposals)`, `NothingRecorded(bool english)`.

MediatR non ha una pipeline di validazione per le richieste in streaming: la domanda si valida nel runner.

- [ ] **Step 1: Scrivi i test che falliscono**

`TESTS/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Turn/AssistantTurnTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using DomainChatMessage = Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.ChatMessage;
using ValidationException = Api.SharedKernel.Domain.Exceptions.ValidationException;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;

/// <summary>
/// Spec 2026-10-11 §6: un turno dell'assistente, dal comando agli eventi. Il ciclo è quello vero (la
/// catena M.E.AI di A0 con gli strumenti di A1); sono finti il gateway, la sessione e i repository.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4276")]
public sealed class AssistantTurnTests
{
    private static readonly Guid Session = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Owner = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid VersionId = Guid.Parse("00000000-0000-0000-0000-0000000000d1");
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Giulia = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private const string ReserveModel = "meta-llama/llama-3.3-70b-instruct:free";

    private readonly Mock<IAssistantSessionDirectory> _directory = new();
    private readonly Mock<ISessionAssistantReader> _reader = new();
    private readonly Mock<IPublishedAgentProfileReader> _profiles = new();
    private readonly Mock<IToolCallingModelCatalog> _catalog = new();
    private readonly Mock<IChatThreadRepository> _threads = new();
    private readonly Mock<IAssistantProposalRepository> _proposals = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILlmService> _gateway = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 20, 0, 0, TimeSpan.Zero));
    private readonly List<LlmChatRequest> _requests = [];
    private readonly List<AssistantProposal> _addedProposals = [];
    private ChatThread? _savedThread;

    public AssistantTurnTests()
    {
        Access("InProgress", accepts: true);
        _directory.Setup(d => d.CreateReader(Session, Owner)).Returns(_reader.Object);
        _reader.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(State());
        Publish(AgentProfileTests.Assistant());
        _catalog.Setup(c => c.GetToolCallingModelIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal) { "deepseek-flash", ReserveModel });
        _threads.Setup(t => t.FindByLiveSessionIdAsync(Session, It.IsAny<CancellationToken>())).ReturnsAsync((ChatThread?)null);
        _threads.Setup(t => t.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()))
            .Callback<ChatThread, CancellationToken>((thread, _) => _savedThread = thread).Returns(Task.CompletedTask);
        _threads.Setup(t => t.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()))
            .Callback<ChatThread, CancellationToken>((thread, _) => _savedThread = thread).Returns(Task.CompletedTask);
        _proposals.Setup(p => p.AddAsync(It.IsAny<AssistantProposal>(), It.IsAny<CancellationToken>()))
            .Callback<AssistantProposal, CancellationToken>((proposal, _) => _addedProposals.Add(proposal)).Returns(Task.CompletedTask);
        _proposals.Setup(p => p.ListByThreadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _proposals.Setup(p => p.ListOpenBySessionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AssistantSessionState State() => new(
        GameId: null,
        GameName: "Azul",
        Status: "InProgress",
        CanRecordScores: true,
        CanAdvanceTurn: true,
        Players: [new(Marco, "Marco", 7, true), new(Giulia, "Giulia", 12, true)],
        CurrentTurnPlayerId: Giulia,
        CurrentTurnIndex: 4,
        CurrentRound: 2,
        CurrentPhase: null,
        Dimensions: ["punti"],
        Scores: [new(Marco, 2, "punti", 7)],
        HouseRules: []);

    private void Access(string status, bool accepts) =>
        _directory.Setup(d => d.GetAccessAsync(Session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantSessionAccess(Owner, status, accepts));

    /// <summary>Il contenuto di prova ha «en» come lingua di riserva e deepseek-flash come modello.</summary>
    private void Publish(SessionAssistantSettings assistant) =>
        _profiles.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishedAgentProfile(VersionId, 3, AgentProfileTests.Content(model: "deepseek-flash", assistant: assistant)));

    private void Script(params LlmCompletionResult[] turns)
    {
        var queue = new Queue<LlmCompletionResult>(turns);
        _gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmChatRequest, CancellationToken>((request, _) => _requests.Add(request))
            .ReturnsAsync(() => queue.Count > 0 ? queue.Dequeue() : LlmCompletionResult.CreateFailure("script finished"));
    }

    private static LlmCompletionResult Calls(string tool, string argumentsJson) =>
        LlmCompletionResult.CreateSuccess(string.Empty) with { ToolCalls = [new LlmToolCall($"call_{Guid.NewGuid():N}", tool, argumentsJson)] };

    private static LlmCompletionResult Says(string text) => LlmCompletionResult.CreateSuccess(text);

    private static LlmCompletionResult Refuses(string errorCode) =>
        LlmCompletionResult.CreateFailure(errorCode) with
        {
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["error_code"] = errorCode },
        };

    private AskSessionAssistantCommandHandler Handler() => new(new AssistantTurnRunner(
        _directory.Object,
        _profiles.Object,
        _catalog.Object,
        _threads.Object,
        _proposals.Object,
        new AssistantChatClientFactory(_gateway.Object, NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider()),
        new ExactNameMatcher(),
        _unitOfWork.Object,
        _time,
        NullLogger<AssistantTurnRunner>.Instance));

    private async Task<List<AssistantTurnEvent>> AskAsync(string question = "Marco fa 5 punti", string? language = "it", Guid? user = null)
    {
        var events = new List<AssistantTurnEvent>();
        await foreach (var turnEvent in Handler().Handle(new AskSessionAssistantCommand(Session, user ?? Owner, question, language), Ct))
        {
            events.Add(turnEvent);
        }

        return events;
    }

    // ─── I controlli prima del primo evento ─────────────────────────────────────

    [Fact]
    public async Task SomeoneWhoIsNotTheOwner_IsForbidden_BeforeAnyModelCall()
    {
        var act = () => AskAsync(user: Guid.NewGuid());

        await act.Should().ThrowAsync<ForbiddenException>();
        _gateway.Verify(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ACompletedSession_IsAConflict_WithItsCode()
    {
        Access("Completed", accepts: false);

        var act = () => AskAsync();

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("session_state_invalid");
    }

    [Fact]
    public async Task ADisabledAssistant_IsAConflict_WithItsCode()
    {
        Publish(SessionAssistantSettings.Disabled);

        var act = () => AskAsync();

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("assistant_disabled");
    }

    /// <summary>
    /// La pubblicazione rifiuta questo profilo, ma una migrazione di dati scavalca l'aggregato, e il
    /// catalogo può cambiare dopo: il turno ricontrolla.
    /// </summary>
    [Fact]
    public async Task AModelWithoutToolCalling_IsAConflict_EvenIfTheProfileIsPublished()
    {
        _catalog.Setup(c => c.GetToolCallingModelIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal));

        var act = () => AskAsync();

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("assistant_model_without_tools");
        _gateway.Verify(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyQuestion_IsAValidationError(string question)
    {
        var act = () => AskAsync(question);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ATooLongQuestion_IsAValidationError()
    {
        var act = () => AskAsync(new string('a', AssistantTurnRunner.MaxQuestionLength + 1));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ALanguageTagLongerThanATag_IsAValidationError()
    {
        // La lingua arriva dal client e finisce in ogni messaggio salvato: non è un campo libero.
        var act = () => AskAsync(language: new string('x', AssistantTurnRunner.MaxLanguageLength + 1));

        await act.Should().ThrowAsync<ValidationException>();
    }

    // ─── Il turno riuscito ──────────────────────────────────────────────────────

    [Fact]
    public async Task AScoreRequest_SavesTheProposalAndTheMessages_ThenEmitsProposalAnswerDone()
    {
        Script(
            Calls("proponi_punteggio", """{"giocatore":"Marco","punti":5}"""),
            Says("Ho preparato la proposta: +5 a Marco. Confermala dalla card."));

        var events = await AskAsync();

        events.Select(e => e.Type).Should().Equal("status", "status", "proposal", "answer", "done");
        events[0].Tool.Should().BeNull();
        events[1].Tool.Should().Be("proponi_punteggio");

        var proposal = _addedProposals.Should().ContainSingle().Subject;
        proposal.Payload.Should().Be(new AssistantProposalPayload(Marco, "Marco", "punti", 2, 5));
        proposal.Expected.CellValue.Should().Be(7);
        proposal.LiveSessionId.Should().Be(Session);
        proposal.ProfileVersionId.Should().Be(VersionId);

        _savedThread.Should().NotBeNull();
        _savedThread!.LiveSessionId.Should().Be(Session);
        _savedThread.GameId.Should().BeNull();
        _savedThread.UserId.Should().Be(Owner);
        _savedThread.AgentType.Should().Be("session-assistant");
        _savedThread.Messages.Select(m => m.Role).Should().Equal("user", "assistant");
        _savedThread.Messages[0].Content.Should().Be("Marco fa 5 punti");
        _savedThread.Messages[1].Id.Should().Be(proposal.MessageId, "la proposta punta al messaggio che l'ha prodotta");
        _savedThread.Messages[1].ProfileVersionId.Should().Be(VersionId);
        _savedThread.Messages[1].Language.Should().Be("it");
        proposal.ChatThreadId.Should().Be(_savedThread.Id);

        events[2].Proposal!.Id.Should().Be(proposal.Id);
        events[2].Proposal!.ResultingValue.Should().Be(12);
        events[3].Text.Should().Be("Ho preparato la proposta: +5 a Marco. Confermala dalla card.");
        events[4].ResponseId.Should().Be(proposal.MessageId);
        events[4].ProfileVersionId.Should().Be(VersionId);
        events[4].ProfileVersionNumber.Should().Be(3);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _threads.Verify(t => t.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Campo → effetto: ogni campo della sezione del profilo arriva al gateway.</summary>
    [Fact]
    public async Task TheTurn_TakesModelPromptAndLimitsFromTheAssistantSection()
    {
        Script(Says("Tocca a Giulia."));

        await AskAsync("a chi tocca?");

        var request = _requests.Should().ContainSingle().Subject;
        request.PrimaryModelId.Should().Be("deepseek-flash");
        request.FallbackModelId.Should().BeNull("la sezione non usa la riserva");
        request.Temperature.Should().Be(0.2);
        request.MaxTokens.Should().Be(600);
        request.ProfileVersionId.Should().Be(VersionId);
        request.Source.Should().Be(RequestSource.SessionAssistant);
        request.Tools.Select(t => t.Name).Should().Equal("stato_partita", "proponi_punteggio", "proponi_turno");
        request.Messages[0].Content.Should().Be("Sei l'assistente al tavolo.");
        request.Messages[1].Content.Should().StartWith("Gioco: Azul");
        request.Messages[^1].Content.Should().Be("a chi tocca?");
    }

    [Fact]
    public async Task TheReserveModel_IsUsedOnlyWhenTheSectionSaysSo()
    {
        Publish(AgentProfileTests.Assistant(useReserveModel: true));
        Script(Says("Tocca a Giulia."));

        await AskAsync("a chi tocca?");

        _requests[0].FallbackModelId.Should().Be(ReserveModel);
    }

    [Theory]
    [InlineData("en-US", "You are the table assistant.", "Game: Azul")]
    [InlineData(null, "You are the table assistant.", "Game: Azul")]
    [InlineData("de", "You are the table assistant.", "Gioco: Azul")]
    public async Task TheLanguage_PicksThePrompt_AndFallsBackToTheProfileLanguage(string? language, string prompt, string context)
    {
        // «de» non ha un prompt: vale quello della lingua di riserva del profilo («en»). Il contesto
        // statico conosce solo italiano e inglese, e per ogni altra lingua resta in italiano.
        Script(Says("ok"));

        await AskAsync("who's next?", language);

        _requests[0].Messages[0].Content.Should().Be(prompt);
        _requests[0].Messages[1].Content.Should().StartWith(context);
    }

    [Fact]
    public async Task TheIterationsOfTheSection_BoundTheLoop_AndAnEmptyAnswerIsSaidExplicitly()
    {
        var oneRound = SessionAssistantSettings.Create(
            true,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["en"] = "You are the table assistant." },
            maxToolIterations: 1);
        Publish(oneRound);
        _gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmChatRequest, CancellationToken>((request, _) => _requests.Add(request))
            .ReturnsAsync(() => Calls("stato_partita", "{}"));

        var events = await AskAsync("a chi tocca?");

        _requests.Should().HaveCount(2, "un giro con strumenti, poi la chiamata finale senza");
        _requests[1].Tools.Should().BeEmpty();
        events.Select(e => e.Type).Where(type => type != "status").Should().Equal("answer", "done");
        events.Single(e => e.Type == "answer").Text.Should().Be(AssistantTurnTexts.NoAnswer(english: false, hasProposals: false));
    }

    [Fact]
    public async Task AClaimedActionWithoutAProposal_GetsTheWarning()
    {
        Script(Says("Ho segnato 5 punti a Marco."));

        var events = await AskAsync();

        events.Single(e => e.Type == "answer").Text.Should()
            .Be("Ho segnato 5 punti a Marco." + AssistantTurnTexts.NothingRecorded(english: false));
        _savedThread!.Messages[1].Content.Should().Contain("non è stato registrato nulla");
        _addedProposals.Should().BeEmpty();
    }

    [Fact]
    public async Task TheSecondTurn_ExpiresThePendingProposals_AndTellsTheModelHowTheEarlierOnesEnded()
    {
        var thread = new ChatThread(Guid.NewGuid(), Owner, gameId: null, title: null, agentId: null,
            agentType: "session-assistant", liveSessionId: Session);
        thread.AddUserMessage("Marco fa 5 punti");
        var earlierAnswer = Guid.NewGuid();
        thread.AddMessage(new DomainChatMessage("Ho preparato la proposta.", DomainChatMessage.AssistantRole, 1, id: earlierAnswer));
        var earlier = AssistantProposal.ForScore(
            Session, thread.Id, earlierAnswer, VersionId, Marco, "Marco", "punti", 2, 5, 7, _time.GetUtcNow().UtcDateTime);
        earlier.Expire(_time.GetUtcNow().UtcDateTime);
        _threads.Setup(t => t.FindByLiveSessionIdAsync(Session, It.IsAny<CancellationToken>())).ReturnsAsync(thread);
        _proposals.Setup(p => p.ListByThreadAsync(thread.Id, It.IsAny<CancellationToken>())).ReturnsAsync([earlier]);
        Script(Says("Tocca a Giulia."));

        await AskAsync("a chi tocca?");

        _proposals.Verify(p => p.ExpirePendingBySessionAsync(Session, _time.GetUtcNow().UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
        var sent = _requests[0].Messages;
        sent.Select(m => m.Role).Should().Equal(
            LlmChatRoles.System, LlmChatRoles.System, LlmChatRoles.User, LlmChatRoles.Assistant, LlmChatRoles.User);
        sent[3].Content.Should().Be("Ho preparato la proposta.\n[+5 punti a Marco, round 2: scaduta senza conferma]");
        _threads.Verify(t => t.UpdateAsync(thread, It.IsAny<CancellationToken>()), Times.Once);
        _threads.Verify(t => t.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
        thread.Messages.Should().HaveCount(4);
    }

    // ─── Il turno che fallisce a metà (Review Focus 5) ──────────────────────────

    [Fact]
    public async Task ABudgetRefusalMidLoop_KeepsTheProposals_AndEmitsBudgetPaused()
    {
        Script(
            Calls("proponi_punteggio", """{"giocatore":"Marco","punti":5}"""),
            Refuses(LlmBudgetAdmission.DailyBudgetErrorCode));

        var events = await AskAsync();

        events.Select(e => e.Type).Should().Equal("status", "status", "proposal", "answer", "error");
        events[^1].Code.Should().Be("budget_paused");
        events[3].Text.Should().Be(AssistantTurnTexts.ForError("budget_paused", english: false, hasProposals: true));
        events[3].Text.Should().Contain("restano valide", "con una card davanti, il testo non manda l'host a segnare a mano");
        _addedProposals.Should().ContainSingle("la proposta creata prima del rifiuto resta confermabile");
        _savedThread!.Messages.Should().HaveCount(2);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AProviderFailure_EmitsAssistantUnavailable()
    {
        Script(Refuses(LlmAnswerErrorCodes.ProviderUnavailable));

        var events = await AskAsync();

        events.Select(e => e.Type).Should().Equal("status", "answer", "error");
        events[^1].Code.Should().Be("assistant_unavailable");
        events[1].Text.Should().Be(AssistantTurnTexts.ForError("assistant_unavailable", english: false, hasProposals: false));
    }

    /// <summary>
    /// La scadenza è virtuale (<see cref="TimeProvider"/>). La sezione chiede 120 secondi e il test ne
    /// aspetta al più 30 veri: se la scadenza girasse sull'orologio di sistema, il test fallirebbe per
    /// attesa scaduta invece di passare dopo aver dormito. E prima di far avanzare il tempo finto il
    /// turno deve essere ancora in corso.
    /// </summary>
    [Fact]
    public async Task ATurnThatTakesTooLong_EmitsTurnTimeout_AndStillSavesTheConversation()
    {
        Publish(SessionAssistantSettings.Create(
            true,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["en"] = "You are the table assistant." },
            turnTimeoutSeconds: 120));
        var called = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns<LlmChatRequest, CancellationToken>(async (_, token) =>
            {
                called.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return Says("mai");
            });

        var asking = AskAsync();
        await called.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        asking.IsCompleted.Should().BeFalse("il gateway non ha risposto e il tempo non è ancora passato");

        _time.Advance(TimeSpan.FromSeconds(121));
        var events = await asking.WaitAsync(TimeSpan.FromSeconds(30), Ct);

        events.Select(e => e.Type).Should().Equal("status", "answer", "error");
        events[^1].Code.Should().Be("turn_timeout");
        _savedThread!.Messages.Should().HaveCount(2);
    }

    /// <summary>
    /// Lo «status» serve mentre il ciclo gira: un turno che trattenesse tutti gli eventi fino alla fine
    /// passerebbe ogni altro test di questa classe.
    /// </summary>
    [Fact]
    public async Task TheStatusOfATool_ReachesTheHost_WhileTheLoopIsStillRunning()
    {
        var release = new TaskCompletionSource<LlmCompletionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        _gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns<LlmChatRequest, CancellationToken>((_, _) =>
                Interlocked.Increment(ref calls) == 1 ? Task.FromResult(Calls("stato_partita", "{}")) : release.Task);

        await using var events = Handler()
            .Handle(new AskSessionAssistantCommand(Session, Owner, "a chi tocca?", "it"), Ct)
            .GetAsyncEnumerator(Ct);

        (await events.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), Ct)).Should().BeTrue();
        events.Current.Type.Should().Be("status");
        (await events.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), Ct)).Should().BeTrue();
        events.Current.Tool.Should().Be("stato_partita");
        release.Task.IsCompleted.Should().BeFalse("il modello non ha ancora risposto: la seconda chiamata è ferma");

        release.SetResult(Says("Tocca a Giulia."));
        var rest = new List<string>();
        while (await events.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), Ct))
        {
            rest.Add(events.Current.Type);
        }

        rest.Should().Equal("answer", "done");
    }

    [Fact]
    public async Task AnUnexpectedFailure_EmitsAssistantUnavailable_AndTheStreamStillEnds()
    {
        _reader.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));

        var events = await AskAsync();

        events.Select(e => e.Type).Should().Equal("status", "error");
        events[^1].Code.Should().Be("assistant_unavailable");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Scostamento S6: le card partono solo dopo il salvataggio. Se il salvataggio fallisce, l'host non
    /// deve aver ricevuto una card per una proposta che nel database non esiste.
    /// </summary>
    [Fact]
    public async Task WhenTheSaveFails_NoCardReachesTheHost()
    {
        Script(
            Calls("proponi_punteggio", """{"giocatore":"Marco","punti":5}"""),
            Says("Ho preparato la proposta."));
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the database is down"));

        var events = await AskAsync();

        _addedProposals.Should().ContainSingle("lo strumento ha girato");
        events.Select(e => e.Type).Should().Equal("status", "status", "error");
        events.Should().NotContain(e => e.Type == "proposal");
        events[^1].Code.Should().Be("assistant_unavailable");
    }

    [Fact]
    public void TheMeter_TellsItsObserverWhichToolWasCalled()
    {
        var seen = new List<string>();
        var meter = new ChatTurnMeter { ToolCalled = seen.Add };

        meter.RecordToolCall("stato_partita");
        meter.RecordToolCall("proponi_turno");

        seen.Should().Equal("stato_partita", "proponi_turno");
        meter.ToolCalls.Should().Equal("stato_partita", "proponi_turno");
    }

    private sealed class ExactNameMatcher : IAssistantPlayerMatcher
    {
        public AssistantPlayerMatch Match(string name, IReadOnlyList<AssistantPlayer> players)
        {
            var player = players.FirstOrDefault(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
            return new AssistantPlayerMatch(player?.Id, player?.Name, IsAmbiguous: false, []);
        }
    }
}
```

`RecordToolCall` è `internal`: il progetto di test lo vede già (gli altri test dell'assistente usano tipi interni).

- [ ] **Step 2: Verifica che non compili**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AssistantTurnTests"`
Expected: `CS0246` su `AskSessionAssistantCommandHandler`, `AssistantTurnRunner`, `AssistantTurnEvent`, `AssistantTurnTexts`; `CS0117` su `ToolCalled`.

- [ ] **Step 3: L'osservatore del meter, l'evento e i testi**

In `ChatTurnMeter.cs`, dopo la proprietà `ToolCalls`:

```csharp
    /// <summary>
    /// Chiamato a ogni strumento che parte, con il suo nome: è da qui che il turno emette l'evento
    /// <c>status</c> mentre il ciclo è ancora in corso (spec §6.2).
    /// </summary>
    public Action<string>? ToolCalled { get; init; }
```

e `RecordToolCall` diventa:

```csharp
    internal void RecordToolCall(string name)
    {
        lock (_lock)
        {
            Volatile.Write(ref _toolCalls, _toolCalls.Add(name));
        }

        // Fuori dal lock: chi osserva non deve poter bloccare il contatore.
        ToolCalled?.Invoke(name);
    }
```

`KB/Application/SessionAssistant/Turn/AssistantTurnEvent.cs`:

```csharp
#pragma warning disable MA0048 // File name must match type name - the turn event and the fallback texts
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;

/// <summary>
/// Un evento del turno (spec §6.2), scritto come <c>data:</c> senza nome con un campo <c>type</c>, nella
/// convenzione degli endpoint SSE esistenti. Un solo record con campi opzionali: ogni tipo ne usa alcuni.
/// </summary>
/// <remarks>
/// <c>Type</c> è <c>status</c>, <c>proposal</c>, <c>answer</c>, <c>done</c> o <c>error</c>. <c>Tool</c> vale
/// per <c>status</c>: lo strumento che sta girando, assente per l'avvio del turno. <c>Proposal</c> per
/// <c>proposal</c>, <c>Text</c> per <c>answer</c>. <c>ResponseId</c> (l'id del messaggio dell'assistente) e la
/// versione del profilo per <c>done</c>. <c>Code</c> per <c>error</c>: <c>budget_paused</c>,
/// <c>turn_timeout</c> o <c>assistant_unavailable</c>.
/// </remarks>
internal sealed record AssistantTurnEvent(
    string Type,
    string? Tool = null,
    AssistantProposalDto? Proposal = null,
    string? Text = null,
    Guid? ResponseId = null,
    Guid? ProfileVersionId = null,
    int? ProfileVersionNumber = null,
    string? Code = null)
{
    public const string StatusType = "status";
    public const string ProposalType = "proposal";
    public const string AnswerType = "answer";
    public const string DoneType = "done";
    public const string ErrorType = "error";

    public static AssistantTurnEvent Status(string? tool = null) => new(StatusType, Tool: tool);

    public static AssistantTurnEvent ForProposal(AssistantProposalDto proposal) => new(ProposalType, Proposal: proposal);

    public static AssistantTurnEvent Answer(string text) => new(AnswerType, Text: text);

    public static AssistantTurnEvent Done(Guid responseId, Guid profileVersionId, int profileVersionNumber) =>
        new(DoneType, ResponseId: responseId, ProfileVersionId: profileVersionId, ProfileVersionNumber: profileVersionNumber);

    public static AssistantTurnEvent Error(string code) => new(ErrorType, Code: code);
}

/// <summary>
/// I testi che il turno scrive al posto del modello (spec §6.3): quando il modello non ha risposto, e
/// l'avviso quando dice di aver fatto qualcosa che non ha fatto. Finiscono nella conversazione salvata,
/// quindi sono nella lingua della sessione.
/// </summary>
internal static class AssistantTurnTexts
{
    public static string ForError(string code, bool english, bool hasProposals)
    {
        var what = code switch
        {
            AssistantTurnRunner.BudgetPausedCode => english
                ? "The assistant is paused: the spending cap has been reached."
                : "L'assistente è in pausa: il tetto di spesa è stato raggiunto.",
            AssistantTurnRunner.TimeoutCode => english
                ? "I could not answer in time."
                : "Non ho fatto in tempo a rispondere.",
            _ => english
                ? "The assistant is not available right now."
                : "L'assistente non è disponibile in questo momento.",
        };
        var retryMakesSense = !string.Equals(code, AssistantTurnRunner.BudgetPausedCode, StringComparison.Ordinal);
        return $"{what} {WhatNow(english, hasProposals, retryMakesSense)}";
    }

    public static string NoAnswer(bool english, bool hasProposals) =>
        (english ? "I could not complete the answer. " : "Non sono riuscito a completare la risposta. ")
        + WhatNow(english, hasProposals, retryMakesSense: true);

    public static string NothingRecorded(bool english) => english
        ? "\n\nNote: nothing has been recorded and there is no proposal to confirm. Ask again, or record it by hand."
        : "\n\nAttenzione: non è stato registrato nulla e non c'è nessuna proposta da confermare. Ripeti la richiesta, o segna a mano.";

    /// <summary>
    /// Con una card davanti, mandare l'host a segnare a mano gli farebbe scrivere due volte: le proposte
    /// già create restano valide (spec §6.3).
    /// </summary>
    private static string WhatNow(bool english, bool hasProposals, bool retryMakesSense)
    {
        if (hasProposals)
        {
            return english
                ? "The proposals already prepared are still valid: confirm them from their cards."
                : "Le proposte già preparate restano valide: confermale dalle card.";
        }

        if (retryMakesSense)
        {
            return english ? "Try again, or record it by hand." : "Riprova, o segna a mano.";
        }

        return english
            ? "You can still record scores and turns by hand."
            : "Puoi comunque segnare a mano punteggi e turni.";
    }
}
```

- [ ] **Step 4: Il comando, l'handler e il runner**

`KB/Application/SessionAssistant/Turn/AskSessionAssistantCommand.cs`:

```csharp
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - the streaming command and its handler
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;

/// <summary>
/// Spec 2026-10-11 §6.2: una domanda dell'host all'assistente in partita. È in streaming perché il
/// client vede cosa sta facendo l'assistente mentre il ciclo gira; la risposta resta bufferizzata (§6.1).
/// </summary>
internal sealed record AskSessionAssistantCommand(Guid LiveSessionId, Guid UserId, string Question, string? Language)
    : IStreamingQuery<AssistantTurnEvent>;

internal sealed class AskSessionAssistantCommandHandler(AssistantTurnRunner runner)
    : IStreamingQueryHandler<AskSessionAssistantCommand, AssistantTurnEvent>
{
    public async IAsyncEnumerable<AssistantTurnEvent> Handle(
        AskSessionAssistantCommand command, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // I controlli vengono prima del primo evento: se falliscono, chi consuma lo stream riceve
        // l'eccezione al primo MoveNextAsync, e l'endpoint risponde con uno stato HTTP invece che con SSE.
        var turn = await runner.PrepareAsync(command, cancellationToken).ConfigureAwait(false);
        yield return AssistantTurnEvent.Status();

        // Il turno scrive i suoi eventi in un canale mentre gira: così uno «status» arriva al client
        // durante il ciclo, e non serve un yield dentro un try/catch (che C# non permette).
        var channel = Channel.CreateUnbounded<AssistantTurnEvent>(new UnboundedChannelOptions { SingleReader = true });
        var running = runner.RunAsync(turn, channel.Writer, cancellationToken);
        try
        {
            await foreach (var turnEvent in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return turnEvent;
            }
        }
        finally
        {
            // RunAsync non lancia se non per annullamento: qui si aspetta solo che abbia finito.
            await running.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }
}
```

`KB/Application/SessionAssistant/Turn/AssistantTurnRunner.cs`:

```csharp
using System.Threading.Channels;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.AI;
using DomainChatMessage = Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.ChatMessage;
using ValidationException = Api.SharedKernel.Domain.Exceptions.ValidationException;

#pragma warning disable MA0048 // File name must match type name - the runner and the turn it prepares
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;

/// <summary>Un turno che ha superato i controlli: chi chiede, con quale versione del profilo, in che lingua.</summary>
internal sealed record PreparedAssistantTurn(
    AskSessionAssistantCommand Command, PublishedAgentProfile Profile, string Language, string SystemPrompt);

/// <summary>
/// Un turno dell'assistente in partita (spec 2026-10-11 §6.2): controlli, ciclo con gli strumenti,
/// salvataggio di messaggi e proposte, eventi. Non scrive mai la partita: gli strumenti ricevono un
/// lettore e un sink di proposte, e questa classe non ha né il mediator né la porta di scrittura.
/// </summary>
internal sealed class AssistantTurnRunner
{
    public const int MaxQuestionLength = 2000;

    /// <summary>Un tag di lingua («it», «en-US»): arriva dal client e finisce in ogni messaggio salvato.</summary>
    public const int MaxLanguageLength = 16;

    /// <summary>Sotto il limite di un messaggio del thread (10.000), con il margine per l'avviso in coda.</summary>
    public const int MaxAnswerLength = 9000;

    /// <summary>Quanti messaggi passati tornano al modello: sei scambi.</summary>
    public const int HistoryMessages = 12;

    public const string ThreadAgentType = "session-assistant";
    public const string DisabledCode = "assistant_disabled";
    public const string SessionStateInvalidCode = "session_state_invalid";
    public const string BudgetPausedCode = "budget_paused";
    public const string UnavailableCode = "assistant_unavailable";
    public const string TimeoutCode = "turn_timeout";

    private readonly IAssistantSessionDirectory _directory;
    private readonly IPublishedAgentProfileReader _profiles;
    private readonly IToolCallingModelCatalog _catalog;
    private readonly IChatThreadRepository _threads;
    private readonly IAssistantProposalRepository _proposals;
    private readonly IAssistantChatClientFactory _chatClients;
    private readonly IAssistantPlayerMatcher _matcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AssistantTurnRunner> _logger;

    public AssistantTurnRunner(
        IAssistantSessionDirectory directory,
        IPublishedAgentProfileReader profiles,
        IToolCallingModelCatalog catalog,
        IChatThreadRepository threads,
        IAssistantProposalRepository proposals,
        IAssistantChatClientFactory chatClients,
        IAssistantPlayerMatcher matcher,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<AssistantTurnRunner> logger)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _threads = threads ?? throw new ArgumentNullException(nameof(threads));
        _proposals = proposals ?? throw new ArgumentNullException(nameof(proposals));
        _chatClients = chatClients ?? throw new ArgumentNullException(nameof(chatClients));
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// I controlli di §6.2, passo 1. Lancia le eccezioni che il middleware traduce in stato HTTP: nessun
    /// evento è ancora partito.
    /// </summary>
    public async Task<PreparedAssistantTurn> PrepareAsync(AskSessionAssistantCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.Question) || command.Question.Length > MaxQuestionLength)
        {
            throw new ValidationException(
                nameof(command.Question), $"The question is required, up to {MaxQuestionLength} characters.");
        }

        if (command.Language is { Length: > MaxLanguageLength })
        {
            throw new ValidationException(
                nameof(command.Language), $"The language tag is at most {MaxLanguageLength} characters.");
        }

        var access = await _directory.GetAccessAsync(command.LiveSessionId, cancellationToken).ConfigureAwait(false);
        if (access.OwnerUserId != command.UserId)
        {
            throw new ForbiddenException("Only the session host can talk to the session assistant.");
        }

        if (!access.AcceptsQuestions)
        {
            throw new ConflictException(
                SessionStateInvalidCode,
                $"The session is {access.Status}: the assistant no longer answers once the game is over.");
        }

        var profile = await _profiles.GetAsync(cancellationToken).ConfigureAwait(false);
        var content = profile.Content;
        var assistant = content.SessionAssistant;
        var language = string.IsNullOrWhiteSpace(command.Language) ? content.FallbackLanguage : command.Language.Trim();
        var prompt = assistant.PromptFor(language) ?? assistant.PromptFor(content.FallbackLanguage);
        if (!assistant.Enabled || prompt is null)
        {
            throw new ConflictException(DisabledCode, "The session assistant is not enabled in the published agent profile.");
        }

        // La pubblicazione applica la stessa regola, ma una migrazione di dati la scavalca e il catalogo
        // può cambiare dopo: si ricontrolla a ogni turno.
        var toolCallingModels = await _catalog.GetToolCallingModelIdsAsync(cancellationToken).ConfigureAwait(false);
        if (content.SessionAssistantModelProblem(toolCallingModels) is { } problem)
        {
            throw new ConflictException(AgentProfile.AssistantModelWithoutToolsCode, problem);
        }

        return new PreparedAssistantTurn(command, profile, language, prompt);
    }

    /// <summary>
    /// Esegue il turno e scrive i suoi eventi. Non lancia, se non per annullamento: un errore imprevisto
    /// diventa un evento <c>error</c>, e il canale viene sempre chiuso.
    /// </summary>
    public async Task RunAsync(PreparedAssistantTurn turn, ChannelWriter<AssistantTurnEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(events);
        try
        {
            await ExecuteAsync(turn, events, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // SERVICE BOUNDARY: lo stream è già partito, quindi un'eccezione qui troncherebbe la connessione
        // senza dire nulla all'host. Diventa un evento con un codice; la causa resta nei log. Passa solo
        // l'annullamento della richiesta: un OperationCanceledException nato altrove è un errore come gli
        // altri, e lasciarlo uscire chiuderebbe lo stream in silenzio.
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Session assistant: the turn failed for live session {LiveSessionId}", turn.Command.LiveSessionId);
            events.TryWrite(AssistantTurnEvent.Error(UnavailableCode));
        }
#pragma warning restore CA1031
        finally
        {
            events.TryComplete();
        }
    }

    private async Task ExecuteAsync(PreparedAssistantTurn turn, ChannelWriter<AssistantTurnEvent> events, CancellationToken cancellationToken)
    {
        var command = turn.Command;
        var content = turn.Profile.Content;
        var assistant = content.SessionAssistant;

        var thread = await _threads.FindByLiveSessionIdAsync(command.LiveSessionId, cancellationToken).ConfigureAwait(false);
        var isNewThread = thread is null;
        thread ??= new ChatThread(
            Guid.NewGuid(), command.UserId, gameId: null, title: null, agentId: null, agentType: ThreadAgentType,
            liveSessionId: command.LiveSessionId);

        await ExpireEarlierProposalsAsync(command.LiveSessionId, cancellationToken).ConfigureAwait(false);
        var history = await BuildHistoryAsync(thread, cancellationToken).ConfigureAwait(false);

        var reader = _directory.CreateReader(command.LiveSessionId, command.UserId);
        var state = await reader.GetStateAsync(cancellationToken).ConfigureAwait(false);

        // L'id del messaggio dell'assistente nasce prima del ciclo: le proposte lo citano.
        var messageId = Guid.NewGuid();
        var sink = new RepositoryProposalSink(
            _proposals, command.LiveSessionId, thread.Id, messageId, turn.Profile.VersionId, _timeProvider);
        var toolset = new SessionAssistantToolset(reader, sink, _matcher);
        var meter = new ChatTurnMeter { ToolCalled = tool => events.TryWrite(AssistantTurnEvent.Status(tool)) };
        var settings = new AssistantModelSettings(
            content.PrimaryModelId,
            assistant.UseReserveModel ? content.FallbackModelId : null,
            (double)assistant.Temperature,
            assistant.MaxResponseTokens,
            assistant.MaxToolIterations,
            turn.Profile.VersionId,
            RequestSource.SessionAssistant);
        var messages = AssistantConversation.Build(
            new AssistantPromptSet(turn.SystemPrompt, turn.SystemPrompt), turn.Language, state, history, command.Question);

        var outcome = await AskModelAsync(settings, meter, messages, toolset, assistant.TurnTimeoutSeconds, cancellationToken)
            .ConfigureAwait(false);
        var text = FinalText(outcome, sink.Created.Count, turn.Language);

        thread.AddUserMessage(command.Question);
        var answer = new DomainChatMessage(text, DomainChatMessage.AssistantRole, thread.Messages.Count, id: messageId);
        answer.SetAnswerProvenance(turn.Profile.VersionId, turn.Language);
        thread.AddMessage(answer);
        if (isNewThread)
        {
            await _threads.AddAsync(thread, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _threads.UpdateAsync(thread, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Solo adesso le card: una proposta mostrata prima del salvataggio si potrebbe confermare quando
        // nel database non c'è ancora.
        foreach (var proposal in sink.Created)
        {
            events.TryWrite(AssistantTurnEvent.ForProposal(AssistantProposalDto.From(proposal)));
        }

        events.TryWrite(AssistantTurnEvent.Answer(text));
        events.TryWrite(outcome.ErrorCode is null
            ? AssistantTurnEvent.Done(messageId, turn.Profile.VersionId, turn.Profile.VersionNumber)
            : AssistantTurnEvent.Error(outcome.ErrorCode));
    }

    /// <summary>
    /// Il ciclo, con la scadenza del turno. Tetto, provider e scadenza non sono eccezioni per chi chiama:
    /// sono un esito con un codice, perché le proposte già create vanno comunque salvate (spec §6.3).
    /// </summary>
    private async Task<TurnOutcome> AskModelAsync(
        AssistantModelSettings settings,
        ChatTurnMeter meter,
        List<ChatMessage> messages,
        SessionAssistantToolset toolset,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        // La scadenza passa dal TimeProvider: nei test è virtuale, e non gareggia con il carico della suite.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds), _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            using var chat = _chatClients.Create(settings, meter);
            // cerca_regole arriva con la fetta A5: fino ad allora non è offerto.
            var response = await chat.GetResponseAsync(
                messages, new ChatOptions { Tools = toolset.CreateTools(includeRules: false) }, linked.Token)
                .ConfigureAwait(false);

            // L'ultimo messaggio dell'assistente, non la concatenazione: i giri intermedi possono portare
            // un testo accanto alle chiamate a strumenti.
            var last = response.Messages.LastOrDefault(m => m.Role == ChatRole.Assistant);
            return new TurnOutcome(last?.Text, ErrorCode: null);
        }
        catch (AssistantGatewayException ex)
        {
            _logger.LogWarning(ex, "Session assistant: the gateway stopped the turn with {ErrorCode}", ex.ErrorCode);
            var paused = string.Equals(ex.ErrorCode, LlmBudgetAdmission.DailyBudgetErrorCode, StringComparison.Ordinal)
                || string.Equals(ex.ErrorCode, LlmBudgetAdmission.MonthlyBudgetErrorCode, StringComparison.Ordinal);
            return new TurnOutcome(null, paused ? BudgetPausedCode : UnavailableCode);
        }
        catch (OperationCanceledException ex) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Session assistant: the turn exceeded its {TimeoutSeconds}s deadline", timeoutSeconds);
            return new TurnOutcome(null, TimeoutCode);
        }
    }

    private static string FinalText(TurnOutcome outcome, int proposals, string language)
    {
        var english = AssistantLanguage.IsEnglish(language);
        if (outcome.ErrorCode is not null)
        {
            return AssistantTurnTexts.ForError(outcome.ErrorCode, english, hasProposals: proposals > 0);
        }

        if (string.IsNullOrWhiteSpace(outcome.Text))
        {
            return AssistantTurnTexts.NoAnswer(english, hasProposals: proposals > 0);
        }

        var text = outcome.Text.Trim();
        if (text.Length > MaxAnswerLength)
        {
            text = text[..MaxAnswerLength];
        }

        // Spec §6.3: il modello dice di aver registrato, o di aver preparato una proposta, e nel turno non
        // ce n'è nessuna. L'host cercherebbe un punteggio o una card che non esistono.
        var claimed = proposals == 0
            && (ClaimedActionDetector.ClaimsAction(text) || ClaimedActionDetector.ClaimsProposal(text));
        return claimed ? text + AssistantTurnTexts.NothingRecorded(english) : text;
    }

    /// <summary>
    /// Spec §5.2: una proposta ancora <c>Pending</c> scade quando inizia il turno successivo. Una ancora
    /// <c>Applying</c> a questo punto è una conferma rimasta a metà (§5.3): si segnala nei log.
    /// </summary>
    private async Task ExpireEarlierProposalsAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await _proposals.ExpirePendingBySessionAsync(liveSessionId, now, cancellationToken).ConfigureAwait(false);

        var stillOpen = await _proposals.ListOpenBySessionAsync(liveSessionId, cancellationToken).ConfigureAwait(false);
        foreach (var proposal in stillOpen.Where(p => p.Status == AssistantProposalStatus.Applying))
        {
            _logger.LogWarning(
                "Session assistant: proposal {ProposalId} of live session {LiveSessionId} is Applying at the start of a new turn: a confirmation is in flight, or one did not complete",
                proposal.Id, liveSessionId);
        }
    }

    /// <summary>Dei turni passati tornano al modello solo il testo e gli esiti delle proposte (spec §6.2).</summary>
    private async Task<IReadOnlyList<AssistantHistoryTurn>> BuildHistoryAsync(ChatThread thread, CancellationToken cancellationToken)
    {
        if (thread.Messages.Count == 0)
        {
            return [];
        }

        var proposals = await _proposals.ListByThreadAsync(thread.Id, cancellationToken).ConfigureAwait(false);
        var byMessage = proposals.ToLookup(p => p.MessageId);
        return thread.Messages
            .Where(m => !m.IsDeleted)
            .TakeLast(HistoryMessages)
            .Select(m => new AssistantHistoryTurn(m.Role, WithOutcomes(m.Content, byMessage[m.Id])))
            .ToList();
    }

    private static string WithOutcomes(string text, IEnumerable<AssistantProposal> proposals)
    {
        var lines = proposals
            .Select(p => $"[{AssistantProposalSummary.For(p)}: {AssistantProposalSummary.Outcome(p.Status)}]")
            .ToList();
        return lines.Count == 0 ? text : $"{text}\n{string.Join('\n', lines)}";
    }

    private sealed record TurnOutcome(string? Text, string? ErrorCode);
}
```

In `SessionAssistantServiceExtensions.cs` (con `using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;`):

```csharp
        services.AddScoped<AssistantTurnRunner>();
```

In `SessionAssistantRegistrationTests.cs`, accanto agli altri doppi (il runner chiede anche il lettore del profilo, i due repository, la unit of work e l'orologio):

```csharp
        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.KnowledgeBase.Application.Services.Answering.IPublishedAgentProfileReader>());
        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.KnowledgeBase.Domain.Repositories.IChatThreadRepository>());
        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.KnowledgeBase.Domain.Repositories.IAssistantProposalRepository>());
        services.AddScoped(_ => Mock.Of<Api.SharedKernel.Infrastructure.Persistence.IUnitOfWork>());
        services.AddSingleton(TimeProvider.System);
```

e, in fondo al test:

```csharp
        scope.ServiceProvider.GetRequiredService<Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn.AssistantTurnRunner>()
            .Should().NotBeNull();
```

- [ ] **Step 5: Verifica che i test passino**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AssistantTurnTests|FullyQualifiedName~SessionAssistantRegistrationTests"`
Expected: PASS. Annota il numero. Nessun test deve durare secondi.

- [ ] **Step 6: Verifica per perturbazione**

Quattro perturbazioni di `AssistantTurnRunner.cs`, una alla volta, sempre con copia del file fuori dal repository, ripristino per copia e `touch`:

1. In `ExecuteAsync` sposta il ciclo `foreach (var proposal in sink.Created)` **prima** di `SaveChangesAsync`. Expected: `WhenTheSaveFails_NoCardReachesTheHost` FAIL (la card è partita prima del salvataggio fallito).
2. In `FinalText` togli `proposals == 0 &&`. Expected: `AScoreRequest_SavesTheProposalAndTheMessages_ThenEmitsProposalAnswerDone` FAIL (l'avviso compare anche con una proposta vera).
3. In `ExecuteAsync` sostituisci `assistant.UseReserveModel ? content.FallbackModelId : null` con `content.FallbackModelId`. Expected: `TheTurn_TakesModelPromptAndLimitsFromTheAssistantSection` FAIL (la riserva arriva al gateway anche quando la sezione la spegne).
4. In `AskModelAsync` togli `, _timeProvider` dal costruttore del `CancellationTokenSource`: la scadenza torna sull'orologio di sistema. Expected: `ATurnThatTakesTooLong_EmitsTurnTimeout_AndStillSavesTheConversation` FAIL per attesa scaduta dopo 30 secondi veri (il tempo finto non la fa più scattare).

- [ ] **Step 7: Commit**

```bash
git add apps/api
git commit -m "feat(ai): il turno dell'assistente in partita (#4276)"
```

### Task 10: Endpoint SSE, test di architettura e turno sul database vero

**Files:**
- Create: `API/Routing/SessionAssistantEndpoints.cs`
- Modify: `API/Program.cs` (accanto a `v1Api.MapLiveSessionEndpoints();`)
- Modify: `TESTS/Architecture/SessionAssistantArchitectureTests.cs`
- Test: `TESTS/Integration/KnowledgeBase/SessionAssistant/SessionAssistantTurnEndpointTests.cs`
- Modify: `docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md` (§6.2 passo 4, scostamento S6)

**Interfaces:**
- Consumes: `AskSessionAssistantCommand`, `AssistantTurnEvent` (Task 9); `SseJsonOptions.Default`; `RequireAuthenticatedUser()`, `RequireLiveSessionParticipant()` (legge il valore di rotta `sessionId`), `ClaimsPrincipal.GetUserId()`; il supporto dei test del Task 6.
- Produces:
  - `POST /api/v1/live-sessions/{sessionId}/assistant`, corpo `{ "question": string, "language": string? }`. Risposta `200 text/event-stream` con eventi `data: {json}` in camelCase; oppure `400`, `401`, `403`, `404`, `409` in JSON quando i controlli falliscono.
  - `SessionAssistantEndpoints.MapSessionAssistantEndpoints(this RouteGroupBuilder group)`.

- [ ] **Step 1: Scrivi i test di architettura**

In `SessionAssistantArchitectureTests.cs`, in fondo alla classe:

```csharp
    private const string AssistantApplication = "Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant";

    /// <summary>
    /// Tutti i tipi sotto lo spazio dei nomi dell'assistente, compresi quelli generati dal compilatore per
    /// i metodi asincroni: una variabile locale che attraversa un await diventa un loro campo.
    /// </summary>
    private static List<Type> AssistantApplicationTypes() =>
        typeof(Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools.SessionAssistantToolset).Assembly
            .GetTypes()
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith(AssistantApplication, StringComparison.Ordinal))
            .ToList();

    private static IEnumerable<Type> Mentioned(Type type)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var raw = type.GetConstructors(all).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
            .Concat(type.GetFields(all).Select(f => f.FieldType))
            .Concat(type.GetMethods(all).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)));
        return raw.SelectMany(Unwrap);
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        yield return type;
        if (type.HasElementType && type.GetElementType() is { } element)
        {
            foreach (var inner in Unwrap(element))
            {
                yield return inner;
            }
        }

        foreach (var argument in type.IsGenericType ? type.GetGenericArguments() : [])
        {
            foreach (var inner in Unwrap(argument))
            {
                yield return inner;
            }
        }
    }

    /// <summary>
    /// Spec §3.2: il lato applicativo dell'assistente non nomina GameManagement. Gli adattatori stanno in
    /// Infrastructure: così il turno non può arrivare a un comando di scrittura della partita.
    /// </summary>
    [Fact]
    public void AssistantApplication_DoesNotNameGameManagementTypes()
    {
        var types = AssistantApplicationTypes();

        types.Should().NotBeEmpty();
        foreach (var type in types)
        {
            Mentioned(type).Should().NotContain(
                t => t.Namespace != null && t.Namespace.StartsWith("Api.BoundedContexts.GameManagement", StringComparison.Ordinal),
                $"{type.FullName}: i tipi della sessione live passano dalle porte dell'assistente (spec §3.2)");
        }
    }

    /// <summary>Né il turno né gli strumenti ricevono il mediator: è il mezzo con cui si scrive la partita.</summary>
    [Fact]
    public void AssistantApplication_DoesNotReceiveTheMediator()
    {
        // Anche il contenitore: da un IServiceProvider il mediator si ottiene lo stesso.
        Type[] mediator =
        [
            typeof(MediatR.IMediator), typeof(MediatR.ISender), typeof(MediatR.IPublisher),
            typeof(IServiceProvider), typeof(Microsoft.Extensions.DependencyInjection.IServiceScopeFactory),
        ];

        foreach (var type in AssistantApplicationTypes())
        {
            Mentioned(type).Should().NotContain(t => mediator.Contains(t), $"{type.FullName}: niente mediator nell'assistente (spec §3.2)");
        }
    }
```

Sotto, nello stesso file, il controllo sul testo dei sorgenti: la riflessione vede le firme, non i corpi dei metodi.

```csharp
    /// <summary>
    /// Un tipo di GameManagement usato solo DENTRO un metodo sfugge alla riflessione. Qui si legge il testo
    /// dei sorgenti dell'assistente, saltando commenti e stringhe (<see cref="SourceScanner"/>).
    /// </summary>
    [Fact]
    public void AssistantApplicationSources_DoNotMentionGameManagementOrTheMediator()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "apps", "api", "src", "Api")))
        {
            root = root.Parent;
        }

        root.Should().NotBeNull("i sorgenti dell'API si cercano risalendo dalla cartella dei test");
        var folder = Path.Combine(
            root!.FullName, "apps", "api", "src", "Api", "BoundedContexts", "KnowledgeBase", "Application", "SessionAssistant");
        var files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);

        files.Should().NotBeEmpty();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var forbidden in new[] { "BoundedContexts.GameManagement", "IMediator", "ISender" })
            {
                SourceScanner.FindCodeOccurrences(text, forbidden).Should().BeEmpty(
                    $"{Path.GetFileName(file)} nomina «{forbidden}» nel codice (spec §3.2)");
            }
        }
    }
```

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SessionAssistantArchitectureTests"`
Expected: PASS: il codice dei Task 6-9 rispetta già le regole. Non essendoci un «rosso prima», la prova che i test vedono è una perturbazione, tre volte, ogni volta ripristinando il file per copia e `touch`:

1. Aggiungi al costruttore di `AssistantTurnRunner` un ultimo parametro **opzionale** `MediatR.IMediator? mediator = null` (opzionale, così i test del turno compilano ancora; un campo mai letto non compilerebbe, CS0414). Expected: `AssistantApplication_DoesNotReceiveTheMediator` e `AssistantApplicationSources_DoNotMentionGameManagementOrTheMediator` FAIL.
2. Aggiungi allo stesso costruttore `Api.BoundedContexts.GameManagement.Domain.Enums.LiveSessionStatus status = default`. Expected: `AssistantApplication_DoesNotNameGameManagementTypes` e il test sui sorgenti FAIL.
3. Dentro `ExecuteAsync`, aggiungi la riga `_ = Api.BoundedContexts.GameManagement.Domain.Enums.LiveSessionStatus.Completed;`. Expected: solo il test sui sorgenti FAIL: è il caso che la riflessione non vede.

- [ ] **Step 2: Scrivi i test dell'endpoint sul database vero**

`TESTS/Integration/KnowledgeBase/SessionAssistant/SessionAssistantTurnEndpointTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Api.Tests.Integration.KnowledgeBase.SessionAssistant.ScriptedChatGateway;

namespace Api.Tests.Integration.KnowledgeBase.SessionAssistant;

/// <summary>
/// Spec 2026-10-11 §6.2, dalla richiesta HTTP al database: un turno dell'assistente legge la sessione
/// vera, salva messaggi e proposte, e non scrive la partita. Il gateway LLM è un copione.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4276")]
public sealed class SessionAssistantTurnEndpointTests(SessionAssistantHostFixture host)
    : IClassFixture<SessionAssistantHostFixture>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _owner;
    private string _token = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await host.BeginTestAsync();
        host.Llm.Reset();
        host.Hub.Reset();
        _client = host.Factory.CreateClient();
        (_owner, _token) = await SessionAssistantTestData.SeedHostAsync(host.Factory, Ct);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task<LiveTable> StartedTableAsync() =>
        SessionAssistantTestData.CreateTableAsync(host.Factory, _owner, start: true, Ct, "Marco", "Giulia", "Luca");

    private static string Type(JsonElement turnEvent) => turnEvent.GetProperty("type").GetString()!;

    [Fact]
    public async Task ATurn_ReadsTheRealSession_SavesTheProposal_AndDoesNotWriteTheGame()
    {
        var versionId = await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();
        host.Llm.Reset(
            Calls("stato_partita", "{}"),
            Calls("proponi_punteggio", """{"giocatore":"Marco","punti":5}"""),
            Says("Ho preparato la proposta: +5 a Marco. Confermala dalla card."));

        var (status, events, body) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "Marco fa 5 punti", Ct);

        status.Should().Be(HttpStatusCode.OK, body);
        events.Select(Type).Should().Equal("status", "status", "status", "proposal", "answer", "done");
        events[1].GetProperty("tool").GetString().Should().Be("stato_partita");
        events[2].GetProperty("tool").GetString().Should().Be("proponi_punteggio");

        var card = events[3].GetProperty("proposal");
        card.GetProperty("kind").GetString().Should().Be("Score");
        card.GetProperty("status").GetString().Should().Be("Pending");
        card.GetProperty("playerId").GetGuid().Should().Be(table.Players["Marco"]);
        card.GetProperty("round").GetInt32().Should().Be(1, "appena avviata, la partita è al primo giro");
        card.GetProperty("points").GetInt32().Should().Be(5);
        card.GetProperty("cellValueBefore").GetInt32().Should().Be(0);
        card.GetProperty("resultingValue").GetInt32().Should().Be(5);
        events[5].GetProperty("profileVersionId").GetGuid().Should().Be(versionId);

        // Lo stato arrivato al modello è quello della sessione vera.
        var toolResult = host.Llm.Requests[1].Messages[^1];
        toolResult.Role.Should().Be("tool");
        toolResult.Content.Should().Contain("Marco").And.Contain("Giulia").And.Contain("Luca").And.Contain("punti");

        await using var scope = host.Factory.Services.CreateAsyncScope();
        var proposalId = card.GetProperty("id").GetGuid();
        var proposal = await scope.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().GetReadOnlyAsync(proposalId, Ct);
        proposal!.Status.Should().Be(AssistantProposalStatus.Pending);
        proposal.MessageId.Should().Be(events[5].GetProperty("responseId").GetGuid());

        var thread = await scope.ServiceProvider.GetRequiredService<IChatThreadRepository>().FindByLiveSessionIdAsync(table.SessionId, Ct);
        thread!.Messages.Select(m => m.Role).Should().Equal("user", "assistant");
        thread.Messages[1].Id.Should().Be(proposal.MessageId);

        // Nessuna scrittura senza conferma: la partita è com'era.
        var session = await SessionAssistantTestData.SendAsync(host.Factory, new GetLiveSessionQuery(table.SessionId), Ct);
        session.RoundScores.Should().BeEmpty();
        session.CurrentTurnIndex.Should().Be(1);
    }

    [Fact]
    public async Task TheSecondTurn_ExpiresTheUnconfirmedProposal_AndSendsTheHistory()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();
        host.Llm.Reset(Calls("proponi_punteggio", """{"giocatore":"Marco","punti":5}"""), Says("Proposta pronta."));
        var (_, first, _) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "Marco fa 5 punti", Ct);
        var firstProposal = first.Single(e => Type(e) == "proposal").GetProperty("proposal").GetProperty("id").GetGuid();

        host.Llm.Reset(Says("Tocca a Marco."));
        var (status, second, body) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "a chi tocca?", Ct);

        status.Should().Be(HttpStatusCode.OK, body);
        second.Select(Type).Should().Equal("status", "answer", "done");
        var sent = host.Llm.Requests.Single().Messages;
        sent.Select(m => m.Role).Should().Equal("system", "system", "user", "assistant", "user");
        sent[3].Content.Should().Contain("Proposta pronta.").And.Contain("scaduta senza conferma");

        await using var scope = host.Factory.Services.CreateAsyncScope();
        var expired = await scope.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().GetReadOnlyAsync(firstProposal, Ct);
        expired!.Status.Should().Be(AssistantProposalStatus.Expired);
        var thread = await scope.ServiceProvider.GetRequiredService<IChatThreadRepository>().FindByLiveSessionIdAsync(table.SessionId, Ct);
        thread!.Messages.Should().HaveCount(4);
        thread.LiveSessionId.Should().Be(table.SessionId, "il secondo salvataggio non deve azzerare la colonna");
    }

    // ─── Campo del profilo → effetto (spec §3.2) ────────────────────────────────

    [Fact]
    public async Task WithoutTheSectionInThePublishedProfile_TheAssistantIsOff()
    {
        var table = await StartedTableAsync();

        var (status, events, body) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "a chi tocca?", Ct);

        status.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("assistant_disabled");
        events.Should().BeEmpty("un controllo fallito risponde in JSON, non con uno stream");
        host.Llm.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task TheFieldsOfTheSection_ReachTheGateway()
    {
        var versionId = await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct, assistant =>
            Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.SessionAssistantSettings.Create(
                true, assistant.SystemPrompts.ToDictionary(StringComparer.Ordinal), temperature: 0.4m, maxResponseTokens: 450,
                maxToolIterations: 1));
        var table = await StartedTableAsync();
        host.Llm.Reset(Calls("stato_partita", "{}"), Says("Tocca a Marco."));

        var (status, _, body) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "who's next?", Ct, language: "en");

        status.Should().Be(HttpStatusCode.OK, body);
        var requests = host.Llm.Requests;
        requests.Should().HaveCount(2, "un giro con strumenti, poi la chiamata finale");
        requests[0].Messages[0].Content.Should().Be(SessionAssistantTestData.EnglishPrompt);
        requests[0].Temperature.Should().Be(0.4);
        requests[0].MaxTokens.Should().Be(450);
        requests[0].PrimaryModelId.Should().Be("deepseek-flash");
        requests[0].FallbackModelId.Should().BeNull();
        requests[0].ProfileVersionId.Should().Be(versionId);
        requests[0].Tools.Should().HaveCount(3);
        requests[1].Tools.Should().BeEmpty("esauriti i giri della sezione, il modello deve rispondere");
    }

    // ─── Chi può chiedere, e quando ─────────────────────────────────────────────

    [Fact]
    public async Task AParticipantWhoIsNotTheOwner_IsForbidden()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var (guest, guestToken) = await SessionAssistantTestData.SeedHostAsync(host.Factory, Ct);
        var table = await SessionAssistantTestData.CreateTableAsync(host.Factory, _owner, start: false, Ct, "Marco");
        await SessionAssistantTestData.SendAsync(host.Factory, new AddPlayerToLiveSessionCommand(
            table.SessionId, "Giulia", Api.BoundedContexts.GameManagement.Domain.Enums.PlayerColor.Blue, UserId: guest), Ct);
        await SessionAssistantTestData.SendAsync(host.Factory, new StartLiveSessionCommand(
            table.SessionId, _owner, Api.SharedKernel.Domain.ValueObjects.UserTier.Free, Api.SharedKernel.Domain.ValueObjects.Role.User), Ct);

        var (status, _, body) = await SessionAssistantTestData.AskAsync(_client, guestToken, table.SessionId, "a chi tocca?", Ct);

        status.Should().Be(HttpStatusCode.Forbidden, body);
        host.Llm.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ACompletedSession_IsAConflict_WithItsCode()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();
        await SessionAssistantTestData.SendAsync(host.Factory, new CompleteLiveSessionCommand(table.SessionId), Ct);

        var (status, _, body) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "a chi tocca?", Ct);

        status.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("session_state_invalid");
    }

    [Fact]
    public async Task AnEmptyQuestion_IsABadRequest_AndAnAnonymousCallerIsUnauthorized()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();

        var (empty, _, emptyBody) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "  ", Ct);
        var anonymous = await _client.PostAsync(
            $"/api/v1/live-sessions/{table.SessionId}/assistant",
            System.Net.Http.Json.JsonContent.Create(new { question = "a chi tocca?" }), Ct);

        empty.Should().Be(HttpStatusCode.BadRequest, emptyBody);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AGatewayFailure_IsAnErrorEvent_NotABrokenStream()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();
        host.Llm.Reset();   // copione vuoto: il gateway fallisce senza codice

        var (status, events, body) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "a chi tocca?", Ct);

        status.Should().Be(HttpStatusCode.OK, body);
        events.Select(Type).Should().Equal("status", "answer", "error");
        events[^1].GetProperty("code").GetString().Should().Be("assistant_unavailable");
    }
}
```

- [ ] **Step 3: Verifica che falliscano**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SessionAssistantTurnEndpointTests"`
Expected: FAIL: ogni richiesta riceve `404`, perché la rotta non esiste (i test che si aspettano `401`, `403` o `409` falliscono per lo stesso motivo).

- [ ] **Step 4: Scrivi l'endpoint e registralo**

`API/Routing/SessionAssistantEndpoints.cs`:

```csharp
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;
using Api.Extensions;
using Api.Infrastructure.Serialization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Spec 2026-10-11 §6.2: l'assistente in partita. Il filtro degli endpoint live controlla la
/// partecipazione; che chi chiede sia il proprietario lo controllano gli handler.
/// </summary>
internal static class SessionAssistantEndpoints
{
    public static RouteGroupBuilder MapSessionAssistantEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/live-sessions/{sessionId}/assistant", HandleAsk)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces(200, contentType: "text/event-stream")
            .Produces(400)
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .Produces(409)  // assistant_disabled · assistant_model_without_tools · session_state_invalid
            .WithTags("LiveSessions")
            .WithSummary("Ask the in-game assistant (SSE)")
            .WithDescription("Host only. Streams status, proposal, answer and done (or error) events. The assistant never writes the game: it creates proposals the host confirms.");

        return group;
    }

    private static async Task HandleAsk(
        [FromRoute] Guid sessionId,
        [FromBody] AskSessionAssistantRequest request,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new AskSessionAssistantCommand(sessionId, httpContext.User.GetUserId(), request.Question, request.Language);
        var events = mediator.CreateStream(command, cancellationToken).GetAsyncEnumerator(cancellationToken);
        await using var disposal = events.ConfigureAwait(false);

        // Il primo evento arriva dopo i controlli del turno. Se falliscono, l'eccezione esce da qui prima
        // che la risposta sia partita, e il middleware la traduce in 403, 404 o 409 con il suo codice.
        if (!await events.MoveNextAsync().ConfigureAwait(false))
        {
            return;
        }

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.Append("Cache-Control", "no-cache");
        httpContext.Response.Headers.Append("X-Accel-Buffering", "no");

        do
        {
            await httpContext.Response.WriteAsync(
                $"data: {JsonSerializer.Serialize(events.Current, SseJsonOptions.Default)}\n\n", cancellationToken)
                .ConfigureAwait(false);
            await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        while (await events.MoveNextAsync().ConfigureAwait(false));
    }

    internal sealed record AskSessionAssistantRequest(string Question, string? Language);
}
```

In `Program.cs`, sotto `v1Api.MapLiveSessionEndpoints();`:

```csharp
v1Api.MapSessionAssistantEndpoints(); // spec 2026-10-11 §6.2: assistente in partita
```

- [ ] **Step 5: Verifica che i test passino**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SessionAssistantTurnEndpointTests"`
Expected: PASS. Annota numero e tempo. Un `500` dove il test si aspetta `403` o `409` non si corregge nel test: vuol dire che un'eccezione **non mappata** è uscita prima del primo evento (per esempio `AgentProfileNotPublishedException`, se il database del test non ha il profilo). Dopo il primo evento lo stato è già `200`, e un errore arriva come evento `error`.

- [ ] **Step 6: Spec e commit**

Nella spec, §6.2 passo 4, sostituisci l'elenco degli eventi con:

> - `status` all'avvio del turno e poi a ogni strumento che parte, con il suo nome («leggo i punteggi…» lo compone il client);
> - `proposal` per ogni card, **dopo il salvataggio**: una card mostrata prima si potrebbe confermare quando la proposta non è ancora nel database;
> - `answer`, con il testo (e, dalla fetta A5, le citazioni dopo `FinalizeAsync`);
> - `done`, con `responseId` e versione del profilo;
> - `error`, con un codice (`budget_paused`, `turn_timeout`, `assistant_unavailable`), al posto di `done` quando il turno si ferma a metà: le proposte già create sono state comunque salvate e inviate.
>
> Al gateway arrivano la versione del profilo e `RequestSource.SessionAssistant`; l'id della sessione no, perché `LlmChatRequest` non ha il campo: arriva con le metriche della fetta A6, a cui serve per il costo per partita.
>
> I controlli del passo 1 avvengono prima del primo evento: se falliscono la risposta è un errore HTTP in JSON (`403`, `404`, `409` con `assistant_disabled`, `assistant_model_without_tools` o `session_state_invalid`), non uno stream.

```bash
git add apps/api docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md
git commit -m "feat(ai): endpoint SSE dell'assistente in partita (#4276)"
```

### Chiusura della fetta A3a

- [ ] **Suite unit e build Release**

```bash
dotnet build apps/api/src/Api/Api.csproj -c Release
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "(BoundedContext=KnowledgeBase|BoundedContext=Architecture)&Category=Unit" > "$TEMP/a3a-unit.log" 2>&1; tail -5 "$TEMP/a3a-unit.log"
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Integration.KnowledgeBase.SessionAssistant|FullyQualifiedName~ChatThreadRepositoryIntegrationTests" > "$TEMP/a3a-integration.log" 2>&1; tail -5 "$TEMP/a3a-integration.log"
```

Expected: build con 0 avvisi; nella suite unit solo i cinque falliti noti; integrazione tutta verde. Annota i tre numeri.

- [ ] **Review dell'intero branch**, PR verso `main-dev` (`feat(ai): turno dell'assistente in partita: lettore, thread, SSE (#4276)`), CI, squash merge, pulizia. Nel corpo della PR: l'assistente resta spento in ogni ambiente (nessuna versione pubblicata lo abilita), e i test di integrazione eseguiti in locale con il loro numero.
- [ ] **Dopo il merge**: guarda il run `dev-async` su `main-dev` (lì girano i test di integrazione): `gh run list --workflow dev-async.yml --branch main-dev --limit 1`. Un rosso sui test di questa fetta si corregge subito.
- [ ] **Issue #4276**: DoD spuntata e chiusa; epic #4238 aggiornato.

---

# Fetta A3b — #4277

Worktree `D:/Repositories/wt-assistant-a3b`, branch `feature/issue-4277-assistant-confirm` da `origin/main-dev` **dopo il merge di A3a**, `git config branch.feature/issue-4277-assistant-confirm.parent main-dev`.

### Task 11: La conferma, «prima prenota, poi applica»

**Files:**
- Create: `KB/Application/SessionAssistant/Confirmation/IAssistantProposalApplier.cs` (porta ed eccezione)
- Create: `KB/Application/SessionAssistant/Confirmation/ConfirmAssistantProposalCommand.cs` (comando, validator, handler)
- Create: `KB/Infrastructure/SessionAssistant/LiveSessionProposalApplier.cs`
- Modify: `KB/Infrastructure/DependencyInjection/SessionAssistantServiceExtensions.cs`
- Modify: `TESTS/Architecture/SessionAssistantArchitectureTests.cs`
- Test: `TESTS/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/SessionAssistantRegistrationTests.cs` (la porta di scrittura)
- Test: `TESTS/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Confirmation/ConfirmAssistantProposalTests.cs`
- Test: `TESTS/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/LiveSessionProposalApplierTests.cs`

**Interfaces:**
- Consumes: `IAssistantProposalRepository.GetAsync` (tracciata) e `GetReadOnlyAsync`; `AssistantProposal.BeginApplying`, `MarkAccepted`, `MarkEdited`, `MarkFailed`, `MarkStale`, `ScoreValueToWrite`, `NotPendingCode`, `MaxPointsPerProposal` (Task 1); `IAssistantSessionDirectory` (Task 6); `AssistantProposalDto.From` (Task 8); `ConfirmScoreProposalCommand(Guid SessionId, Guid ConfirmingUserId, Guid TargetPlayerId, int Round, string Dimension, int Value) : IRequest` e `AdvanceLiveSessionTurnCommand(Guid SessionId) : ICommand` di GameManagement.
- Produces:

```csharp
internal interface IAssistantProposalApplier
{
    Task ApplyScoreAsync(Guid liveSessionId, Guid confirmingUserId, Guid playerId, int round, string dimension, int value, CancellationToken cancellationToken);
    Task AdvanceTurnAsync(Guid liveSessionId, CancellationToken cancellationToken);
}

internal sealed class AssistantProposalApplyException : Exception   // FailureCode: "session_state_invalid" | "proposal_not_applicable"

internal sealed record ConfirmAssistantProposalCommand(Guid LiveSessionId, Guid ProposalId, Guid UserId, int? CorrectedPoints)
    : ICommand<AssistantProposalDto>;
```

  - Esiti dell'handler: il DTO della proposta (`Accepted`, `Edited`, o lo stato attuale per un secondo tap su `Applying`, `Accepted`, `Edited`); `ForbiddenException`; `NotFoundException`; `ConflictException` con codice `proposal_stale`, `proposal_not_pending`, `session_state_invalid` o `proposal_not_applicable`; `ValidationException` per una correzione su una proposta di turno.
  - Costanti su `ConfirmAssistantProposalCommandHandler`: `StaleCode = "proposal_stale"`, `StateUnavailableCode = "session_state_unavailable"`. Su `AssistantProposalApplyException`: `SessionStateInvalid = "session_state_invalid"`, `NotApplicable = "proposal_not_applicable"`.

Tre scelte di questa fetta, tutte dentro la spec §5.3:

- **L'applicatore gira in uno scope suo.** Il comando della sessione live salva sul proprio `DbContext`; se quel salvataggio fallisse dentro lo scope della richiesta, le sue entità resterebbero tracciate e il salvataggio successivo della proposta le riproverebbe. Con uno scope a sé, proposta e partita non condividono il change tracker (la spec dice già che non condividono la transazione).
- **Dopo la prenotazione la conferma non si annulla più.** Dal momento in cui la proposta è `Applying`, lettura dello stato, applicazione e salvataggio dell'esito usano `CancellationToken.None`: un host che chiude la pagina non deve lasciare una proposta a metà.
- **`Failed` solo quando si sa che la partita non è stata scritta.** Lo stato che non si riesce a leggere, e i rifiuti che i due comandi della sessione lanciano **prima** di salvare (stato non valido, sessione sparita, giocatore uscito, concorrenza), chiudono la proposta come `Failed`. Ogni altro errore dell'applicazione può essere arrivato **dopo** il salvataggio (per esempio la trasmissione ai partecipanti): lì non si sa se la partita è stata scritta, e dire «fallita» inviterebbe l'host a ripetere un punteggio già registrato. In quel caso la proposta resta `Applying`: è il caso raro che la spec accetta, e il turno successivo lo segnala nei log (Task 9).

- [ ] **Step 1: Scrivi i test dell'handler**

`TESTS/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Confirmation/ConfirmAssistantProposalTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using ValidationException = Api.SharedKernel.Domain.Exceptions.ValidationException;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;

/// <summary>
/// Spec 2026-10-11 §5.3: la conferma è l'unico ponte verso le scritture. «Prima prenota, poi applica»:
/// la prenotazione è l'idempotenza, il controllo dello stato atteso protegge da una partita cambiata.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4277")]
public sealed class ConfirmAssistantProposalTests
{
    private static readonly Guid Session = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Thread = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid Owner = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid Version = Guid.Parse("00000000-0000-0000-0000-0000000000d1");
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly Mock<IAssistantProposalRepository> _proposals = new();
    private readonly Mock<IAssistantSessionDirectory> _directory = new();
    private readonly Mock<ISessionAssistantReader> _reader = new();
    private readonly Mock<IAssistantProposalApplier> _applier = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 20, 0, 0, TimeSpan.Zero));

    public ConfirmAssistantProposalTests()
    {
        _directory.Setup(d => d.GetAccessAsync(Session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantSessionAccess(Owner, "InProgress", true));
        _directory.Setup(d => d.CreateReader(Session, Owner)).Returns(_reader.Object);
        Table(cell: 7, turnIndex: 4);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    /// <summary>Lo stato della partita al momento della conferma: la cella di Marco al round 2 e l'indice di turno.</summary>
    private void Table(int? cell, int turnIndex) =>
        _reader.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new AssistantSessionState(
            GameId: null,
            GameName: "Azul",
            Status: "InProgress",
            CanRecordScores: true,
            CanAdvanceTurn: true,
            Players: [new(Marco, "Marco", cell ?? 0, true)],
            CurrentTurnPlayerId: Marco,
            CurrentTurnIndex: turnIndex,
            CurrentRound: 2,
            CurrentPhase: null,
            Dimensions: ["punti"],
            Scores: cell is { } value
                ? new List<AssistantScoreCell> { new(Marco, 2, "PUNTI", value) }
                : new List<AssistantScoreCell>(),
            HouseRules: []));

    private AssistantProposal ScoreProposal(int points = 5, int cellBefore = 7)
    {
        var proposal = AssistantProposal.ForScore(Session, Thread, Guid.NewGuid(), Version, Marco, "Marco", "punti", 2, points, cellBefore, Now);
        _proposals.Setup(p => p.GetAsync(proposal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(proposal);
        return proposal;
    }

    private AssistantProposal TurnProposal(int turnIndex = 4)
    {
        var proposal = AssistantProposal.ForAdvanceTurn(Session, Thread, Guid.NewGuid(), Version, turnIndex, Now);
        _proposals.Setup(p => p.GetAsync(proposal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(proposal);
        return proposal;
    }

    private ConfirmAssistantProposalCommandHandler Handler() =>
        new(_proposals.Object, _directory.Object, _applier.Object, _unitOfWork.Object, _time);

    private Task<AssistantProposalDto> ConfirmAsync(AssistantProposal proposal, int? corrected = null, Guid? user = null, Guid? session = null) =>
        Handler().Handle(new ConfirmAssistantProposalCommand(session ?? Session, proposal.Id, user ?? Owner, corrected), Ct);

    private void VerifyNothingApplied()
    {
        _applier.Verify(a => a.ApplyScoreAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _applier.Verify(a => a.AdvanceTurnAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirm_ReservesFirst_ThenWritesTheCellPlusTheDelta_AndAccepts()
    {
        var proposal = ScoreProposal(points: 5, cellBefore: 7);
        AssistantProposalStatus? statusWhenApplied = null;
        var savesBeforeApply = -1;
        _applier.Setup(a => a.ApplyScoreAsync(Session, Owner, Marco, 2, "punti", 12, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                statusWhenApplied = proposal.Status;
                savesBeforeApply = _unitOfWork.Invocations.Count;
            })
            .Returns(Task.CompletedTask);

        var dto = await ConfirmAsync(proposal);

        statusWhenApplied.Should().Be(AssistantProposalStatus.Applying, "la prenotazione precede la scrittura");
        savesBeforeApply.Should().Be(1, "ed è già salvata quando la scrittura parte");
        proposal.Status.Should().Be(AssistantProposalStatus.Accepted);
        proposal.ResolvedBy.Should().Be(Owner);
        dto.Status.Should().Be("Accepted");
        dto.ResultingValue.Should().Be(12);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Confirm_WithACorrection_WritesTheCorrectedDelta_AndIsEdited()
    {
        var proposal = ScoreProposal(points: 5, cellBefore: 7);

        var dto = await ConfirmAsync(proposal, corrected: 3);

        _applier.Verify(a => a.ApplyScoreAsync(Session, Owner, Marco, 2, "punti", 10, It.IsAny<CancellationToken>()), Times.Once);
        proposal.Status.Should().Be(AssistantProposalStatus.Edited);
        dto.ConfirmedPoints.Should().Be(3);
        dto.ResultingValue.Should().Be(10);
    }

    [Fact]
    public async Task Confirm_WithTheSamePointsAsProposed_IsAccepted_NotEdited()
    {
        var proposal = ScoreProposal(points: 5);

        await ConfirmAsync(proposal, corrected: 5);

        proposal.Status.Should().Be(AssistantProposalStatus.Accepted);
        proposal.ConfirmedPayload.Should().BeNull();
    }

    [Fact]
    public async Task Confirm_OnACellThatDidNotExist_ExpectsZero()
    {
        Table(cell: null, turnIndex: 4);
        var proposal = ScoreProposal(points: 5, cellBefore: 0);

        await ConfirmAsync(proposal);

        _applier.Verify(a => a.ApplyScoreAsync(Session, Owner, Marco, 2, "punti", 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Review Focus 4: qualcuno ha scritto la stessa cella fra la proposta e la conferma.</summary>
    [Fact]
    public async Task Confirm_WhenTheCellChanged_IsStaleAndDoesNotWrite()
    {
        var proposal = ScoreProposal(points: 5, cellBefore: 7);
        Table(cell: 9, turnIndex: 4);

        var act = () => ConfirmAsync(proposal);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("proposal_stale");
        proposal.Status.Should().Be(AssistantProposalStatus.Stale);
        VerifyNothingApplied();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2), "prenotazione ed esito");
    }

    [Fact]
    public async Task Confirm_ATurn_AdvancesIt()
    {
        var proposal = TurnProposal(turnIndex: 4);

        var dto = await ConfirmAsync(proposal);

        _applier.Verify(a => a.AdvanceTurnAsync(Session, It.IsAny<CancellationToken>()), Times.Once);
        dto.Status.Should().Be("Accepted");
    }

    [Fact]
    public async Task Confirm_ATurn_WhenTheTurnAlreadyMoved_IsStale()
    {
        var proposal = TurnProposal(turnIndex: 4);
        Table(cell: 7, turnIndex: 5);

        var act = () => ConfirmAsync(proposal);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("proposal_stale");
        VerifyNothingApplied();
    }

    [Fact]
    public async Task Confirm_ByAnyoneButTheOwner_IsForbidden_AndNothingIsReserved()
    {
        var proposal = ScoreProposal();

        var act = () => ConfirmAsync(proposal, user: Guid.NewGuid());

        await act.Should().ThrowAsync<ForbiddenException>();
        proposal.Status.Should().Be(AssistantProposalStatus.Pending);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirm_AProposalOfAnotherSession_IsNotFound()
    {
        // L'host possiede anche l'altra sessione: l'id della proposta non basta a confermarla da lì.
        var other = Guid.NewGuid();
        _directory.Setup(d => d.GetAccessAsync(other, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantSessionAccess(Owner, "InProgress", true));
        var proposal = ScoreProposal();

        var act = () => ConfirmAsync(proposal, session: other);

        await act.Should().ThrowAsync<NotFoundException>();
        proposal.Status.Should().Be(AssistantProposalStatus.Pending);
    }

    [Theory]
    [InlineData("session_state_invalid")]
    [InlineData("proposal_not_applicable")]
    public async Task Confirm_WhenTheDomainRefuses_IsFailedWithTheCode(string failureCode)
    {
        var proposal = ScoreProposal();
        _applier.Setup(a => a.ApplyScoreAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AssistantProposalApplyException(failureCode, "the domain said no", new InvalidOperationException()));

        var act = () => ConfirmAsync(proposal);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be(failureCode);
        proposal.Status.Should().Be(AssistantProposalStatus.Failed);
        proposal.FailureCode.Should().Be(failureCode);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>
    /// Review Focus 8: dopo la prenotazione lo stato della partita non si legge (la sessione è sparita).
    /// Nulla è stato scritto, quindi la proposta si chiude: restando Applying avrebbe una card che non si
    /// può più né confermare né rifiutare.
    /// </summary>
    [Fact]
    public async Task Confirm_WhenTheStateCannotBeRead_FailsTheProposal_InsteadOfLeavingItApplying()
    {
        var proposal = ScoreProposal();
        _reader.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("LiveGameSession", Session.ToString()));

        var act = () => ConfirmAsync(proposal);

        await act.Should().ThrowAsync<NotFoundException>();
        proposal.Status.Should().Be(AssistantProposalStatus.Failed);
        proposal.FailureCode.Should().Be("session_state_unavailable");
        VerifyNothingApplied();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2), "prenotazione ed esito");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASecondTap_OnAProposalAlreadyConfirmed_ReturnsItsState_WithoutApplyingAgain(bool edited)
    {
        var proposal = ScoreProposal();
        proposal.BeginApplying(Owner);
        if (edited)
        {
            proposal.MarkEdited(3, Now);
        }
        else
        {
            proposal.MarkAccepted(Now);
        }

        var dto = await ConfirmAsync(proposal);

        dto.Status.Should().Be(edited ? "Edited" : "Accepted");
        VerifyNothingApplied();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirm_ARejectedProposal_IsNotPending()
    {
        var proposal = ScoreProposal();
        proposal.Reject(Owner, Now);

        var act = () => ConfirmAsync(proposal);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("proposal_not_pending");
        VerifyNothingApplied();
    }

    /// <summary>
    /// Review Focus 3: due tap nello stesso istante. Entrambi caricano la proposta Pending; il secondo che
    /// salva la prenotazione perde su xmin, non scrive e riceve lo stato lasciato dal primo.
    /// </summary>
    [Fact]
    public async Task Confirm_WhenTheReservationLosesTheRace_ReturnsTheCurrentState_WithoutWriting()
    {
        var proposal = ScoreProposal();
        var winner = AssistantProposal.ForScore(Session, Thread, proposal.MessageId, Version, Marco, "Marco", "punti", 2, 5, 7, Now);
        winner.BeginApplying(Owner);
        winner.MarkAccepted(Now);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateConcurrencyException("xmin"));
        _proposals.Setup(p => p.GetReadOnlyAsync(proposal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(winner);

        var dto = await ConfirmAsync(proposal);

        dto.Status.Should().Be("Accepted");
        VerifyNothingApplied();
    }

    [Fact]
    public async Task Confirm_WhenTheReservationLosesToAnExpiry_IsNotPending()
    {
        var proposal = ScoreProposal();
        var expired = AssistantProposal.ForScore(Session, Thread, proposal.MessageId, Version, Marco, "Marco", "punti", 2, 5, 7, Now);
        expired.Expire(Now);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateConcurrencyException("xmin"));
        _proposals.Setup(p => p.GetReadOnlyAsync(proposal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(expired);

        var act = () => ConfirmAsync(proposal);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("proposal_not_pending");
        VerifyNothingApplied();
    }

    [Fact]
    public async Task AfterTheReservation_TheConfirmNoLongerFollowsTheClientCancellation()
    {
        var proposal = ScoreProposal();
        using var client = new CancellationTokenSource();

        await Handler().Handle(new ConfirmAssistantProposalCommand(Session, proposal.Id, Owner, null), client.Token);

        _applier.Verify(a => a.ApplyScoreAsync(
            Session, Owner, Marco, 2, "punti", 12, It.Is<CancellationToken>(token => !token.CanBeCanceled)), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.Is<CancellationToken>(token => token.CanBeCanceled)), Times.Once, "la prenotazione");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.Is<CancellationToken>(token => !token.CanBeCanceled)), Times.Once, "l'esito");
        _reader.Verify(r => r.GetStateAsync(It.Is<CancellationToken>(token => !token.CanBeCanceled)), Times.Once);
    }

    [Fact]
    public async Task ACorrectionOnATurnProposal_IsAValidationError_AndNothingIsReserved()
    {
        var proposal = TurnProposal();

        var act = () => ConfirmAsync(proposal, corrected: 1);

        await act.Should().ThrowAsync<ValidationException>();
        proposal.Status.Should().Be(AssistantProposalStatus.Pending);
    }

    [Fact]
    public void TheValidator_WantsTheIds_AndACorrectionWithinTheLimit()
    {
        var validator = new ConfirmAssistantProposalCommandValidator();

        validator.TestValidate(new ConfirmAssistantProposalCommand(Session, Guid.NewGuid(), Owner, 3)).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new ConfirmAssistantProposalCommand(Session, Guid.NewGuid(), Owner, null)).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new ConfirmAssistantProposalCommand(Guid.Empty, Guid.NewGuid(), Owner, null))
            .ShouldHaveValidationErrorFor(c => c.LiveSessionId);
        validator.TestValidate(new ConfirmAssistantProposalCommand(Session, Guid.Empty, Owner, null))
            .ShouldHaveValidationErrorFor(c => c.ProposalId);
        validator.TestValidate(new ConfirmAssistantProposalCommand(Session, Guid.NewGuid(), Owner, AssistantProposal.MaxPointsPerProposal + 1))
            .ShouldHaveValidationErrorFor(c => c.CorrectedPoints);
    }
}
```

- [ ] **Step 2: Scrivi i test dell'applicatore e la regola di architettura**

`TESTS/BoundedContexts/KnowledgeBase/Infrastructure/SessionAssistant/LiveSessionProposalApplierTests.cs`:

```csharp
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// Spec 2026-10-11 §5.3, passo 4: la conferma applica attraverso i comandi esistenti della sessione
/// live, e traduce i loro rifiuti in un codice che la proposta può conservare.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4277")]
public sealed class LiveSessionProposalApplierTests
{
    private static readonly Guid Session = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Owner = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly Mock<IMediator> _mediator = new();

    private LiveSessionProposalApplier Applier()
    {
        var services = new ServiceCollection().AddScoped(_ => _mediator.Object).BuildServiceProvider();
        return new LiveSessionProposalApplier(services.GetRequiredService<IServiceScopeFactory>());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AScore_GoesThroughTheHostConfirmationCommand()
    {
        await Applier().ApplyScoreAsync(Session, Owner, Marco, 2, "punti", 12, Ct);

        _mediator.Verify(m => m.Send(
            It.Is<IRequest>(r => r.Equals(new ConfirmScoreProposalCommand(Session, Owner, Marco, 2, "punti", 12))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ATurn_GoesThroughTheAdvanceTurnCommand()
    {
        await Applier().AdvanceTurnAsync(Session, Ct);

        _mediator.Verify(m => m.Send(
            It.Is<IRequest>(r => r.Equals(new AdvanceLiveSessionTurnCommand(Session))), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// I rifiuti che i due comandi della sessione lanciano PRIMA di salvare: nulla è stato scritto, quindi
    /// la proposta può chiudersi come fallita.
    /// </summary>
    public static TheoryData<Exception, string> Refusals() => new()
    {
        { new ConflictException("Cannot record scores in Completed status."), "session_state_invalid" },
        { new DbUpdateConcurrencyException("xmin"), "session_state_invalid" },
        { new NotFoundException("LiveGameSession", "a1"), "session_state_invalid" },
        { new DomainException("Active player not found in this session"), "proposal_not_applicable" },
        { new ForbiddenException("Only the session host can confirm score proposals"), "proposal_not_applicable" },
        { new FluentValidation.ValidationException("Value must be greater than or equal to 0"), "proposal_not_applicable" },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task ARefusalOfTheSession_BecomesAFailureCode(Exception refusal, string expectedCode)
    {
        _mediator.Setup(m => m.Send(It.IsAny<IRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(refusal);

        var act = () => Applier().AdvanceTurnAsync(Session, Ct);

        var thrown = (await act.Should().ThrowAsync<AssistantProposalApplyException>()).Which;
        thrown.FailureCode.Should().Be(expectedCode);
        thrown.InnerException.Should().BeSameAs(refusal);
    }

    /// <summary>
    /// Per esempio la trasmissione ai partecipanti che fallisce DOPO il salvataggio: non si sa se la partita
    /// è stata scritta, quindi non è un rifiuto. L'errore risale e la proposta resta Applying (spec §5.3).
    /// </summary>
    [Fact]
    public async Task AnythingElse_IsNotARefusal_AndPassesThrough()
    {
        _mediator.Setup(m => m.Send(It.IsAny<IRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the hub is down"));

        var act = () => Applier().AdvanceTurnAsync(Session, Ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
```

In `SessionAssistantArchitectureTests.cs`, in fondo alla classe:

```csharp
    /// <summary>
    /// Spec §5.3: la conferma è l'unico ponte verso le scritture. La porta che scrive la riceve solo il suo
    /// handler: se la ricevesse il turno, il modello potrebbe arrivare a una scrittura senza una persona.
    /// </summary>
    [Fact]
    public void OnlyTheConfirmHandler_ReceivesThePortThatWritesTheGame()
    {
        var applier = typeof(Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation.IAssistantProposalApplier);

        var receivers = applier.Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(c => c.GetParameters().Any(p => p.ParameterType == applier)))
            .Select(t => t.Name)
            .ToList();

        receivers.Should().Equal("ConfirmAssistantProposalCommandHandler");
    }
```

- [ ] **Step 3: Verifica che non compilino**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ConfirmAssistantProposalTests|FullyQualifiedName~LiveSessionProposalApplierTests"`
Expected: `CS0246` su `IAssistantProposalApplier`, `AssistantProposalApplyException`, `ConfirmAssistantProposalCommand`, `LiveSessionProposalApplier`.

- [ ] **Step 4: Scrivi la porta, il comando e l'applicatore**

`KB/Application/SessionAssistant/Confirmation/IAssistantProposalApplier.cs`:

```csharp
#pragma warning disable MA0048 // File name must match type name - the write port and its refusal
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;

/// <summary>
/// L'unica porta dell'assistente che scrive la partita (spec 2026-10-11 §5.3). La riceve solo
/// <see cref="ConfirmAssistantProposalCommandHandler"/>, cioè un'azione avviata da una persona: lo fissa
/// un test architetturale. L'implementazione passa dai comandi esistenti della sessione live.
/// </summary>
internal interface IAssistantProposalApplier
{
    /// <summary>Scrive <paramref name="value"/> nella cella (giocatore, round, dimensione) e lo trasmette ai partecipanti.</summary>
    /// <exception cref="AssistantProposalApplyException">La sessione ha rifiutato la scrittura.</exception>
    Task ApplyScoreAsync(
        Guid liveSessionId, Guid confirmingUserId, Guid playerId, int round, string dimension, int value,
        CancellationToken cancellationToken);

    /// <exception cref="AssistantProposalApplyException">La sessione ha rifiutato il passaggio di turno.</exception>
    Task AdvanceTurnAsync(Guid liveSessionId, CancellationToken cancellationToken);
}

/// <summary>
/// La sessione ha rifiutato ciò che la proposta voleva scrivere. Il codice finisce in
/// <c>failure_code</c> della proposta e nel 409 della risposta.
/// </summary>
#pragma warning disable S3871 // Exceptions should be public — internal here is intentional (BC-private contract)
internal sealed class AssistantProposalApplyException(string failureCode, string message, Exception innerException)
    : Exception(message, innerException)
{
    /// <summary>Lo stato della sessione non permette la scrittura (per esempio è conclusa), o è cambiata nel frattempo.</summary>
    public const string SessionStateInvalid = "session_state_invalid";

    /// <summary>La proposta non è più applicabile a questa partita: il giocatore è uscito, la dimensione non c'è più.</summary>
    public const string NotApplicable = "proposal_not_applicable";

    public string FailureCode { get; } = failureCode;
}
#pragma warning restore S3871
```

`KB/Application/SessionAssistant/Confirmation/ConfirmAssistantProposalCommand.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ValidationException = Api.SharedKernel.Domain.Exceptions.ValidationException;

#pragma warning disable MA0048 // File name must match type name - command, validator and handler
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;

/// <summary>
/// Spec 2026-10-11 §5.3: l'host conferma una proposta dell'assistente, con l'eventuale incremento corretto.
/// È l'unico ponte verso le scritture della partita.
/// </summary>
internal sealed record ConfirmAssistantProposalCommand(Guid LiveSessionId, Guid ProposalId, Guid UserId, int? CorrectedPoints)
    : ICommand<AssistantProposalDto>;

internal sealed class ConfirmAssistantProposalCommandValidator : AbstractValidator<ConfirmAssistantProposalCommand>
{
    public ConfirmAssistantProposalCommandValidator()
    {
        RuleFor(c => c.LiveSessionId).NotEmpty();
        RuleFor(c => c.ProposalId).NotEmpty();
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.CorrectedPoints)
            .InclusiveBetween(-AssistantProposal.MaxPointsPerProposal, AssistantProposal.MaxPointsPerProposal)
            .When(c => c.CorrectedPoints.HasValue);
    }
}

internal sealed class ConfirmAssistantProposalCommandHandler
    : ICommandHandler<ConfirmAssistantProposalCommand, AssistantProposalDto>
{
    public const string StaleCode = "proposal_stale";

    /// <summary>Il <c>failure_code</c> di una proposta che, dopo la prenotazione, non ha potuto leggere la partita.</summary>
    public const string StateUnavailableCode = "session_state_unavailable";

    private readonly IAssistantProposalRepository _proposals;
    private readonly IAssistantSessionDirectory _directory;
    private readonly IAssistantProposalApplier _applier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ConfirmAssistantProposalCommandHandler(
        IAssistantProposalRepository proposals,
        IAssistantSessionDirectory directory,
        IAssistantProposalApplier applier,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _proposals = proposals ?? throw new ArgumentNullException(nameof(proposals));
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _applier = applier ?? throw new ArgumentNullException(nameof(applier));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<AssistantProposalDto> Handle(ConfirmAssistantProposalCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Proprietario: il filtro degli endpoint live controlla solo la partecipazione.
        var access = await _directory.GetAccessAsync(command.LiveSessionId, cancellationToken).ConfigureAwait(false);
        if (access.OwnerUserId != command.UserId)
        {
            throw new ForbiddenException("Only the session host can confirm an assistant proposal.");
        }

        var proposal = await _proposals.GetAsync(command.ProposalId, cancellationToken).ConfigureAwait(false);
        if (proposal is null || proposal.LiveSessionId != command.LiveSessionId)
        {
            throw new NotFoundException("AssistantProposal", command.ProposalId.ToString());
        }

        if (command.CorrectedPoints.HasValue && proposal.Kind != AssistantProposalKind.Score)
        {
            throw new ValidationException(nameof(command.CorrectedPoints), "Only a score proposal can be corrected.");
        }

        if (proposal.Status != AssistantProposalStatus.Pending)
        {
            return CurrentStateOf(proposal);
        }

        // 2. Prenotazione: da Pending ad Applying, salvata con xmin. È l'idempotenza della conferma.
        proposal.BeginApplying(command.UserId);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Un altro tap, o la scadenza di inizio turno, è arrivato prima: nessuna scrittura, e lo stato
            // attuale. L'entità tracciata qui è ormai vecchia: si rilegge senza tracciarla.
            var current = await _proposals.GetReadOnlyAsync(command.ProposalId, cancellationToken).ConfigureAwait(false)
                ?? throw new NotFoundException("AssistantProposal", command.ProposalId.ToString());
            return CurrentStateOf(current);
        }

        // Da qui la conferma non segue più l'annullamento della richiesta: una proposta prenotata va
        // portata a un esito anche se l'host chiude la pagina.
        return await CompleteAsync(proposal, command).ConfigureAwait(false);
    }

    private async Task<AssistantProposalDto> CompleteAsync(AssistantProposal proposal, ConfirmAssistantProposalCommand command)
    {
        // 3. Stato atteso: se la cella o l'indice di turno sono cambiati, la proposta è superata.
        AssistantSessionState state;
        try
        {
            state = await _directory.CreateReader(command.LiveSessionId, command.UserId)
                .GetStateAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nulla è stato scritto: la proposta non deve restare Applying, con una card che non si può più
            // né confermare né rifiutare. L'errore originale risale.
            proposal.MarkFailed(StateUnavailableCode, Now());
            await _unitOfWork.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        if (!MatchesWhatTheProposalExpected(proposal, state))
        {
            proposal.MarkStale(Now());
            await _unitOfWork.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            throw new ConflictException(StaleCode, "The game has changed since the assistant made this proposal. Ask again.");
        }

        // 4. Applicazione, attraverso i comandi della sessione live.
        try
        {
            if (proposal.Kind == AssistantProposalKind.Score)
            {
                await _applier.ApplyScoreAsync(
                    command.LiveSessionId,
                    command.UserId,
                    proposal.Payload.PlayerId ?? Guid.Empty,
                    proposal.Payload.Round ?? 1,
                    proposal.Payload.Dimension ?? string.Empty,
                    proposal.ScoreValueToWrite(command.CorrectedPoints),
                    CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                await _applier.AdvanceTurnAsync(command.LiveSessionId, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (AssistantProposalApplyException ex)
        {
            proposal.MarkFailed(ex.FailureCode, Now());
            await _unitOfWork.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            throw new ConflictException(ex.FailureCode, ex.Message);
        }

        // 5. Esito.
        if (command.CorrectedPoints is { } corrected && corrected != proposal.Payload.Points)
        {
            proposal.MarkEdited(corrected, Now());
        }
        else
        {
            proposal.MarkAccepted(Now());
        }

        await _unitOfWork.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        return AssistantProposalDto.From(proposal);
    }

    /// <summary>
    /// Un secondo tap su una proposta che si sta applicando, o già applicata, riceve lo stato attuale:
    /// l'host non deve vedere un errore per aver toccato due volte. Chiusa in altro modo, è un 409.
    /// </summary>
    private static AssistantProposalDto CurrentStateOf(AssistantProposal proposal) =>
        proposal.Status is AssistantProposalStatus.Applying or AssistantProposalStatus.Accepted or AssistantProposalStatus.Edited
            ? AssistantProposalDto.From(proposal)
            : throw new ConflictException(
                AssistantProposal.NotPendingCode, $"The proposal is {proposal.Status}: it can no longer be confirmed.");

    private static bool MatchesWhatTheProposalExpected(AssistantProposal proposal, AssistantSessionState state)
    {
        if (proposal.Kind == AssistantProposalKind.AdvanceTurn)
        {
            return state.CurrentTurnIndex == proposal.Expected.TurnIndex;
        }

        // Una cella che non esiste vale zero, come quando la proposta è nata.
        var cell = state.Scores
            .Where(s => s.PlayerId == proposal.Payload.PlayerId
                && s.Round == proposal.Payload.Round
                && string.Equals(s.Dimension, proposal.Payload.Dimension, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Value)
            .FirstOrDefault();
        return cell == (proposal.Expected.CellValue ?? 0);
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;
}
```

`KB/Infrastructure/SessionAssistant/LiveSessionProposalApplier.cs`:

```csharp
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// <see cref="IAssistantProposalApplier"/> sopra i comandi esistenti della sessione live (spec §5.3,
/// passo 4): <see cref="ConfirmScoreProposalCommand"/> verifica di nuovo l'host, scrive con
/// <c>RecordScore</c>, salva e trasmette ai partecipanti; <see cref="AdvanceLiveSessionTurnCommand"/>
/// passa il turno.
/// <para>
/// Ogni comando gira in uno scope suo: salva sul proprio <c>DbContext</c>, e se quel salvataggio fallisse
/// nello scope della richiesta le sue entità resterebbero tracciate, e il salvataggio successivo della
/// proposta le riproverebbe.
/// </para>
/// </summary>
internal sealed class LiveSessionProposalApplier(IServiceScopeFactory scopes) : IAssistantProposalApplier
{
    public Task ApplyScoreAsync(
        Guid liveSessionId, Guid confirmingUserId, Guid playerId, int round, string dimension, int value,
        CancellationToken cancellationToken) =>
        SendAsync(new ConfirmScoreProposalCommand(liveSessionId, confirmingUserId, playerId, round, dimension, value), cancellationToken);

    public Task AdvanceTurnAsync(Guid liveSessionId, CancellationToken cancellationToken) =>
        SendAsync(new AdvanceLiveSessionTurnCommand(liveSessionId), cancellationToken);

    private async Task SendAsync(IRequest command, CancellationToken cancellationToken)
    {
        var scope = scopes.CreateAsyncScope();
        await using var disposal = scope.ConfigureAwait(false);
        try
        {
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, cancellationToken).ConfigureAwait(false);
        }
        // I rami qui sotto sono i rifiuti che i due comandi lanciano PRIMA di salvare: nulla è stato scritto.
        // Ogni altra eccezione risale così com'è, perché può essere arrivata dopo il salvataggio.
        catch (ConflictException ex)
        {
            // Il dominio rifiuta per lo stato della sessione: conclusa, non in corso.
            throw new AssistantProposalApplyException(AssistantProposalApplyException.SessionStateInvalid, ex.Message, ex);
        }
        catch (NotFoundException ex)
        {
            // La sessione non c'è più.
            throw new AssistantProposalApplyException(AssistantProposalApplyException.SessionStateInvalid, ex.Message, ex);
        }
        catch (ForbiddenException ex)
        {
            // Chi conferma non è più l'host della sessione.
            throw new AssistantProposalApplyException(AssistantProposalApplyException.NotApplicable, ex.Message, ex);
        }
        catch (FluentValidation.ValidationException ex)
        {
            // Oggi i validator dei comandi senza risposta non girano; se un giorno girassero, il valore
            // rifiutato (per esempio una cella negativa) è una proposta non applicabile.
            throw new AssistantProposalApplyException(AssistantProposalApplyException.NotApplicable, ex.Message, ex);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new AssistantProposalApplyException(
                AssistantProposalApplyException.SessionStateInvalid,
                "The live session was changed by someone else while the proposal was being applied.",
                ex);
        }
        catch (DomainException ex)
        {
            // Il giocatore non è più attivo, la dimensione non è più abilitata.
            throw new AssistantProposalApplyException(AssistantProposalApplyException.NotApplicable, ex.Message, ex);
        }
    }
}
```

In `SessionAssistantServiceExtensions.cs` (con `using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;`):

```csharp
        // Singleton: non ha stato e apre uno scope per ogni scrittura.
        services.AddSingleton<IAssistantProposalApplier, LiveSessionProposalApplier>();
```

In `SessionAssistantRegistrationTests.cs`, in fondo al test (l'applicatore chiede solo `IServiceScopeFactory`, che il contenitore ha già):

```csharp
        scope.ServiceProvider.GetRequiredService<Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation.IAssistantProposalApplier>()
            .Should().NotBeNull();
```

- [ ] **Step 5: Verifica che i test passino**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ConfirmAssistantProposalTests|FullyQualifiedName~LiveSessionProposalApplierTests|FullyQualifiedName~SessionAssistantArchitectureTests|FullyQualifiedName~SessionAssistantRegistrationTests"`
Expected: PASS. Annota il numero. Se `ARefusalOfTheSession_BecomesAFailureCode` fallisce sulla riga `ConflictException` perché viene presa dal ramo `DomainException` (o viceversa), una delle due eredita dall'altra: metti per primo il `catch` del tipo più derivato e annotalo come `Ruling`.

- [ ] **Step 6: Verifica per perturbazione**

Con copia del file fuori dal repository, ripristino per copia e `touch`:

1. In `Handle` sposta `proposal.BeginApplying(...)` e il suo salvataggio **dopo** l'applicazione: `Confirm_ReservesFirst_ThenWritesTheCellPlusTheDelta_AndAccepts` deve fallire.
2. In `MatchesWhatTheProposalExpected` restituisci sempre `true`: `Confirm_WhenTheCellChanged_IsStaleAndDoesNotWrite` e `Confirm_ATurn_WhenTheTurnAlreadyMoved_IsStale` devono fallire.
3. Aggiungi al costruttore di `AssistantTurnRunner` un ultimo parametro **opzionale** `Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation.IAssistantProposalApplier? applier = null` (opzionale, così i test del turno compilano ancora): `OnlyTheConfirmHandler_ReceivesThePortThatWritesTheGame` deve fallire.
4. In `CompleteAsync` togli il `try`/`catch` intorno alla lettura dello stato: `Confirm_WhenTheStateCannotBeRead_FailsTheProposal_InsteadOfLeavingItApplying` deve fallire (la proposta resta `Applying`).

- [ ] **Step 7: Commit**

```bash
git add apps/api
git commit -m "feat(ai): conferma delle proposte dell'assistente (#4277)"
```

### Task 12: Il rifiuto e gli endpoint di conferma e rifiuto

**Files:**
- Create: `KB/Application/SessionAssistant/Confirmation/RejectAssistantProposalCommand.cs` (comando, validator, handler)
- Modify: `API/Routing/SessionAssistantEndpoints.cs`
- Test: `TESTS/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Confirmation/RejectAssistantProposalTests.cs`

**Interfaces:**
- Consumes: `AssistantProposal.Reject(Guid userId, DateTime now)`; `ConfirmAssistantProposalCommand` (Task 11); `AssistantProposalDto`.
- Produces:
  - `internal sealed record RejectAssistantProposalCommand(Guid LiveSessionId, Guid ProposalId, Guid UserId) : ICommand<AssistantProposalDto>`.
  - `POST /api/v1/live-sessions/{sessionId}/assistant/proposals/{proposalId}/confirm`, corpo opzionale `{ "points": int? }` → `200` con la proposta, oppure `400` (correzione su una proposta di turno), `403`, `404`, `409` (`proposal_stale`, `proposal_not_pending`, `session_state_invalid`, `proposal_not_applicable`), `422` (correzione fuori dai limiti).
  - `POST /api/v1/live-sessions/{sessionId}/assistant/proposals/{proposalId}/reject` → `200` con la proposta, oppure `403`, `404`, `409` (`proposal_not_pending`).

- [ ] **Step 1: Scrivi i test del rifiuto**

`TESTS/BoundedContexts/KnowledgeBase/Application/SessionAssistant/Confirmation/RejectAssistantProposalTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;

/// <summary>Spec 2026-10-11 §5.3: il rifiuto, con le stesse verifiche di proprietario e di stato della conferma.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4277")]
public sealed class RejectAssistantProposalTests
{
    private static readonly Guid Session = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Owner = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly Mock<IAssistantProposalRepository> _proposals = new();
    private readonly Mock<IAssistantSessionDirectory> _directory = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 20, 0, 0, TimeSpan.Zero));

    public RejectAssistantProposalTests()
    {
        _directory.Setup(d => d.GetAccessAsync(Session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantSessionAccess(Owner, "InProgress", true));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private AssistantProposal Proposal()
    {
        var proposal = AssistantProposal.ForScore(
            Session, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Marco, "Marco", "punti", 2, 5, 7, Now);
        _proposals.Setup(p => p.GetAsync(proposal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(proposal);
        return proposal;
    }

    private Task<AssistantProposalDto> RejectAsync(AssistantProposal proposal, Guid? user = null, Guid? session = null) =>
        new RejectAssistantProposalCommandHandler(_proposals.Object, _directory.Object, _unitOfWork.Object, _time)
            .Handle(new RejectAssistantProposalCommand(session ?? Session, proposal.Id, user ?? Owner), Ct);

    [Fact]
    public async Task Reject_ClosesAPendingProposal()
    {
        var proposal = Proposal();

        var dto = await RejectAsync(proposal);

        proposal.Status.Should().Be(AssistantProposalStatus.Rejected);
        proposal.ResolvedBy.Should().Be(Owner);
        proposal.ResolvedAt.Should().Be(Now);
        dto.Status.Should().Be("Rejected");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reject_ByAnyoneButTheOwner_IsForbidden()
    {
        var proposal = Proposal();

        var act = () => RejectAsync(proposal, user: Guid.NewGuid());

        await act.Should().ThrowAsync<ForbiddenException>();
        proposal.Status.Should().Be(AssistantProposalStatus.Pending);
    }

    [Fact]
    public async Task Reject_AProposalOfAnotherSession_IsNotFound()
    {
        var other = Guid.NewGuid();
        _directory.Setup(d => d.GetAccessAsync(other, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantSessionAccess(Owner, "InProgress", true));
        var proposal = Proposal();

        var act = () => RejectAsync(proposal, session: other);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ASecondTapOnReject_ReturnsTheRejectedProposal()
    {
        var proposal = Proposal();
        proposal.Reject(Owner, Now);

        var dto = await RejectAsync(proposal);

        dto.Status.Should().Be("Rejected");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reject_AProposalBeingApplied_IsNotPending()
    {
        // Rifiutare mentre la conferma è in corso non deve annullare una scrittura già avviata.
        var proposal = Proposal();
        proposal.BeginApplying(Owner);

        var act = () => RejectAsync(proposal);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("proposal_not_pending");
        proposal.Status.Should().Be(AssistantProposalStatus.Applying);
    }

    [Fact]
    public async Task Reject_ThatLosesTheRaceToAConfirm_IsNotPending()
    {
        var proposal = Proposal();
        var confirmed = AssistantProposal.ForScore(
            Session, proposal.ChatThreadId, proposal.MessageId, proposal.ProfileVersionId, Marco, "Marco", "punti", 2, 5, 7, Now);
        confirmed.BeginApplying(Owner);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateConcurrencyException("xmin"));
        _proposals.Setup(p => p.GetReadOnlyAsync(proposal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(confirmed);

        var act = () => RejectAsync(proposal);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("proposal_not_pending");
    }

    [Fact]
    public void TheValidator_WantsTheIds()
    {
        var validator = new RejectAssistantProposalCommandValidator();

        validator.TestValidate(new RejectAssistantProposalCommand(Session, Guid.NewGuid(), Owner)).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new RejectAssistantProposalCommand(Session, Guid.Empty, Owner))
            .ShouldHaveValidationErrorFor(c => c.ProposalId);
    }
}
```

- [ ] **Step 2: Verifica che non compili**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~RejectAssistantProposalTests"`
Expected: `CS0246` su `RejectAssistantProposalCommand`, `RejectAssistantProposalCommandHandler`, `RejectAssistantProposalCommandValidator`.

- [ ] **Step 3: Scrivi il comando di rifiuto**

`KB/Application/SessionAssistant/Confirmation/RejectAssistantProposalCommand.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

#pragma warning disable MA0048 // File name must match type name - command, validator and handler
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;

/// <summary>Spec 2026-10-11 §5.3: l'host rifiuta una proposta dell'assistente. Non scrive la partita.</summary>
internal sealed record RejectAssistantProposalCommand(Guid LiveSessionId, Guid ProposalId, Guid UserId)
    : ICommand<AssistantProposalDto>;

internal sealed class RejectAssistantProposalCommandValidator : AbstractValidator<RejectAssistantProposalCommand>
{
    public RejectAssistantProposalCommandValidator()
    {
        RuleFor(c => c.LiveSessionId).NotEmpty();
        RuleFor(c => c.ProposalId).NotEmpty();
        RuleFor(c => c.UserId).NotEmpty();
    }
}

internal sealed class RejectAssistantProposalCommandHandler(
    IAssistantProposalRepository proposals,
    IAssistantSessionDirectory directory,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<RejectAssistantProposalCommand, AssistantProposalDto>
{
    public async Task<AssistantProposalDto> Handle(RejectAssistantProposalCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await directory.GetAccessAsync(command.LiveSessionId, cancellationToken).ConfigureAwait(false);
        if (access.OwnerUserId != command.UserId)
        {
            throw new ForbiddenException("Only the session host can reject an assistant proposal.");
        }

        var proposal = await proposals.GetAsync(command.ProposalId, cancellationToken).ConfigureAwait(false);
        if (proposal is null || proposal.LiveSessionId != command.LiveSessionId)
        {
            throw new NotFoundException("AssistantProposal", command.ProposalId.ToString());
        }

        if (proposal.Status != AssistantProposalStatus.Pending)
        {
            return CurrentStateOf(proposal);
        }

        proposal.Reject(command.UserId, timeProvider.GetUtcNow().UtcDateTime);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Una conferma o un altro rifiuto è arrivato prima: vale ciò che ha lasciato.
            var current = await proposals.GetReadOnlyAsync(command.ProposalId, cancellationToken).ConfigureAwait(false)
                ?? throw new NotFoundException("AssistantProposal", command.ProposalId.ToString());
            return CurrentStateOf(current);
        }

        return AssistantProposalDto.From(proposal);
    }

    /// <summary>Un secondo tap su una proposta già rifiutata la restituisce; ogni altro stato chiuso, o in corso, è un 409.</summary>
    private static AssistantProposalDto CurrentStateOf(AssistantProposal proposal) =>
        proposal.Status == AssistantProposalStatus.Rejected
            ? AssistantProposalDto.From(proposal)
            : throw new ConflictException(
                AssistantProposal.NotPendingCode, $"The proposal is {proposal.Status}: it can no longer be rejected.");
}
```

- [ ] **Step 4: Aggiungi le due rotte**

In `SessionAssistantEndpoints.cs`, dentro `MapSessionAssistantEndpoints`, prima del `return group;`:

```csharp
        group.MapPost("/live-sessions/{sessionId}/assistant/proposals/{proposalId}/confirm", HandleConfirm)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<AssistantProposalDto>(200)
            .Produces(400)  // una correzione su una proposta di turno
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .Produces(409)  // proposal_stale · proposal_not_pending · session_state_invalid · proposal_not_applicable
            .Produces(422)  // una correzione fuori dai limiti
            .WithTags("LiveSessions")
            .WithSummary("Confirm a proposal of the in-game assistant")
            .WithDescription("Host only. Applies the proposal to the game: the only write path of the assistant. A second tap returns the current state.");

        group.MapPost("/live-sessions/{sessionId}/assistant/proposals/{proposalId}/reject", HandleReject)
            .RequireAuthenticatedUser()
            .RequireLiveSessionParticipant()
            .Produces<AssistantProposalDto>(200)
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .Produces(409)  // proposal_not_pending
            .WithTags("LiveSessions")
            .WithSummary("Reject a proposal of the in-game assistant");
```

In fondo alla classe, prima di `AskSessionAssistantRequest`:

```csharp
    private static async Task<IResult> HandleConfirm(
        [FromRoute] Guid sessionId,
        [FromRoute] Guid proposalId,
        [FromBody] ConfirmAssistantProposalRequest? request,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var proposal = await mediator.Send(
            new ConfirmAssistantProposalCommand(sessionId, proposalId, httpContext.User.GetUserId(), request?.Points),
            cancellationToken).ConfigureAwait(false);
        return Results.Ok(proposal);
    }

    private static async Task<IResult> HandleReject(
        [FromRoute] Guid sessionId, [FromRoute] Guid proposalId, HttpContext httpContext, IMediator mediator, CancellationToken cancellationToken)
    {
        var proposal = await mediator.Send(
            new RejectAssistantProposalCommand(sessionId, proposalId, httpContext.User.GetUserId()), cancellationToken)
            .ConfigureAwait(false);
        return Results.Ok(proposal);
    }

    /// <summary>Il corpo è opzionale: senza, si conferma l'incremento proposto.</summary>
    internal sealed record ConfirmAssistantProposalRequest(int? Points);
```

con `using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;` in testa al file.

- [ ] **Step 5: Verifica che i test passino e che l'API si costruisca**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~RejectAssistantProposalTests|FullyQualifiedName~ConfirmAssistantProposalTests"`
Expected: PASS. Annota il numero.

Run: `dotnet build apps/api/src/Api/Api.csproj -c Release` — Expected: 0 avvisi, 0 errori.

- [ ] **Step 6: Commit**

```bash
git add apps/api
git commit -m "feat(ai): rifiuto delle proposte e rotte di conferma e rifiuto (#4277)"
```

### Task 13: Conferma e rifiuto sul database vero

**Files:**
- Test: `TESTS/Integration/KnowledgeBase/SessionAssistant/AssistantProposalConfirmationEndpointTests.cs`
- Modify: `docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md` (§5.2 e §5.3, scostamenti S7, S8, S10, S11)

**Interfaces:**
- Consumes: le tre rotte dell'assistente; `SessionAssistantHostFixture` (`Llm`, `Hub`), `SessionAssistantTestData`, `LiveTable` (Task 6); `RecordLiveSessionScoreCommand`, `AdvanceLiveSessionTurnCommand`, `CompleteLiveSessionCommand`, `GetLiveSessionQuery`; l'evento SignalR `ScoreConfirmed` di `ConfirmScoreProposalCommandHandler`.
- Produces: i test di integrazione della riga «Conferma» della spec §7.2.

Questi test **sono** il «rosso prima» della fetta: i due sui tap concorrenti e quello sulla cella cambiata vanno visti fallire per perturbazione (Step 3), perché il codice dei Task 11 e 12 li fa già passare.

- [ ] **Step 1: Scrivi i test**

`TESTS/Integration/KnowledgeBase/SessionAssistant/AssistantProposalConfirmationEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Api.Tests.Integration.KnowledgeBase.SessionAssistant.ScriptedChatGateway;

namespace Api.Tests.Integration.KnowledgeBase.SessionAssistant;

/// <summary>
/// Spec 2026-10-11 §5.3 e §7.2, riga «Conferma», su Postgres vero: una conferma scrive la cella giusta;
/// una proposta superata non scrive; chi non è il proprietario non conferma; una sessione conclusa
/// rifiuta; un doppio tap si applica una volta sola.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4277")]
public sealed class AssistantProposalConfirmationEndpointTests(SessionAssistantHostFixture host)
    : IClassFixture<SessionAssistantHostFixture>, IAsyncLifetime
{
    private const string ScoreConfirmed = "ScoreConfirmed";

    private HttpClient _client = null!;
    private Guid _owner;
    private string _token = null!;
    private LiveTable _table = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Ogni test parte da una partita avviata con tre giocatori, Marco a 7 punti nel primo round, e l'assistente abilitato.</summary>
    public async ValueTask InitializeAsync()
    {
        await host.BeginTestAsync();
        host.Llm.Reset();
        _client = host.Factory.CreateClient();
        (_owner, _token) = await SessionAssistantTestData.SeedHostAsync(host.Factory, Ct);
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        _table = await SessionAssistantTestData.CreateTableAsync(host.Factory, _owner, start: true, Ct, "Marco", "Giulia", "Luca");
        await SessionAssistantTestData.SendAsync(
            host.Factory, new RecordLiveSessionScoreCommand(_table.SessionId, _table.Players["Marco"], 1, "punti", 7), Ct);
        host.Hub.Reset();
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    // ─── Supporto ───────────────────────────────────────────────────────────────

    /// <summary>Un turno dell'assistente che lascia una proposta: restituisce il suo id.</summary>
    private async Task<Guid> ProposeAsync(string tool, string argumentsJson, string question)
    {
        host.Llm.Reset(Calls(tool, argumentsJson), Says("Proposta pronta: confermala dalla card."));
        var (status, events, body) = await SessionAssistantTestData.AskAsync(_client, _token, _table.SessionId, question, Ct);
        status.Should().Be(HttpStatusCode.OK, body);
        return events.Single(e => e.GetProperty("type").GetString() == "proposal")
            .GetProperty("proposal").GetProperty("id").GetGuid();
    }

    private Task<Guid> ProposeFivePointsToMarcoAsync() =>
        ProposeAsync("proponi_punteggio", """{"giocatore":"Marco","punti":5}""", "Marco fa 5 punti");

    private Task<Guid> ProposeTheTurnAsync() => ProposeAsync("proponi_turno", "{}", "passa il turno");

    private async Task<(HttpStatusCode Status, string Body)> PostAsync(
        string action, Guid proposalId, object? body = null, string? token = null, Guid? sessionId = null)
    {
        var uri = $"/api/v1/live-sessions/{sessionId ?? _table.SessionId}/assistant/proposals/{proposalId}/{action}";
        var request = TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Post, uri, token ?? _token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        var response = await _client.SendAsync(request, Ct);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(Ct));
    }

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement.Clone();

    private Task<LiveSessionDto> SessionAsync() =>
        SessionAssistantTestData.SendAsync(host.Factory, new GetLiveSessionQuery(_table.SessionId), Ct);

    private async Task<int> MarcoFirstRoundAsync() =>
        (await SessionAsync()).RoundScores
            .Single(s => s.PlayerId == _table.Players["Marco"] && s.Round == 1 && s.Dimension == "punti").Value;

    private async Task<AssistantProposal> StoredAsync(Guid proposalId)
    {
        await using var scope = host.Factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().GetReadOnlyAsync(proposalId, Ct))!;
    }

    // ─── La conferma scrive ─────────────────────────────────────────────────────

    [Fact]
    public async Task Confirm_WritesTheCellPlusTheDelta_AndTellsTheParticipants()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();

        var (status, body) = await PostAsync("confirm", proposalId);

        status.Should().Be(HttpStatusCode.OK, body);
        var proposal = Json(body);
        proposal.GetProperty("status").GetString().Should().Be("Accepted");
        proposal.GetProperty("cellValueBefore").GetInt32().Should().Be(7);
        proposal.GetProperty("resultingValue").GetInt32().Should().Be(12);
        (await MarcoFirstRoundAsync()).Should().Be(12, "RecordScore sostituisce la cella: 7 più l'incremento");
        host.Hub.Count(ScoreConfirmed).Should().Be(1);
        var stored = await StoredAsync(proposalId);
        stored.Status.Should().Be(AssistantProposalStatus.Accepted);
        stored.ResolvedBy.Should().Be(_owner);
    }

    [Fact]
    public async Task Confirm_WithACorrection_WritesTheCorrectedDelta_AndKeepsBoth()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();

        var (status, body) = await PostAsync("confirm", proposalId, new { points = 3 });

        status.Should().Be(HttpStatusCode.OK, body);
        Json(body).GetProperty("status").GetString().Should().Be("Edited");
        (await MarcoFirstRoundAsync()).Should().Be(10);
        var stored = await StoredAsync(proposalId);
        stored.Payload.Points.Should().Be(5, "la proposta originale resta leggibile");
        stored.ConfirmedPayload!.Points.Should().Be(3);
    }

    [Fact]
    public async Task Confirm_ATurnProposal_AdvancesTheTurn()
    {
        var proposalId = await ProposeTheTurnAsync();

        var (status, body) = await PostAsync("confirm", proposalId);

        status.Should().Be(HttpStatusCode.OK, body);
        var session = await SessionAsync();
        session.CurrentTurnIndex.Should().Be(2);
        session.CurrentTurnPlayerId.Should().Be(_table.Players["Giulia"]);
    }

    // ─── La conferma non scrive ─────────────────────────────────────────────────

    /// <summary>Review Focus 4, sul database vero.</summary>
    [Fact]
    public async Task Confirm_WhenTheCellChanged_IsStale_AndDoesNotWrite()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();
        await SessionAssistantTestData.SendAsync(
            host.Factory, new RecordLiveSessionScoreCommand(_table.SessionId, _table.Players["Marco"], 1, "punti", 9), Ct);

        var (status, body) = await PostAsync("confirm", proposalId);

        status.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("proposal_stale");
        (await MarcoFirstRoundAsync()).Should().Be(9, "resta ciò che ha scritto l'host a mano");
        host.Hub.Count(ScoreConfirmed).Should().Be(0);
        (await StoredAsync(proposalId)).Status.Should().Be(AssistantProposalStatus.Stale);
    }

    [Fact]
    public async Task Confirm_ATurnProposal_WhenTheTurnAlreadyMoved_IsStale()
    {
        var proposalId = await ProposeTheTurnAsync();
        await SessionAssistantTestData.SendAsync(host.Factory, new AdvanceLiveSessionTurnCommand(_table.SessionId), Ct);

        var (status, body) = await PostAsync("confirm", proposalId);

        status.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("proposal_stale");
        (await SessionAsync()).CurrentTurnIndex.Should().Be(2, "il turno è passato una volta sola, a mano");
    }

    [Fact]
    public async Task Confirm_ByAParticipantWhoIsNotTheOwner_IsForbidden()
    {
        var (guest, guestToken) = await SessionAssistantTestData.SeedHostAsync(host.Factory, Ct);
        await SessionAssistantTestData.SendAsync(
            host.Factory, new AddPlayerToLiveSessionCommand(_table.SessionId, "Ospite", PlayerColor.Yellow, UserId: guest), Ct);
        var proposalId = await ProposeFivePointsToMarcoAsync();

        var (confirm, confirmBody) = await PostAsync("confirm", proposalId, token: guestToken);
        var (reject, rejectBody) = await PostAsync("reject", proposalId, token: guestToken);

        confirm.Should().Be(HttpStatusCode.Forbidden, confirmBody);
        reject.Should().Be(HttpStatusCode.Forbidden, rejectBody);
        (await MarcoFirstRoundAsync()).Should().Be(7);
        (await StoredAsync(proposalId)).Status.Should().Be(AssistantProposalStatus.Pending);
    }

    [Fact]
    public async Task Confirm_OnACompletedSession_IsSessionStateInvalid_AndTheProposalIsFailed()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();
        await SessionAssistantTestData.SendAsync(host.Factory, new CompleteLiveSessionCommand(_table.SessionId), Ct);

        var (status, body) = await PostAsync("confirm", proposalId);

        status.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("session_state_invalid").And.NotContain("proposal_stale");
        var stored = await StoredAsync(proposalId);
        stored.Status.Should().Be(AssistantProposalStatus.Failed);
        stored.FailureCode.Should().Be("session_state_invalid");
        (await MarcoFirstRoundAsync()).Should().Be(7);
    }

    [Fact]
    public async Task Confirm_WithTheIdOfAnotherSession_IsNotFound()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();
        var other = await SessionAssistantTestData.CreateTableAsync(host.Factory, _owner, start: true, Ct, "Anna");

        var (status, body) = await PostAsync("confirm", proposalId, sessionId: other.SessionId);

        status.Should().Be(HttpStatusCode.NotFound, body);
        (await StoredAsync(proposalId)).Status.Should().Be(AssistantProposalStatus.Pending);
    }

    // ─── Il doppio tap (Review Focus 3) ─────────────────────────────────────────

    [Fact]
    public async Task TwoConcurrentConfirms_OfAnAdvanceTurn_AdvanceOnce()
    {
        var proposalId = await ProposeTheTurnAsync();

        var taps = await Task.WhenAll(PostAsync("confirm", proposalId), PostAsync("confirm", proposalId));

        taps.Should().OnlyContain(tap => tap.Status == HttpStatusCode.OK, "il secondo tap riceve lo stato attuale, non un errore");
        (await SessionAsync()).CurrentTurnIndex.Should().Be(2, "il turno è passato una volta sola");
        (await StoredAsync(proposalId)).Status.Should().Be(AssistantProposalStatus.Accepted);
    }

    /// <summary>
    /// Su un punteggio il doppio tap non si vede dalla cella, perché <c>RecordScore</c> sostituisce: 7 più 5
    /// fa 12 anche se la scrittura parte due volte. Si vede dagli eventi trasmessi ai partecipanti.
    /// </summary>
    [Fact]
    public async Task TwoConcurrentConfirms_OfAScore_WriteOnce_CountedOnTheBroadcast()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();

        var taps = await Task.WhenAll(PostAsync("confirm", proposalId), PostAsync("confirm", proposalId));

        taps.Should().OnlyContain(tap => tap.Status == HttpStatusCode.OK);
        host.Hub.Count(ScoreConfirmed).Should().Be(1);
        (await MarcoFirstRoundAsync()).Should().Be(12);
    }

    [Fact]
    public async Task ASecondConfirm_AfterTheFirst_ReturnsTheSameState_AndWritesNothingMore()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();
        await PostAsync("confirm", proposalId);

        var (status, body) = await PostAsync("confirm", proposalId);

        status.Should().Be(HttpStatusCode.OK, body);
        Json(body).GetProperty("status").GetString().Should().Be("Accepted");
        host.Hub.Count(ScoreConfirmed).Should().Be(1);
    }

    // ─── Il rifiuto ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_ClosesTheProposal_AndItCanNoLongerBeConfirmed()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();

        var (reject, rejectBody) = await PostAsync("reject", proposalId);
        var (confirm, confirmBody) = await PostAsync("confirm", proposalId);

        reject.Should().Be(HttpStatusCode.OK, rejectBody);
        Json(rejectBody).GetProperty("status").GetString().Should().Be("Rejected");
        confirm.Should().Be(HttpStatusCode.Conflict, confirmBody);
        confirmBody.Should().Contain("proposal_not_pending");
        (await MarcoFirstRoundAsync()).Should().Be(7);
        host.Hub.Count(ScoreConfirmed).Should().Be(0);
    }

    [Fact]
    public async Task AProposalExpiredByTheNextTurn_CanNoLongerBeConfirmed()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();
        host.Llm.Reset(Says("Tocca a Marco."));
        await SessionAssistantTestData.AskAsync(_client, _token, _table.SessionId, "a chi tocca?", Ct);

        var (status, body) = await PostAsync("confirm", proposalId);

        status.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("proposal_not_pending");
        (await MarcoFirstRoundAsync()).Should().Be(7);
    }
}
```

- [ ] **Step 2: Esegui i test**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AssistantProposalConfirmationEndpointTests"`
Expected: PASS. Annota numero e tempo. Esegui la classe **tre volte**: i due test sui tap concorrenti dipendono da una corsa vera, e un verde su tre rossi possibili non basta. Un fallimento intermittente di quei due è un difetto della prenotazione, non un test instabile: fermati e leggi quale delle due richieste ha scritto.

- [ ] **Step 3: Verifica per perturbazione**

Con copia del file fuori dal repository, ripristino per copia e `touch`. Dopo ogni perturbazione esegui la classe del Task 13:

1. In `AssistantProposalConfiguration` togli `.IsConcurrencyToken()` dalla proprietà `Xmin`: la prenotazione non perde più sulla concorrenza. Expected, in almeno una di tre esecuzioni: `TwoConcurrentConfirms_OfAnAdvanceTurn_AdvanceOnce` FAIL (indice di turno 3, oppure un `409` al secondo tap, perché a fermare il doppio passaggio resta solo la concorrenza della sessione) e `TwoConcurrentConfirms_OfAScore_WriteOnce_CountedOnTheBroadcast` FAIL (due eventi trasmessi). Se restano verdi in tutte e tre, le due richieste non si sovrappongono: porta i tap da due a otto in entrambi i test (`Enumerable.Range(0, 8).Select(_ => PostAsync("confirm", proposalId))`), riesegui la perturbazione, e tieni gli otto tap anche dopo il ripristino.
2. In `ConfirmAssistantProposalCommandHandler.MatchesWhatTheProposalExpected` restituisci sempre `true`. Expected: `Confirm_WhenTheCellChanged_IsStale_AndDoesNotWrite` FAIL (la cella diventa 12) e `Confirm_ATurnProposal_WhenTheTurnAlreadyMoved_IsStale` FAIL (indice 3).
3. In `LiveSessionProposalApplier.SendAsync` togli il `catch (ConflictException ex)`. Expected: `Confirm_OnACompletedSession_IsSessionStateInvalid_AndTheProposalIsFailed` FAIL (la proposta resta `Applying`, e il corpo non porta `session_state_invalid`).

- [ ] **Step 4: Riporta gli scostamenti nella spec**

In `docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md`:

1. §5.2, capoverso «**Scadenza**», sostituiscilo con:

> **Scadenza**: una proposta ancora `Pending` diventa `Expired` quando inizia il turno successivo dell'assistente, con un solo `UPDATE` eseguito subito: così una conferma che arriva nello stesso istante non fa fallire il turno, trova la proposta non più `Pending`. Alla chiusura della sessione le proposte rimaste `Pending` non si toccano: una conferma tardiva viene comunque rifiutata dal dominio (`session_state_invalid`), e la pulizia arriva con le metriche della fetta A6.

2. §5.3, passo 2, sostituisci «Un secondo tap concorrente perde sulla concorrenza e riceve lo stato attuale, senza scrivere.» con «Un secondo tap perde sulla concorrenza, non scrive, e riceve lo stato attuale: `200` se la proposta si sta applicando o è stata applicata (`Applying`, `Accepted`, `Edited`), `409 proposal_not_pending` se è stata chiusa in altro modo (`Rejected`, `Expired`, `Stale`, `Failed`).»

3. §5.3, passo 5, sostituisci da «Se il dominio rifiuta» fino a «distinto da `proposal_stale`.» con:

> Se il dominio rifiuta, la proposta diventa `Failed` con il codice e la risposta è `409` con lo stesso codice, distinto da `proposal_stale`: `session_state_invalid` quando è lo stato della sessione a non permettere la scrittura (`RecordScore` la vuole `InProgress` o `Paused`, `AdvanceTurn` la vuole `InProgress`), quando la sessione non esiste più, o quando qualcun altro l'ha modificata nello stesso istante; `proposal_not_applicable` quando la proposta non vale più per questa partita (il giocatore è uscito, la dimensione non è più abilitata). In tutti questi casi nulla è stato scritto, e l'host richiede. Se dopo la prenotazione non si riesce a leggere lo stato della partita, la proposta diventa `Failed` con `session_state_unavailable` e l'errore risale.

4. §5.3, capoverso che comincia con «Il comando di dominio salva da solo», sostituisci l'ultima frase («Se il processo cade … lo segnala nei log.») con:

> Il comando di dominio gira in uno scope suo, così un suo salvataggio fallito non resta nel change tracker della proposta. Dopo la prenotazione la conferma non segue più l'annullamento della richiesta: un host che chiude la pagina non lascia una proposta a metà. Se il processo cade fra il passo 4 e il 5, la proposta resta `Applying`: è un caso raro e visibile, che il turno successivo dell'assistente segnala nei log (la proposta non ha un istante di prenotazione, quindi il controllo non usa una soglia di tempo).

5. §5.2, riga `status` della tabella: dopo «da `Pending` anche `Rejected` · `Expired` · `Stale`» aggiungi «; `Stale` anche da `Applying`, perché lo stato atteso si controlla dopo la prenotazione (§5.3)».

6. §5.3, ultima riga («Il rifiuto è `POST …/proposals/{pid}/reject`, con le stesse verifiche di proprietario e di stato.»): aggiungi «Un secondo tap sul rifiuto riceve la proposta già rifiutata, come un secondo tap sulla conferma riceve l'esito della prima: anche quando porta una correzione diversa.»

7. §5.1, tabella degli strumenti, riga `proponi_punteggio`: sostituisci «Punti interi» con «Punti interi, entro ±100.000 (oltre, un errore tipizzato)».

- [ ] **Step 5: Commit**

```bash
git add apps/api docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md
git commit -m "test(ai): conferma e rifiuto sul database vero (#4277)"
```

### Chiusura della fetta A3b

- [ ] **Suite unit, integrazione della fetta e build Release**

```bash
dotnet build apps/api/src/Api/Api.csproj -c Release
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "(BoundedContext=KnowledgeBase|BoundedContext=Architecture)&Category=Unit" > "$TEMP/a3b-unit.log" 2>&1; tail -5 "$TEMP/a3b-unit.log"
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Integration.KnowledgeBase.SessionAssistant" > "$TEMP/a3b-integration.log" 2>&1; tail -5 "$TEMP/a3b-integration.log"
```

Expected: build con 0 avvisi; nella suite unit solo i cinque falliti noti; integrazione tutta verde.

- [ ] **Review dell'intero branch**, PR verso `main-dev` (`feat(ai): conferma e rifiuto delle proposte dell'assistente (#4277)`), CI, squash merge, pulizia; poi il run `dev-async` su `main-dev`, come per A3a.
- [ ] **Issue #4277**: DoD spuntata e chiusa. **Epic #4238**: A2 e A3 fatte; restano #4270 e la migrazione che abilita l'assistente, A4 (frontend), A5, A6, A7.
- [ ] **Memoria**: aggiorna `session-assistant-pilot-state.md` con lo stato dopo il Piano 2.

---

## Fuori dal piano

Ciò che la spec nomina e questo piano non fa, con il motivo e dove va.

| Cosa | Perché non qui | Dove |
|---|---|---|
| La migrazione di dati che pubblica la versione del profilo con l'assistente abilitato | Il testo e l'insieme di strumenti da pubblicare (tre, senza `cerca_regole`) non sono ancora stati misurati sul set alla versione 4 | Dopo #4270, come passo a sé. Il jsonb si scrive con le chiavi del Task 3; la capacità del modello la dichiara già il seed |
| Endpoint admin per dichiarare o togliere `SupportsToolCalling` | Il seed basta al pilota; la pagina admin del profilo è la fetta 6c di ADR-095 | Con la 6c |
| Scadenza delle proposte `Pending` alla chiusura della sessione, e loro pulizia se la sessione viene cancellata | La conferma tardiva è già rifiutata dal dominio; serve solo alla pulizia e ai conteggi | A6, con le metriche |
| Metriche (`loop_exhausted`, `claimed_action_without_proposal`, tasso di accettazione) | Sono il contenuto della fetta A6 | A6. I punti in cui nascono sono `AssistantTurnRunner.FinalText` e gli esiti della conferma |
| `parts` dei messaggi in `MessagesJson` | Il legame messaggio → proposte sta già in `message_id`; le `parts` servono alle citazioni | A5 |
| Suite di contratto astratta per `IChatThreadRepository` | Serve all'adattatore Cosmos, che vive sulla linea `azure/main` | Sottoprogetto §10.2. Qui i test nuovi stanno in `ChatThreadRepositoryIntegrationTests` |
| Due proposte sulla stessa cella nello stesso turno, uguali (il modello ripete la chiamata) o diverse («5 punti e 3 di bonus») | Il sink non le unisce: le proposte di troppo le conta il set di valutazione, e unirle in produzione nasconderebbe ciò che il gate misura. Dopo la conferma della prima, la seconda è superata: l'host riceve `proposal_stale` e deve richiedere. È sicuro, non comodo | Da osservare nel pilota |
| L'id della sessione sulle chiamate al gateway (spec §6.2) | `LlmChatRequest` non ha il campo; serve solo al costo per partita | A6 |
| L'ordine di turno impostato a mano nel round derivato | `LiveSessionDto` non espone l'ordine di turno: il lettore conta i giocatori attivi non spettatori, che coincidono con l'ordine predefinito | Un campo in più nel DTO di GameManagement, se il pilota usa `SetTurnOrder` |
| Secondo tentativo automatico quando la sessione cambia nello stesso istante della conferma | La corsa è rara: oggi la proposta diventa `Failed` e l'host richiede | Da osservare nel pilota |
| Una modifica a mano fra il controllo dello stato atteso e la scrittura | Sono due letture distinte, in due scope: una scrittura caduta in quell'istante viene sovrascritta. La finestra è di millisecondi | Si chiude solo portando il controllo dentro il comando della sessione |
| Blocco di due turni concorrenti sulla stessa sessione | La spec lo esclude (§6.3): il frontend disabilita l'invio, e due turni producono solo proposte | A4 |
| Quota per piano | Arriva con la fetta 5g di ADR-095 | 5g |

## Autoverifica del piano

- **Copertura della spec.** §3.2 profilo → Task 3; catalogo → Task 4; pubblicazione → Task 5; test architetturali → Task 10 e 11. §5.1 round → Task 6. §5.2 proposta → Task 1, 2, 8. §5.3 conferma e rifiuto → Task 11, 12, 13. §6.2 turno, eventi, storia, thread → Task 7, 9, 10. §6.3 limiti ed errori → Task 9 (tetto, provider, scadenza, giri esauriti, azione dichiarata). §7.2 riga «Dominio» → Task 1 e 5; «Strumenti» → già in A1, più Task 6 per il round; «Architettura» → Task 10 e 11; «Conferma» → Task 13.
- **Review Focus.** 1 → Task 4; 2 → Task 3; 3 → Task 2, 11, 13; 4 → Task 11, 13; 5 → Task 9; 6 → Task 6; 7 → Task 7; 8 → Task 11; 9 → Task 7.
- **Review.** Il piano è stato rivisto contro il codice da due revisori indipendenti prima dell'esecuzione; le loro correzioni sono in questo testo.
- **Tipi fra i task.** `IAssistantProposalRepository`: `GetAsync`, `GetReadOnlyAsync`, `ListOpenBySessionAsync`, `ListByThreadAsync`, `AddAsync` (Task 2), `ExpirePendingBySessionAsync` (Task 8). `AssistantProposalDto` nasce nel Task 8 nello spazio dei nomi `…SessionAssistant.Turn` ed è usato dai Task 9, 11 e 12. `IAssistantSessionDirectory` nasce nel Task 6 ed è usato dai Task 9, 11 e 12. `AgentProfile.AssistantModelWithoutToolsCode` nasce nel Task 5 ed è usato dal Task 9.

## Dopo l'esecuzione

Il piano è stato eseguito in tre fette: A2 (#4275, PR #4289), A3a (#4276, PR #4297), A3b (#4277). Dove il codice consegnato differisce dal testo dei task, per decisione presa in esecuzione o per una correzione chiesta dalla review dell'intero branch:

- **Task 1 e 3 (A2).** `ScoreValueToWrite` applica il limite dei punti anche alla correzione dell'host, e rifiuta una proposta di punteggio senza la sua cella attesa. `AgentProfileContent.Create` e `Restore` prendono la sezione dell'assistente come argomento obbligatorio.
- **Task 7 (A3a).** Il thread dell'assistente è escluso anche dal cruscotto (conteggio e attività recente) e dalla timeline delle attività, che leggono `ChatThreads` senza passare dal repository.
- **Task 9 (A3a).** `AskSessionAssistantCommandHandler.Handle` è diviso in controllo dell'argomento e iteratore privato, come `StreamQaQueryHandler`. La scadenza delle proposte precedenti vale anche quando il turno poi fallisce, ed è contratto: la annuncia il primo evento `status`.
- **Task 11 (A3b).** Il valore da scrivere si calcola prima della prenotazione. La porta di scrittura porta lo stato su cui la proposta è nata (`AssistantScoreWrite`, `expectedTurnIndex`) e i due comandi della sessione lo ricontrollano sull'aggregato che stanno per salvare. Chiude la riga «Una modifica a mano fra il controllo dello stato atteso e la scrittura» di «Fuori dal piano», e corregge la riga «Due proposte sulla stessa cella»: era sicuro solo confermandole una dopo l'altra.
- **Task 13 (A3b).** I test dei tap concorrenti si chiamano `ConcurrentConfirms_…` e mandano otto richieste: con due, tolto il token di concorrenza, restavano verdi. In più: di più proposte uguali se ne conferma una sola, e due proposte diverse confermate una dentro l'altra.
