# P2 prep — Legacy cleanup inventory

> **Date**: 2026-05-07
> **Owner**: @DegrassiAaron
> **Status**: P2 preparation document — NOT to be executed during P1 (Wiegers scope discipline)
> **Parent spec**: `docs/superpowers/specs/2026-05-07-v2-meepleai-deploy-goals.md` (Goal P2)

## Purpose

Catalog flag, dual-mode component, e codice legacy candidati per rimozione, con verifica grep dei consumer e classificazione di rischio. Output dopo P1 acceptance: 1-N PR atomiche di cleanup che concretizzano la parte P2 "codice legacy lungo critical-path Alpha rimosso".

## Scope discipline (Wiegers)

❌ **NON eseguire durante P1 deploy verification**: scope mixing P1 vs cleanup rende impossibile attribuire regressioni. Tutto il lavoro qui catalogato attende P1 acceptance closed.

❌ **NON toccare `IS_ALPHA_MODE`** (22+ usages): è feature gate per Alpha Zero mode (subset features per early testers). NON è un v2/legacy switch. Vivente per design.

✅ **Eseguire come PR atomiche** post-P1, una per Tier (Tier 1 prima, Tier 2 dopo verifica caso-per-caso).

## Tier 1 — Safe to remove (verified 0 consumers)

| File | Path | Status | Verification |
|------|------|--------|--------------|
| `AgentDetailPanel.tsx` | `apps/web/src/components/v2/agents/AgentDetailPanel.tsx` | Stub no-consumer | `grep AgentDetailPanel` ritorna SOLO la stringa nel comment di `index.ts` che spiega il non-export. ZERO import production |
| `AgentsSidebarList.tsx` | `apps/web/src/components/v2/agents/AgentsSidebarList.tsx` | Stub no-consumer | Idem AgentDetailPanel — solo menzione in commento `index.ts` |
| `SharedGameDetailModal.tsx` | `apps/web/src/components/shared-games/SharedGameDetailModal.tsx` | `@deprecated Wave A.4 (#603)` | `grep SharedGameDetailModal` ritorna 0 match production. Superseded da `/shared-games/[id]` route |
| `shared-games/index.ts` re-export | `apps/web/src/components/shared-games/index.ts` riga 19 `@deprecated` | Re-export di componente già morto | Da pulire insieme a SharedGameDetailModal |

### PR plan Tier 1

Verified via grep post-review (2026-05-07):
- `AgentDetailPanel.tsx` + `AgentsSidebarList.tsx`: NO paired test files
- `SharedGameDetailModal.tsx`: paired test `__tests__/SharedGameDetailModal.test.tsx` exists → delete insieme

```bash
git checkout main-dev
git pull --ff-only
git checkout -b chore/p2-prep-legacy-cleanup-tier1
git rm apps/web/src/components/v2/agents/AgentDetailPanel.tsx
git rm apps/web/src/components/v2/agents/AgentsSidebarList.tsx
git rm apps/web/src/components/shared-games/SharedGameDetailModal.tsx
git rm apps/web/src/components/shared-games/__tests__/SharedGameDetailModal.test.tsx
# Edit shared-games/index.ts → rimuovi re-export deprecated (riga 19)
# Edit components/v2/agents/index.ts → rimuovi commento "orphan stubs" obsoleto
pnpm typecheck   # MUST pass
pnpm test        # MUST pass
git commit -m "chore(cleanup): remove Tier 1 legacy components (AgentDetailPanel, AgentsSidebarList, SharedGameDetailModal + paired test)"
gh pr create --base main-dev --title "chore(cleanup): P2 prep Tier 1 — remove orphan stubs + deprecated SharedGameDetailModal"
```

DoD:
- [ ] 5 file rimossi (3 components + 1 paired test + index re-export edit)
- [ ] 1 file modificato (`shared-games/index.ts` re-export removal)
- [ ] 1 file modificato (`components/v2/agents/index.ts` commento orphan stubs cleanup)
- [ ] `pnpm typecheck` green
- [ ] `pnpm test` green
- [ ] PR review approva con 0 obiezioni (zero consumer = zero risk)
- [ ] Bundle size CI mostra riduzione (anche se piccola)

## Tier 2 — Requires verification before removal

### Tier 2a — `ArbitroModal.useV2` prop dual-mode

**Status**: 🚨 **HIGH RISK — production usa BRANCH v1**

`grep ArbitroModal` rivela:
```
apps/web/src/app/(authenticated)/sessions/live/[sessionId]/agent/page.tsx:272:
  <ArbitroModal sessionId={sessionId} players={...} />   ← NO useV2 prop, defaults to false
```

