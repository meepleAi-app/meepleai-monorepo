# ADR-085 — SP7 Agent-Builder: allineamento al backend admin `AgentDefinition` (MVP)

**Status**: Accepted
**Date**: 2026-07-15
**Deciders**: @badsworm (ratificato 2026-07-15 via [#2964](https://github.com/meepleAi-app/meepleai-monorepo/issues/2964); opzione "Allinea a backend (MVP)")
**Tracking**: [#2964](https://github.com/meepleAi-app/meepleai-monorepo/issues/2964) — deriva dallo spec-panel [#1889](https://github.com/meepleAi-app/meepleai-monorepo/issues/1889)
**Related**: audit `docs/for-developers/audits/2026-07-15-sp7-spec-panel-invariant-diff.md` (§2 AB-11) · brief `admin-mockups/briefs/SP7-game-night-agent-builder.md` (sez. D–H) · spec `docs/for-developers/specs/2026-07-15-agent-builder-domain-invariants.md` (8 invarianti)

## Context

Il brief SP7 (sez. D–H) commissiona una superficie **agent-builder user-facing** su route `/editor/agent-proposals*`, con entità `AgentProposal`, persona primaria **Marco (power-user regolare)** che crea/testa/pubblica agenti, status `{Draft, Testing, Published, Archived}`, un **confidence-threshold slider**, un **tone-preset picker** e **version history + rollback**.

Lo spec-panel #1889 (audit §2, finding **AB-11**) ha verificato sul codebase che il claim del brief *"backend già pronto per US-33"* è **fuorviante su 4 punti**:

| # | Brief assume | Backend reale (`KnowledgeBase` BC) |
|---|---|---|
| 1 | `AgentProposal` + route `/editor/agent-proposals*` user-facing | `AgentDefinition` + route `/admin/agent-definitions*` **admin-only** (`RequireAdminSessionFilter`) |
| 2 | Status `{Draft, Testing, Published, Archived}` | Enum `{Draft, Testing, Published}` — **no `Archived`** (esiste `SoftDelete()/Restore()`) |
| 3 | Confidence-threshold slider + tone-preset + system-prompt strutturato (E-Step 3) | `AgentDefinitionConfig` persiste solo `Model/MaxTokens/Temperature` + `Prompts` generico (JSONB) — **campi assenti** |
| 4 | Version history + rollback (G, G33.9) | **Assenti** |

Supportato pienamente: solo KB+Game linking (`KbCardIds` + `GameId`), CQRS core (Create/Update/Delete/Publish/Unpublish/StartTesting + query), testing via `PlaygroundChatCommand`. La state machine reale è matura ma **`Publish()` blocca la transizione diretta Draft→Published** (richiede il passaggio da `Testing`); `StartTesting()` blocca il ritorno da `Published` ("Unpublish first"). Il `DELETE` REST è **hard-delete** (`SoftDelete()/Restore()` esistono sull'aggregato ma **non sono esposti via REST**), quindi "Archived" non ha oggi alcuna superficie API.

**Scoperta decisiva — l'admin FE agent-builder è GIÀ shipped.** Verifica FE: esiste `apps/web/src/app/admin/(dashboard)/agents/definitions/` con `page.tsx` (builder table), `create/page.tsx` (wizard con step `BasicInfoStep/PromptEditorStep/ToolsStrategyStep/ReviewStep` + `AgentPreviewPanel`), `[id]/page.tsx`, `[id]/edit/page.tsx`, `playground/page.tsx`, oltre a API client (`lib/api/agent-definitions.api.ts`), schemas, hook (`hooks/admin/useAgentDefinitions.ts`) e voci nav admin. **I mockup SP7 D–G ridisegnano quindi una superficie già costruita e in produzione** (list/create/test/edit come admin-tool). Nota drift da verificare: la sidebar admin punta a `/admin/agent-definitions` (flat) mentre le pagine App Router vivono sotto `/admin/(dashboard)/agents/definitions` → URL effettivo `/admin/agents/definitions`.

Nota parziale-mapping: il "tone-preset picker" del brief ha un analogo grezzo nel backend (`TypologySlug` ∈ {arbitro, game-master, chat} + `ChatLanguage`), ma non è il picker a 5 toni + confidence-slider descritto in E-Step 3.

Serviva una decisione — la stessa che l'audit segnalava come *"precede tutto il resto"*: **allineare i mockup al backend esistente** oppure **estendere il backend allo scope del brief**.

## Decision

**MVP = allineare al backend esistente. L'agent-building è un tool ADMIN (AI-Lab), non una feature user-facing `/editor/`.**

La superficie di **costruzione** agenti (mockup D–G) è ri-scopata come **admin surface** sopra `/admin/agent-definitions` (admin-gated). La superficie **user-facing** è solo:
- **H** (`sp7-library-game-agent`, `/library/games/[gameId]/agent`) — Marco **usa** un agente pubblicato via chat inline;
- il tab "agent" del game-detail (già esistente).

**Ripartizione persona**: **Aaron (admin/dogfood) COSTRUISCE** gli agenti; **Marco (power-user) li USA**. La persona "Marco costruisce agenti al `/editor/`" del brief è **differita** (richiede l'estensione backend user-facing — vedi § Consequences/Negative).

## Allineamenti concreti (per punto di mismatch)

1. **Nomenclatura + route**: mockup D–G → `/admin/agent-definitions*` (admin-gated), entità `AgentDefinition`. NON `/editor/agent-proposals` / `AgentProposal`. Endpoint reali: base `GET/POST/PUT/DELETE /admin/agent-definitions` + `/start-testing`, `/publish`, `/unpublish`, `/stats`, `/catalog-stats`.
2. **Lifecycle**: `{Draft → Testing → Published}`. **`Archived` NON è un 4° status di lifecycle** ma il soft-delete (`SoftDelete()/Restore()`) → nella UI "Archiviati" = filtro sui soft-deleted, non un badge di stato accanto a Draft/Testing/Published. Il **publish passa obbligatoriamente da Testing** (`Publish()` rifiuta Draft→Published diretto) → E-wizard "Pubblica subito" **rimosso**; il flusso è "Crea come Draft → testa (F) → pubblica".
3. **Config fields (E-Step 3)**: confidence-threshold slider + tone-preset picker + system-prompt strutturato sono **OUT of MVP** (il backend persiste solo `Model/MaxTokens/Temperature` + `Prompts` generico). Differiti come estensione backend, tracciati. L'MVP usa i campi esistenti.
4. **Version history / rollback (G)**: **OUT of MVP** (nessun supporto backend). Mockup **G differito/tagliato** dalla wave MVP.

**Testing/playground (F)**: mappa su `PlaygroundChatCommand` esistente — in scope MVP.

Le **8 invarianti agent-builder** risolte coerentemente con questa direzione sono formalizzate in `docs/for-developers/specs/2026-07-15-agent-builder-domain-invariants.md`.

## Consequences

### Positive
- **Backend già pronto per l'MVP allineato**: `AgentDefinition` è maturo (state machine con guard, CQRS, KB+Game linking, soft-delete). Zero backend work per l'MVP admin-tool.
- **Basso rischio + sicurezza**: la costruzione agenti resta dietro `RequireAdminSessionFilter` (nessuna esposizione user-facing non presidiata).
- **Sblocca #1889**: definito il perimetro reale, i mockup D–G (mancanti) diventano autorabili come admin-surface; poi il demo replay diventa eseguibile.
- **Coerenza demo**: il caso dogfood (Aaron costruisce agenti Nanolith) è esattamente admin-tool.

### Negative
- **Persona "Marco costruisce agenti" differita**: l'MVP non attiva la US-33 nella sua forma user-facing piena. Marco usa gli agenti (H) ma non li crea. Riabilitabile con l'estensione backend (route user-facing + ownership per-utente).
- **Confidence-threshold + tone-preset + version/rollback differiti**: 3 feature del brief non entrano nell'MVP. Vanno tracciate come estensioni backend se il prodotto le vuole.
- **Il brief SP7 va corretto** (routes/persona/scope D–G) per non rigenerare mockup disallineati — fatto in questo PR.

### Trade-offs accettati
- MVP admin-only ora, user-facing dopo (se giustificato da signal d'uso), invece di costruire subito il flusso user-facing completo. Coerente con il principio YAGNI + "backend existing > speculative build".

## Implementation guidance

1. **Brief SP7** (`admin-mockups/briefs/SP7-game-night-agent-builder.md`): correggere sez. D–G (route → `/admin/agent-definitions`, persona → admin/dogfood, rimuovere `Archived`-status/confidence-slider/tone-preset/version-rollback dallo scope MVP, publish via Testing). H resta user-facing. **Fatto in questo PR.**
2. **Domain invariants** (`docs/for-developers/specs/2026-07-15-agent-builder-domain-invariants.md`): 8 invarianti risolti per align-to-backend. **Fatto in questo PR.**
3. **Mockup D–G: NON ri-commissionare.** La superficie admin agent-builder è **già in produzione** (`/admin/agents/definitions` — list/create-wizard/test-playground/edit + componenti + API client + nav). I mockup SP7 D–G sono ridondanti con FE shipped → dispositi come *"not commissioned"* (analogo alla disposition di ADR-079 per il mockup C). L'eventuale allineamento visivo dei mockup alla UI admin reale è opzionale e a bassa priorità.
4. **Impatto su #1889**: il blocco *"5 mockup agent-builder mancanti (D–H)"* si dissolve in gran parte — D–G esistono come admin FE shipped; resta genuinamente da autorare solo **H** (chat inline user-facing) se non già coperto dal tab agent del game-detail. Il demo replay #1889, se rieseguito, va scopato su H + game-night, non su D–G.
5. **Estensione backend user-facing** (route `/editor/*`, ownership per-utente, `Archived` status, confidence/tone/version): **tracciata come futura**, fuori MVP, da aprire se il prodotto conferma la persona "Marco builder".
6. **Drift da verificare** (fuori scope di questo ADR): nav admin punta a `/admin/agent-definitions` (flat) vs pagine sotto `/admin/(dashboard)/agents/definitions` (URL `/admin/agents/definitions`). Segnalare in issue separata se confermato.

## Rollback / reversibility

L'ADR è documentazione. La reversibilità è totale a livello doc: se il prodotto decide per la direzione user-facing, si riparte dal brief originale + issue di estensione backend. Nessun codice viene rimosso da questa decisione (l'MVP riusa il backend esistente).

## References

- Issue #2964 — decisione (sorgente di questo ADR)
- Issue #1889 — spec-panel che ha scoperto il mismatch (AB-11)
- Audit `docs/for-developers/audits/2026-07-15-sp7-spec-panel-invariant-diff.md` §2
- Spec `docs/for-developers/specs/2026-07-15-agent-builder-domain-invariants.md` (8 invarianti)
- Backend: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/AgentDefinition.cs` + `Routing/AdminAgentDefinitionEndpoints.cs`
- Brief: `admin-mockups/briefs/SP7-game-night-agent-builder.md` (sez. D–H — corretta nello stesso PR)
