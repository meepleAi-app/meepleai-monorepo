# E2E Smoke Game-Night Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrare i 4 spec game-night da workflow isolato a struttura nightly esistente (3 mock) + workflow weekly free-LLM (1 real), eliminare workflow obsoleto.

**Architecture:** B+ ibrida — 3 mock spec piggyback su `smoke-real-backend/` + 1 real-LLM spec in nuovo `smoke-real-llm/` con free model fallback chain.

**Tech Stack:** Playwright 1.x, Next.js 16 (web container :3000), .NET 9 API (:8080), Docker Compose stack `dev-core` (mock) / `dev-from-snapshot` (real-LLM).

**Spec**: `docs/superpowers/specs/2026-05-10-e2e-smoke-game-night-integration-design.md`

**Pre-flight context for implementer:**
- File 4 spec sorgenti da migrare: `apps/web/e2e/smoke/game-night-{happy-path,low-confidence,out-of-context,pdf-non-owner}.spec.ts`
- Helpers sorgenti: `apps/web/e2e/smoke/_helpers.ts`, `apps/web/e2e/smoke/_helpers.fixtures.ts`
- Auth helper target da riusare: `apps/web/e2e/smoke-real-backend/_helpers/auth.ts` (pattern `smokeLogin` + `applySessionToPage`)
- Pattern altri spec smoke nightly: vedi `apps/web/e2e/smoke-real-backend/games.smoke.spec.ts` per struttura concisa
- Workflow nightly esistente: `.github/workflows/e2e-smoke-real-backend.yml` (cron 03:00, `make dev-core`, admin seedato via env)

---

### Task 1: Crea `_helpers/game-chat.ts`

**Files:**
- Create: `apps/web/e2e/smoke-real-backend/_helpers/game-chat.ts`

**Context:** Helpers shared da 3 mock spec. Resolve OQ1/OQ2/OQ3/OQ4 dello spec leggendo il codice prima di implementare.

- [ ] **Step 1: Verifica API surface (OQ1, OQ2)**

Run:
```bash
grep -rn "MapPost.*shared-games\|MapPost.*library/games" apps/api/src/Api/BoundedContexts/ | head -10
```
Expected: trova endpoint creazione SharedGame e add-to-library. Annota route paths esatti + DTO body shape per usarli al Step 3.

- [ ] **Step 2: Verifica SSE protocol (OQ3, OQ4)**

Run:
```bash
grep -n "type:\s*[0-9]\|StreamingComplete\|qaStream" apps/web/src/lib/chatClient.ts apps/api/src/Api/Contracts.cs | head -20
```
Expected: identifica enum `EventType` (1=delta, 7=complete) + shape esatto del payload `complete` (citations array shape, confidence number, outOfContext flag o derivato).

- [ ] **Step 3: Implementa helpers**

Crea il file con: `seedGameForChat(request, cookieHeader): Promise<string>` (idempotent, usa `GITHUB_RUN_ID` per nome univoco), `mockQaStreamV2(page, opts)` (mock SSE response usando route fulfill), `mockNoDocuments(page, gameId)` (route empty array), `buildSseBody(opts)` (helper interno format SSE).

Schema completo: vedi spec §4.1.

Adatta payload SSE in base a quanto trovato in Step 2. Se `outOfContext` è derivato (non in payload backend), il mock può semplicemente impostare `confidence: 0.0` + `citations: []` per triggerare lo stesso branch frontend.

- [ ] **Step 4: Verifica tipo TypeScript compila**

Run: `cd apps/web && pnpm tsc --noEmit 2>&1 | grep "game-chat.ts" || echo "OK"`
Expected: `OK` (no type errors).

- [ ] **Step 5: Commit**

```bash
git add apps/web/e2e/smoke-real-backend/_helpers/game-chat.ts
git commit -m "test(smoke): add game-chat helpers for nightly mock specs (#938)"
```

---

### Task 2: Migrate `game-night-low-confidence.smoke.spec.ts`

**Files:**
- Read: `apps/web/e2e/smoke/game-night-low-confidence.spec.ts` (sorgente)
- Create: `apps/web/e2e/smoke-real-backend/game-night-low-confidence.smoke.spec.ts`

**Context:** Adapter pattern: prendi logica del sorgente, sostituisci `loginSmokeAaron` → `smokeLogin`, sostituisci hardcoded `SEED_GAME_ID` → `seedGameForChat()`, sostituisci `mockQaStream` custom → `mockQaStreamV2` Task 1.

