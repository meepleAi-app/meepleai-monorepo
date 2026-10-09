# Spec — Mechanic claims v3: regole defettibili come dati (`Kind`, `Priority`, `Overrides`, `Trigger`)

**Data**: 2026-10-08 · **Stato**: approvata in brainstorming (design a sezioni), in attesa di piano di implementazione
**Decisioni a monte**: ADR-092 (Arbitro: Python validatore, C# risposta) · ADR-093 (confine IP parser esterni, non impatta questa spec) · ADR-051 (review gate per claim) · ADR-084 (forma canonica = claim; card = proiezione in discesa; chiavi JSON congelate) · ADR-088 (claim iniettati nel prompt, non braccio RRF)
**Origine**: revisione a pannello `docs/for-developers/research/2026-10-08-neuro-symbolic-architecture-spec-panel.md` §2.3 e roadmap §4 punto 7.
**Convenzione**: path relativi ad `apps/api/src/Api/` salvo nota; nessun conteggio volatile in prosa.

---

## 1. Obiettivo e perimetro

Oggi un claim è una frase approvata con citazioni (`MechanicCardContent` v2). Non dice se è una regola generale, un'eccezione o un esempio, non ha una precedenza e non dice quando si applica. Il prompt li riceve in ordine di sezione e l'Arbitro non può usarli per un verdetto.

Questa spec aggiunge quattro campi al claim, li fa proporre dall'LLM e confermare dal revisore, li proietta nella card e li usa in due consumatori: l'ordinamento del blocco `[Verified Rules]` e il validatore Python di ADR-092.

**In scope**: modello dati e migrazione; prompt v1.2.0 e guardia T5; review admin; card `schema_version` 3; renderer ordinato; contratto `/validate` e kernel di risoluzione; riestrazione dei giochi esistenti; test e flag.
**Fuori scope**: filtro del retrieval per `Kind`; pilota ASP/clingo; fusione automatica degli alias delle entità (roadmap §4 punto 4); UI player-facing delle regole.

## 2. Modello dati (claim, forma canonica)

Entità `MechanicClaim` (`BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicClaim.cs`) e tabella `mechanic_claims` (`Infrastructure/Entities/SharedGameCatalog/MechanicClaimEntity.cs`, configurazione `Infrastructure/Configurations/SharedGameCatalog/MechanicClaimEntityConfiguration.cs`).

| Campo | Tipo | Persistenza | Default | Note |
|---|---|---|---|---|
| `Kind` | enum `MechanicClaimKind { Rule = 0, Exception = 1, Clarification = 2, Example = 3 }` | colonna `kind` int, indice | `Rule` | il "flavor" non è un claim: il prompt lo scarta |
| `Priority` | enum ordinato `MechanicRulePriority { Base = 0, Expansion = 1, Card = 2, Scenario = 3 }` | colonna `priority` int | `Base` | le house rule restano in AgentMemory e vincono su tutto |
| `Overrides` | `IReadOnlyList<Guid>` di id di claim **della stessa analisi** | JSONB `overrides` | `[]` | nessun indice GIN (coerente con ADR-084 §4) |
| `Trigger` | value object `MechanicTrigger { Phase?, Action?, Component? }` con nomi canonici (minuscolo, singolare, lingua della card) | JSONB `trigger` | `null` | vocabolario = tipi di `EntityExtractor` (`Phase`, `Action`, `Component`) |

**Invarianti** (nel dominio, lanciano `DomainException` → 400):
- un claim non sovrascrive se stesso;
- il grafo `Overrides` di un'analisi è aciclico;
- un `Example` non compare in alcun `Overrides` e non ne ha;
- un `Exception` ha almeno un elemento in `Overrides` **oppure** un `Trigger` non nullo;
- gli id in `Overrides` esistono nella stessa analisi.

**Mutatori**: `MechanicAnalysis.SetClaimStructure(claimId, structure)` ammesso sui claim `Pending` e `Approved` (il revisore può correggere dopo l'approvazione: la card cambia solo a una nuova pubblicazione); rifiutato su `Rejected`. Le stesse invarianti sono verificate in `ApproveClaim`, nel publish dell'analisi (409) e nel bulk approve (claim invalidi saltati).

**Migrazione**: `AddMechanicClaimStructureV3` aggiunge le quattro colonne con default; nessun backfill in migrazione (la riestrazione è la via scelta, §7). Niente modifiche alle chiavi esistenti.

## 3. Estrazione

- `EmbeddedMechanicPromptProvider.PromptVersion` → `"v1.2.0"`. Il JSON di output di ogni claim aggiunge `kind`, `priority`, `overrides` (riferimenti **per ordinale** dentro la risposta, convertiti in id dopo la creazione dei claim), `trigger`. Il prompt istruisce: scartare il flavor; `Example` solo per esempi di gioco espliciti; `Exception` solo con rimando alla regola che modifica.
- **Guardia `T5` (`RuleStructureGuardrail`, `Order = 25`, dopo T4)**, implementa `IMechanicGuardrail`. Violazioni:
  - `T5_override_missing`: ordinale non presente nella risposta;
  - `T5_override_cycle`: ciclo nel grafo;
  - `T5_example_in_override`: `Example` coinvolto in un `Overrides`;
  - `T5_exception_unbound`: `Exception` senza `Overrides` né `Trigger`;
  - `T5_trigger_unknown`: nome del `Trigger` non riconducibile al vocabolario (dopo normalizzazione).
  Esito a tre stati come ADR-084 §1. Un fallimento T5 **non scarta** il claim: lo porta a `Pending` con la violazione visibile al revisore, che corregge o rifiuta. Le violazioni di ciclo vengono risolte rimuovendo l'arco che chiude il ciclo e registrando la violazione. Il parser rimuove anche gli archi da/verso `Example`; T5 li segnala (`T5_example_in_override`). T5 usa lo stesso predicato di trigger del parser (un `trigger` vuoto o di soli spazi non lega) e calcola `T5_exception_unbound` sugli archi risolti.
- I guardrail esistenti (T1, T2, T3a, T3b, T4) non cambiano.
- Idempotenza: lo short-circuit di `GenerateMechanicAnalysisCommandHandler` è chiavato su `(SharedGame, Pdf, PromptVersion, Status)`, quindi `v1.2.0` genera una nuova analisi senza collidere con quelle `v1.1.0`.

## 4. Review admin

- `ClaimsSection.tsx` (`apps/web/src/components/admin/mechanic-extractor/claims/`): badge `Kind`, `Priority`, `Trigger` (fase/azione/componente) e un elenco "sovrascrive #n" con ancora al claim bersaglio; le violazioni T5 compaiono accanto alle altre.
- `ApproveClaimDialog.tsx`: quattro controlli modificabili (select `Kind`, select `Priority`, multi-select `Overrides` fra i claim della stessa analisi, tre campi testo per `Trigger` con suggerimento dal vocabolario). I valori proposti dall'LLM sono precompilati.
- `ApproveMechanicClaimCommand(AnalysisId, ClaimId, ReviewerId, Note, Structure?)`: `Structure` opzionale; `MechanicAnalysis.ApproveClaim(..., structure?)` valida la struttura fornita (o, se assente, quella corrente del claim) contro il grafo prima di approvare: struttura invalida ⇒ 400 e nessun salvataggio. Nuovo comando `UpdateMechanicClaimStructureCommand` (`PUT …/claims/{id}/structure`) per correggere un claim senza riaprirlo, ammesso solo con analisi `InReview`, `Rejected` o `PartiallyExtracted` (altrimenti 409).
- Bulk approve: conserva i valori proposti; non espone editor.
- Validatori FluentValidation: enum validi, `Overrides` senza duplicati. Il `Trigger` non è rifiutato dal validatore: è normalizzato a `null` quando tutti i campi sono vuoti (`MechanicTrigger.Create`).

## 5. Card `schema_version` 3 e consumatori

**Proiezione** (`MechanicCardContent.FromAnalysis`): `CurrentSchemaVersion = 3`. `MechanicCardClaimSnapshot` aggiunge le chiavi snake_case `kind` (stringa enum), `priority` (stringa enum), `overrides` (array di id di claim), `trigger` (oggetto o `null`). Nessuna chiave esistente cambia nome o posizione (ADR-084 §3). Le card v2 già pubblicate restano leggibili: i lettori trattano i campi assenti come default (`Rule`, `Base`, `[]`, `null`).

**DTO**: `PublishedMechanicCardClaimDto(Id, Claim, Citations, Kind, Priority, Overrides, Trigger)`.

**Renderer** (`KnowledgeBase/Application/Services/MechanicClaimInjection/VerifiedRulesRenderer.cs`), dietro il flag `rag.mechanic-claims.v3-ordering`:
1. per ogni sezione richiesta, esclude gli `Example` (parametro `includeExamples = false`);
2. ordina per `Priority` decrescente, poi in ordine topologico su `Overrides` (chi sovrascrive precede chi è sovrascritto), poi per `Ordinal`;
3. rende un `Exception` come `[Vn] <testo> … (Eccezione a [Vm])`, dove `[Vm]` sono i marcatori dei claim sovrascritti presenti nella stessa sezione (separati da virgola); un `Trigger` come suffisso `(quando: fase X / azione Y / componente Z)` con le sole parti non vuote;
4. applica `maxClaimsPerSection` **dopo** l'ordinamento;
5. con il flag spento il comportamento è identico a oggi (golden test).

**Validatore Python**: riceve i claim v3 così come proiettati nella card pubblicata (§6).

## 6. Contratto del validatore (ADR-092)

`POST /validate` su `orchestration-service`:

```json
{
  "game_id": "uuid",
  "state": { "phase": "azione", "current_player": "p1", "...": "libero, validato dal toolkit lato C#" },
  "move": { "action": "muovere", "component": "cavaliere", "params": {} },
  "rules": [ { "id": "uuid", "kind": "Rule|Exception|Clarification", "priority": "Base|Expansion|Card|Scenario",
               "overrides": ["uuid"], "trigger": { "phase": "azione", "action": null, "component": null },
               "claim": "testo", "citations": [ { "pdf_page": 7 } ] } ]
}
```

Risposta:

```json
{ "verdict": "Valid|Invalid|Unknown", "applied_rule_ids": [], "violated_rule_ids": [],
  "explanation_steps": [ { "rule_id": "uuid", "step": "Trigger match: phase=azione" } ],
  "confidence": null }
```

**Kernel di risoluzione** (deterministico, senza LLM):
1. *applicabili* = regole il cui `Trigger` combacia con `state`/`move` (campo nullo = jolly; nomi confrontati dopo normalizzazione); i `Clarification` non producono verdetto, solo passi;
2. rimuovi le regole sovrascritte da un'applicabile via `Overrides`;
3. tieni la `Priority` massima fra le restanti;
4. se resta una sola regola (o più regole concordi) ⇒ `Valid`/`Invalid` secondo la sua polarità; se restano regole in conflitto allo stesso livello senza `Overrides` fra loro ⇒ `Unknown`; se nessuna applicabile ⇒ `Unknown`.
La **polarità** (permette/vieta) di un claim è derivata in questa prima versione da un classificatore lessicale documentato nei test (verbi di divieto/obbligo in IT/EN); è il punto di innesto del pilota ASP e della successiva estensione del modello (`Polarity` esplicita) se il classificatore si rivela insufficiente sul golden set.

`confidence` resta `null` finché non esiste una misura (per esempio accordo con il golden set per gioco).

**Lato C#**: client `IArbiterValidationClient` in `KnowledgeBase/Infrastructure`, invocato da `AskArbiterCommandHandler` via handler/`IMediator`; timeout dal budget di latenza configurato; timeout o errore ⇒ `Unknown` e risposta senza citazioni inventate. `SsrfPin` applicato come a ogni `HttpClient`.

## 7. Riestrazione dei giochi esistenti

- Comando admin `RequeueMechanicAnalysisForPromptVersionCommand(sharedGameId)` che avvia una nuova `MechanicAnalysis` con `v1.2.0` per il PDF della card pubblicata. Risponde 409 se per il prompt corrente esiste già un'analisi `Draft`, `InReview`, `Published` o `PartiallyExtracted`: l'indice unico `ux_mechanic_analyses_shared_game_pdf_prompt` (filtro `status <> 3`) ammette una sola riga non rifiutata per (gioco, PDF, prompt), quindi un'analisi parzialmente estratta va rifiutata prima di riestrarre. Un job admin "riestrai tutti" itera i giochi con card pubblicata, con concorrenza limitata e costo registrato via `MechanicAnalysis.RecordUsage` (ADR-051).
- La card pubblicata resta in uso finché il revisore approva i nuovi claim e pubblica (`PublishMechanicCardCommand` incrementa `Version`). Nessun claim approvato viene toccato.
- Dopo la pubblicazione della v3 il job di auto-suppression e il feedback continuano a valere sulla nuova card.

## 8. Test

| Livello | Cosa | Dove |
|---|---|---|
| Dominio | invarianti di §2 (auto-override, ciclo, Example, Exception non legata), `SetStructure` per stato | `tests/Api.Tests/.../SharedGameCatalog/Domain/MechanicClaimStructureTests.cs` |
| Guardia | ogni violazione T5 con fixture JSON; ciclo risolto con arco rimosso e violazione registrata | `.../MechanicExtractor/Guardrails/RuleStructureGuardrailTests.cs` |
| Proiezione | `FromAnalysis` produce v3 con chiavi nuove e chiavi v2 invariate (snapshot JSON) | `.../MechanicCardContentV3Tests.cs` |
| Renderer | golden test: flag spento = output identico a oggi; flag acceso = ordinamento per priorità e topologico, `Example` esclusi, tetto applicato dopo | `.../MechanicClaimInjection/VerifiedRulesRendererOrderingTests.cs` |
| Comandi | approvazione con `Structure`, aggiornamento struttura su `Approved`, rifiuto su `Rejected`, validatori | `.../MechanicExtractor/ApproveMechanicClaimCommandV3Tests.cs` |
| Migrazione | colonne con default; card v2 letta con default | integrazione (Testcontainers), classe con `SharedHostPerTestDatabaseFixture` solo se ≥ 2 test |
| Python | kernel: trigger jolly, override, priorità, conflitto ⇒ `Unknown`, nessuna regola ⇒ `Unknown`, `confidence` sempre `null` | `apps/orchestration-service/tests/test_validate_kernel.py` |
| Contratto | C# ↔ Python: timeout ⇒ `Unknown`; schema di richiesta/risposta congelato in un test di contratto da entrambi i lati | `.../KnowledgeBase/ArbiterValidationClientTests.cs`, `tests/test_validate_contract.py` |
| Gate | fetta multi-hop del golden set (prerequisito esterno, roadmap §4 punto 1) prima/dopo con `v3-ordering` acceso | `infra/fixtures/rag-golden-baseline.json` |

Policy del repo: nessun test skippato senza classe di motivo (`PREVISTO:/GUASTO:/DIFETTO:/LIMITE:`); nessuna crescita del conteggio di fallimenti.

## 9. Rollout e flag

- `rag.mechanic-claims.v3-ordering` (default off) governa il renderer; `rag.mechanic-card-injection` resta il flag padre.
- `/validate` è attivo solo se `orchestration-service` è avviato (profilo `tutor-agents`); in sua assenza il C# risponde `Unknown` e lo registra come metrica, non come errore.
- Ordine di consegna suggerito: §2 + §3 (migrazione, prompt, T5) → §4 (review) → §5 (card v3 + renderer dietro flag) → §7 (riestrazione di un gioco pilota) → §6 (validatore) → accensione del flag per gioco.

## 10. Decisioni registrate durante il brainstorming

| Domanda | Scelta |
|---|---|
| Chi compila i campi | LLM propone, revisore conferma |
| Forma del `Trigger` | strutturato sul vocabolario di `EntityExtractor` |
| Forma della `Priority` | livelli chiusi |
| Claim esistenti | riestrazione completa (nuova analisi per gioco, card attuale invariata fino a pubblicazione) |
| Dove vivono i campi | sul claim, proiettati nella card (approccio A) |
| Proprietario del verdetto | Python validatore puro (ADR-092) |