Il default `useV2 = false` significa **produzione esegue il v1 branch**, non v2. Rimuovere v1 senza prima migrare il call-site = breaking change visibile.

**Removal plan**:
1. Verifica con product owner se la `useV2` esperienza è quella desiderata
2. Aggiungi `useV2={true}` al call-site `agent/page.tsx:272`
3. Run E2E `/sessions/live/.../agent` per verificare comportamento atteso
4. Se OK, rimuovi prop `useV2` e branch `if (!useV2)` dentro `ArbitroModal.tsx`
5. Pulisci test condizionati su `useV2` prop

**Tempo stimato**: 1-2 giorni single dev (richiede E2E verification + test cleanup)

### Tier 2b — `agentTypologiesApi` no-op methods

**Status**: 🟡 **MEDIUM RISK — heavy production usage**

`grep agentTypologiesApi` rivela call-sites in:
- `/editor/agent-proposals/[id]/test/page.tsx` (3 metodi: `getById`, `test`, `submitForApproval`)
- `/editor/agent-proposals/_components/ProposalsList.tsx` (`getMyProposals`)
- Tests integration con mocks su `propose`, `getMyProposals`

Comment `kept as no-ops to avoid compile errors` suggerisce il backend non implementa più Proposal flow, ma frontend admin tooling lo chiama ancora (UI rotta o call-sites no-op).

**Removal plan**:
1. Verifica backend: `/api/v1/agent-typologies/proposals/*` endpoint esiste? Risposta?
2. Se backend gone: rimuovere TUTTO il flow `/editor/agent-proposals/**` (route + components + api file)
3. Se backend ancora attivo ma deprecated: marcare con visible UI banner "deprecated" + plan per sunset
4. NON rimuovere solo il file `agent-typologies.api.ts` — call-site rotti

**Tempo stimato**: 3-5 giorni (verifica backend + decisione + cleanup esteso)

### Tier 2c — `useGameImportWizardStore.documentId` alias

**Status**: 🟡 **MEDIUM RISK — 6 consumer in admin wizard flow**

Verified via grep post-review (2026-05-07): `useGameImportWizardStore()` chiamato in 6 file:
- `apps/web/src/app/admin/(dashboard)/shared-games/import/client.tsx:65`
- `apps/web/src/app/admin/(dashboard)/shared-games/import/steps/Step2MetadataReview.tsx:29`
- `apps/web/src/app/admin/(dashboard)/shared-games/import/steps/Step3PreviewConfirm.tsx:20`
- `apps/web/src/app/admin/(dashboard)/shared-games/import/steps/Step4CreationProgress.tsx:38`
- `apps/web/src/app/admin/(dashboard)/shared-games/import/steps/Step5RagTest.tsx:45`
- `apps/web/src/hooks/wizard/useWizardAutoSave.ts:108`

**Removal plan**:
1. Per ognuno dei 6 file: ispeziona destrutturazione → verifica se accede `documentId` (deprecato) vs `pdfDocumentId` (canonico)
2. Refactor 6 consumer al nome canonico `pdfDocumentId`
3. Rimuovi alias `documentId` getter dallo store
4. `pnpm typecheck` + `pnpm test` green

**Tempo stimato**: 1-2 giorni (refactor 6 file + test re-run)

### Tier 2d — `Citation` deprecated alias (`chat/shared/types.ts`)

**Status**: 🟢 **LOW RISK — alias type, no direct consumers found via grep**

Verified via grep post-review (2026-05-07): `from '@/components/chat/shared/types'` ha 0 import diretti production. L'alias type è probabilmente importato via re-export indirect.

**Pre-removal investigation needed**:
1. Trova path import effettivo del type (`grep -rEn "Citation" apps/web/src/components/chat`)
2. Identifica il "legacy module" target citato nel comment
3. Verifica se cleanup chat-unified Phase-0 è completo (cerca PR / issue Phase-0)
4. Se completo, rimuovi alias + `chat/shared/types.ts` se vuoto

**Tempo stimato**: 0.5-1 giorno (investigation + atomic removal)

## Tier 3 — DO NOT touch

| Item | Razionale |
|------|-----------|
| `IS_ALPHA_MODE` (22+ usages) | Feature gate Alpha Zero mode, NON è v2/legacy switch. Rimuoverlo abilita features non pronte (sessions/agents UI completa). Build-time flag dichiarato design |
| File con commenti `kept as legacy fallback for when [backend] is implemented` | Hanno semantica futura — backend in fieri |
| `@deprecated` markers senza alternative documentata | Possono essere rumore, lasciare per ora |