- [ ] **Step 1: Read source spec**

Read `apps/web/e2e/smoke/game-night-low-confidence.spec.ts` completo per capire la logica esatta di assertion.

- [ ] **Step 2: Write adapted spec**

Use template da spec §4.1 (esempio low-confidence). Imports:
```ts
import { test, expect } from '@playwright/test';
import { smokeLogin, applySessionToPage } from './_helpers/auth';
import { seedGameForChat, mockQaStreamV2 } from './_helpers/game-chat';
```

Assertion principali da preservare:
- `[data-slot="low-confidence-disclaimer"]` visible
- `[data-slot="confidence-badge"][data-tier="bassa"]` visible
- `[data-slot="citation-chip"]` ancora presente

- [ ] **Step 3: Run spec locally (manual)**

Pre-condition: stack `make dev-core` running, web :3000 up, INITIAL_ADMIN seedato.
Run: `cd apps/web && pnpm playwright test smoke-real-backend/game-night-low-confidence.smoke.spec.ts --project=desktop-chrome --reporter=list`
Expected: PASS in <30s. Se fallisce: leggi report, debug, fix.

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/smoke-real-backend/game-night-low-confidence.smoke.spec.ts
git commit -m "test(smoke): migrate low-confidence spec to nightly structure (#938)"
```

---

### Task 3: Migrate `game-night-out-of-context.smoke.spec.ts`

**Files:**
- Read: `apps/web/e2e/smoke/game-night-out-of-context.spec.ts`
- Create: `apps/web/e2e/smoke-real-backend/game-night-out-of-context.smoke.spec.ts`

- [ ] **Step 1: Read source + write adapted spec**

Pattern identico a Task 2. Mock SSE call: `mockQaStreamV2(page, { confidence: 0.0, citations: [], outOfContext: true })`.

Assertion principali:
- `[data-slot="out-of-context-actions"]` visible (3 buttons)
- `[data-slot="out-of-context-actions"] button[data-action-kind="switch-game"]` present
- `button[data-action-kind="find-agent"]` present
- `button[data-action-kind="stay"]` present

- [ ] **Step 2: Run + commit**

Run pattern Task 2 Step 3.

```bash
git add apps/web/e2e/smoke-real-backend/game-night-out-of-context.smoke.spec.ts
git commit -m "test(smoke): migrate out-of-context spec to nightly structure (#938)"
```

---

### Task 4: Migrate `game-night-pdf-non-owner.smoke.spec.ts`

**Files:**
- Read: `apps/web/e2e/smoke/game-night-pdf-non-owner.spec.ts`
- Create: `apps/web/e2e/smoke-real-backend/game-night-pdf-non-owner.smoke.spec.ts`

- [ ] **Step 1: Read source + write adapted spec**

Use `mockQaStreamV2` con citation valida + `mockNoDocuments(page, gameId)` per simulare non-ownership.

Assertion principali:
- Click citation chip → `[data-slot="citation-modal"]` opens
- Switch to "PDF originale" tab → `[data-slot="citation-ownership-upsell"]` visible
- Link "Carica il mio PDF" href contiene `/upload?gameId=`
- NO download button visible (anti-leak): `await expect(page.locator('a[download]')).toHaveCount(0);`

- [ ] **Step 2: Run + commit**

```bash
git add apps/web/e2e/smoke-real-backend/game-night-pdf-non-owner.smoke.spec.ts
git commit -m "test(smoke): migrate pdf-non-owner spec to nightly structure (#938)"
```

---

### Task 5: Update workflow `e2e-smoke-real-backend.yml`

**Files:**
- Modify: `.github/workflows/e2e-smoke-real-backend.yml`

- [ ] **Step 1: Add path-trigger to existing `on:` block**

Locate `on:` block (lines 3-5 currently: `schedule` + `workflow_dispatch`).

Replace with:
```yaml
on:
  schedule:
    - cron: '0 3 * * *'
  push:
    branches: [main-dev]
    paths:
      - 'apps/web/src/components/v2/game-chat/**'
      - 'apps/web/src/hooks/queries/useGameChat.ts'
      - 'apps/web/e2e/smoke-real-backend/**'
      - '.github/workflows/e2e-smoke-real-backend.yml'
  workflow_dispatch:
```

- [ ] **Step 2: Verify YAML syntax**

Run: `python -c "import yaml; yaml.safe_load(open('.github/workflows/e2e-smoke-real-backend.yml'))" && echo OK`
Expected: `OK`. Se fail: fix indentation.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/e2e-smoke-real-backend.yml
git commit -m "ci(smoke): add path-trigger for game-chat v2 changes (#938)"
```

---

### Task 6: Create `smoke-real-llm/game-night-happy-path.spec.ts`

**Files:**
- Read: `apps/web/e2e/smoke/game-night-happy-path.spec.ts` (sorgente)
- Read: `apps/web/e2e/smoke/_helpers.fixtures.ts` (per Catan SEED_GAME_ID)
- Create: `apps/web/e2e/smoke-real-llm/game-night-happy-path.spec.ts`

**Context:** Spec real-LLM deve girare contro `make dev-from-snapshot` con Catan reale. Riusa SEED_GAME_ID da fixture. NO mock: real OpenRouter free model.

- [ ] **Step 1: Read source + write adapted spec**

Imports:
```ts
import { test, expect } from '@playwright/test';
import { smokeLogin, applySessionToPage } from '../smoke-real-backend/_helpers/auth';

const SEED_GAME_ID = process.env.SMOKE_GAME_ID ?? '3d54901d-fe9d-4d00-a7ae-e9865bb764f7';

test.describe.configure({ retries: 3 });

test.describe('SMOKE-REAL-LLM — game-night happy path (free model)', () => {
  test('@flaky-by-design end-to-end with real LLM', async ({ page, request }) => {
    test.setTimeout(180_000);  // 3 min total per retry
    // ... login, navigate, send query, assert response/citation/confidence
  });
});
```

Assertion principali (NO contenuto LLM-specifico, solo struttura):
- `[data-testid="chat-bubble"][data-role="agent"]` visible
- `[data-slot="citation-chip"]` count >= 1
- `[data-slot="confidence-badge"]` visible
- `[data-slot="citation-modal"]` opens on chip click
- After reload: skeleton + history hydrate

Timeout per LLM step: `{ timeout: 120_000 }` su `waitFor` chiamate.

- [ ] **Step 2: Lint check**

Run: `cd apps/web && pnpm tsc --noEmit 2>&1 | grep "smoke-real-llm" || echo OK`

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/smoke-real-llm/
git commit -m "test(smoke): real-LLM happy path with free model + retry (#938)"
```

---

### Task 7: Create workflow `e2e-smoke-real-llm-weekly.yml`

**Files:**
- Create: `.github/workflows/e2e-smoke-real-llm-weekly.yml`

- [ ] **Step 1: Write workflow**

Basato su template spec §5.2. Sezioni critiche:
- Trigger: `schedule: '0 4 * * 0'` (domenica) + `workflow_dispatch`
- Step "Verify free model availability" (vedi spec §4.3) — exit early se nessun modello free disponibile
- Step "Setup secret + free model" che scrive `infra/secrets/openrouter.secret` con `OPENROUTER_API_KEY` + `OPENROUTER_DEFAULT_MODEL` dinamico (output da step verify)
- Stack: `make dev-from-snapshot` (NON `dev-core`)
- Step "Apply smoke-aaron fixture" (psql import `tests/fixtures/smoke-test-users.sql` su `meepleai_staging`)
- Run: `pnpm playwright test smoke-real-llm/ --project=desktop-chrome --retries=3`
- Upload report on failure
- **NO auto-issue creation** (free tier flake legittimo)

Env passati a Playwright:
```yaml
env:
  SMOKE_API_BASE: http://localhost:8080
  SMOKE_USER_EMAIL: smoke-aaron@meepleai.test
  SMOKE_USER_PASSWORD: SmokeAaron1!!
  SMOKE_GAME_ID: 3d54901d-fe9d-4d00-a7ae-e9865bb764f7
  PLAYWRIGHT_SKIP_WEB_SERVER: '1'
```

- [ ] **Step 2: Verify YAML syntax**

Run: `python -c "import yaml; yaml.safe_load(open('.github/workflows/e2e-smoke-real-llm-weekly.yml'))" && echo OK`

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/e2e-smoke-real-llm-weekly.yml
git commit -m "ci(smoke): add weekly real-LLM workflow with free model fallback (#938)"
```

---

### Task 8: Cleanup obsoleto

**Files:**
- Delete: `apps/web/e2e/smoke/` (intero folder)
- Delete: `.github/workflows/e2e-smoke.yml`

- [ ] **Step 1: Verifica nessun import rimanente**

Run:
```bash
grep -rn "from.*e2e/smoke/\|e2e/smoke\"" apps/web/ 2>/dev/null | grep -v "smoke-real-backend\|smoke-real-llm\|node_modules" || echo "No imports"
```
Expected: `No imports`. Se trova match: investiga, sposta o rimuovi reference prima di delete.

- [ ] **Step 2: Delete folder + workflow**

```bash
rm -rf apps/web/e2e/smoke/
rm .github/workflows/e2e-smoke.yml
```

- [ ] **Step 3: Verifica delete**

Run: `ls apps/web/e2e/smoke 2>&1 | head -1 && ls .github/workflows/e2e-smoke.yml 2>&1 | head -1`
Expected: due "No such file" errors (entrambi cancellati).

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore(smoke): remove obsolete e2e-smoke workflow + smoke/ folder (#938)

Replaced by:
- 3 mock specs in smoke-real-backend/ (nightly)
- 1 real-LLM spec in smoke-real-llm/ (weekly)

See: docs/superpowers/specs/2026-05-10-e2e-smoke-game-night-integration-design.md"
```

---

### Task 9: PR + merge

- [ ] **Step 1: Push branch**

```bash
git push -u origin feature/issue-938-smoke-integration
```

- [ ] **Step 2: Create PR**

```bash
gh pr create --base main-dev --title "ci(smoke): integrate game-night specs into nightly + add weekly real-LLM (#938)" --body "$(cat <<'EOF'
## Summary

Refactor E2E smoke game-night da workflow isolato (cancellato) a B+ ibrida:
- **3 mock specs** integrati in `smoke-real-backend/` (nightly, riusa cron 03:00 UTC)
- **1 real-LLM spec** in nuovo `smoke-real-llm/` (weekly, free model fallback chain)

Risolve fail consecutivi dell'`e2e-smoke.yml` isolato (PR #944-948).

## Decisione architettonica

Spec-panel critique (Crispin/Nygard/Wiegers) → opzione **B+ ibrida**:
- Zero costi LLM nightly (mock SSE deterministici)
- Real-LLM weekly con free tier OpenRouter + fallback chain (mitigation Nygard deprecation)
- Timeout 120s + retry 3 + tag `@flaky-by-design` (mitigation Crispin latenza variabile)

## Cambiamenti

- ✅ `_helpers/game-chat.ts` (seedGameForChat + mockQaStreamV2)
- ✅ 3 mock specs in `smoke-real-backend/`
- ✅ 1 real-LLM spec in `smoke-real-llm/`
- ✅ Update `e2e-smoke-real-backend.yml` (path-trigger su components V2)
- ✅ New `e2e-smoke-real-llm-weekly.yml` (cron domenica + dispatch)
- 🗑️ Cancellato `apps/web/e2e/smoke/` + `e2e-smoke.yml`

## Test plan

- [ ] CI nightly cron 03:00 UTC include i 3 nuovi mock spec
- [ ] PR successivo che modifica `apps/web/src/components/v2/game-chat/` triggera nightly via path
- [ ] Weekly workflow `workflow_dispatch` manuale completa con free model
- [ ] No regression sui 11 spec esistenti in `smoke-real-backend/`

## Riferimenti

- Spec: `docs/superpowers/specs/2026-05-10-e2e-smoke-game-night-integration-design.md`
- Plan: `docs/superpowers/plans/2026-05-10-e2e-smoke-integration.md`
- Issue: #938
EOF
)"
```

- [ ] **Step 3: Wait for CI green + merge**

Run: `gh pr checks --watch` (attendi green).
Run: `gh pr merge --squash --delete-branch` (auto-cleanup branch).

---

## Self-review checklist (controller pre-execution)

- ✅ Spec coverage: tutti i 9 task corrispondono a §6 dello spec
- ✅ No placeholders: ogni step ha comando/codice concreto
- ✅ Type consistency: `seedGameForChat`, `mockQaStreamV2`, `mockNoDocuments` nomi consistenti tra Task 1 e Task 2-4
- ✅ OQ residue: risolte in Task 1 Step 1-2 (verifica API + SSE protocol prima di scrivere helper)
- ⚠️ Risk: API endpoint shape (Task 1 Step 1) potrebbe rivelare che il pattern seed non funziona — fallback documentato in spec §8 OQ1/OQ2