## Spec-panel review

### FOWLER — Refactoring patterns

> ✅ Tier 1 = textbook safe removal. `@deprecated` + 0 consumers + replacement docs available + ~6 settimane prior notice (Wave A.4 #603). PRIORITY: HIGH, eseguire come prima PR post-P1.
> ⚠️ Tier 2a `ArbitroModal` è strangler fig stuck mid-strangulation: BOTH branches living, production sceglie v1 by default. Questo è un BUG nascosto, non solo cleanup — la migration è incompleta. PRIORITY: MEDIUM (richiede product decision).
> 📊 Tier 2b `agentTypologiesApi` no-ops senza backend è dead code che simula vitalità per il compiler. Anti-pattern. Decidere sunset completo flow proposals.

### WIEGERS — Scope discipline

> ❌ CRITICAL pre-condition: P1 acceptance closed BEFORE qualunque rimozione. Se rimuoviamo legacy durante P1 deploy verification, non possiamo attribuire regressioni a P1 vs cleanup. Recovery cost > benefit.
> ✅ PR atomiche per Tier (1 PR Tier 1, separate PR per ogni Tier 2 sub-item) preserva traceability per regression bisection.
> 📝 Tier 1 PR DoD esplicito (typecheck + test green + bundle reduction visible) è sufficient.

### NEWMAN — API evolution & backward compat

> ⚠️ Tier 1 OK: zero consumers verificati via grep, no API exposure (sono internal components).
> 🚨 Tier 2b `agentTypologiesApi`: heavy frontend usage suggerisce contract con backend ancora vivo o frontend è rotto silently. Verifica empirica via `curl backend` PRIMA di toccare frontend code. Possibile finding parallelo: `/editor/agent-proposals` route stessa è dead UI?
> 📝 Tier 2a `ArbitroModal`: aggiungi a P2 plan task "verifica useV2 contract con product" prima di pianificare rimozione branch.

### CRISPIN — Testing & quality

> ⚠️ Tier 2a removal richiede E2E coverage `/sessions/live/[id]/agent` con flusso ArbitroModal. Verificare esistenza test prima di rimuovere v1 branch — se test pre-P2 testano v1 path, removal invalida test coverage.
> ✅ Tier 1 DoD include `pnpm test` green — sufficient se non ci sono test paired ai file removed.
> 📝 Per Tier 2b: aggiungere step "verifica MSW handlers + integration test mocks" — agent-typologies è heavily mocked, removal del file rompe i test.

## Improvement roadmap

### Immediate (post-P1 acceptance)
- **PR #1**: Tier 1 cleanup (4 file delete). Effort: 1 giorno. Risk: minimal.

### Short-term (P2 in progress)
- **PR #2**: Tier 2c `documentId` alias removal. Effort: 1 giorno. Risk: low.
- **PR #3**: Tier 2d `Citation` alias removal (subordinate a chat-unified Phase-0 verify). Effort: 1 giorno.

### Medium-term (P2 critical-path cleanup)
- **PR #4**: Tier 2a `ArbitroModal.useV2` finalize migration. Effort: 1-2 giorni + product decision.
- **PR #5**: Tier 2b `agentTypologiesApi` + `/editor/agent-proposals/**` sunset (se backend confermato gone). Effort: 3-5 giorni + product decision.

## Self-review checklist

- ✅ **Placeholder scan**: nessun TBD. "TBD" nei plan PR future è esplicito riferimento a verification step da fare.
- ✅ **Internal consistency**: Tier 1 / 2 / 3 categorization basata su grep evidence (ogni Tier 2 item ha grep finding citato).
- ✅ **Scope check**: documento è inventory + plan, non si esegue durante P1. Tier 1 PR ben isolata. Tier 2 sub-items in PR separate.
- ✅ **Ambiguity check**: ogni Tier 2 item ha removal plan step-by-step + tempo stimato.

## Riferimenti

- Spec parent: `docs/superpowers/specs/2026-05-07-v2-meepleai-deploy-goals.md` (Goal P2)
- Plan P1: `docs/superpowers/plans/2026-05-07-p1-pipeline-staging-trustworthy.md`
- Wave A.4 #603 — SharedGameDetailModal deprecation rationale
- Wave B.2 spec-panel — AgentDetailPanel + AgentsSidebarList rimozione scope
- v2 migration matrix: `docs/for-developers/frontend/v2-migration-matrix.md`
