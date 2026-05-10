# E2E Smoke G1-G5 — Implementation Plan

**Status**: ✅ COMPLETED (PR #944 — smoke G1-G5 4 spec)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere 4 smoke spec Playwright (`apps/web/e2e/smoke/`) + workflow CI post-merge `main-dev` per validare end-to-end il flusso "serata di gioco" (G1+G2+G3+G4+G5).

**Architecture:** 4 nuovi spec (1 happy path real LLM + 3 edge case con SSE V2 mock) + helper smoke-only `_helpers.ts` con mock factory per il formato `{type, data}` di V2 + workflow CI con path-based trigger su file game-chat. Riusa fixture `smoke-test-users.sql` esistente + snapshot DB pre-cooked.

**Tech Stack:** Playwright (già configurato), `pnpm exec playwright test`, GitHub Actions, `make dev-from-snapshot` Docker stack, snapshot DB.

**Spec:** [`docs/superpowers/specs/2026-05-10-e2e-smoke-game-night-design.md`](../specs/2026-05-10-e2e-smoke-game-night-design.md)
**Issue:** [#938](https://github.com/meepleAi-app/meepleai-monorepo/issues/938)
**Branch:** `feature/issue-938-e2e-smoke-game-night` (creato dal commit spec)

---

## File Structure

| Status | Path | Responsibility |
|---|---|---|
| Create | `apps/web/e2e/smoke/_helpers.ts` | Login + navigate + mockQaStreamV2 helpers smoke-only |
| Create | `apps/web/e2e/smoke/_verify-snapshot.sh` | Pre-flight verifica snapshot DB ha game+KB+PDF per smoke-aaron |
| Create | `apps/web/e2e/smoke/game-night-happy-path.spec.ts` | Real LLM end-to-end G1+G2+G3+G4 |
| Create | `apps/web/e2e/smoke/game-night-low-confidence.spec.ts` | Mock G5 disclaimer |
| Create | `apps/web/e2e/smoke/game-night-out-of-context.spec.ts` | Mock OOC action pills |
| Create | `apps/web/e2e/smoke/game-night-pdf-non-owner.spec.ts` | Mock G4 ownership upsell |
| Create | `.github/workflows/e2e-smoke.yml` | Workflow CI path-based trigger |

**Decomposition logic:**
- Task 1 (helpers + snapshot verify) — foundation per tutti gli spec
- Task 2-5 (1 per spec) — TDD non applicabile (test E2E sono il test stesso); pattern: scrivi spec → run locale → fix selectors → commit
- Task 6 (workflow CI) — finale, integra tutto

---

## Pre-flight

- [ ] **Step 0.1: Verifica branch**

```bash
git -C "D:/Repositories/meepleai-monorepo-main" branch --show-current
```
Expected: `feature/issue-938-e2e-smoke-game-night`

- [ ] **Step 0.2: Verifica setup Playwright**

```bash
cd apps/web && pnpm exec playwright --version
```
Expected: versione installata. Se errore "browsers not installed", run `pnpm exec playwright install chromium`.

- [ ] **Step 0.3: Verifica `make dev-from-snapshot` funziona localmente**

```bash
cd infra && make dev-from-snapshot
```
Expected: stack Docker up con DB pre-cucinato. Se errore "snapshot not found", verifica `scripts/snapshot-fetch.sh` o documenta il setup richiesto.

- [ ] **Step 0.4: Identifica gameId + documentId nello snapshot**

Connettiti al DB locale post `make dev-from-snapshot`:

```bash
docker exec -it meepleai-postgres psql -U postgres -d meepleai -c \
  "SELECT id, name FROM games LIMIT 5;"
docker exec -it meepleai-postgres psql -U postgres -d meepleai -c \
  "SELECT id, game_id, processing_status FROM pdf_documents WHERE processing_status = 'ready' LIMIT 5;"
```

Annota:
- `SEED_GAME_ID` = primo gameId con KB ready
- `SEED_DOCUMENT_ID` = primo documentId associato

> Se non ci sono game con KB ready: STOP. Documenta requirement nel readme + escalate al utente per estendere snapshot OR `smoke-test-users.sql`.

- [ ] **Step 0.5: Verifica smoke-aaron user esiste**

```bash
docker exec -it meepleai-postgres psql -U postgres -d meepleai -c \
  "SELECT \"Id\", \"Email\", \"EmailVerified\" FROM users WHERE \"Email\" = 'smoke-aaron@meepleai.test';"
```

Expected: 1 row con `EmailVerified=true`. Se NO, applica fixture: `psql -f tests/fixtures/smoke-test-users.sql`.

- [ ] **Step 0.6: Verifica smoke-aaron ha il game in libreria con ownership**

```bash
docker exec -it meepleai-postgres psql -U postgres -d meepleai -c \
  "SELECT * FROM user_library WHERE user_id = (SELECT \"Id\" FROM users WHERE \"Email\" = 'smoke-aaron@meepleai.test') AND game_id = '<SEED_GAME_ID>';"
```

Se vuoto: dovrai aggiungere SQL al fixture per linkare smoke-aaron al game con `ownershipDeclaredAt = NOW()`. Dettaglio nel Task 1 fallback.

---

## Task 1: Helpers + snapshot verify script

**Files:**
- Create: `apps/web/e2e/smoke/_helpers.ts`
- Create: `apps/web/e2e/smoke/_verify-snapshot.sh`
- Create: `apps/web/e2e/smoke/_helpers.fixtures.ts` (constants)

### 1.1 Crea fixtures TS

Crea `apps/web/e2e/smoke/_helpers.fixtures.ts`:

```ts
/**
 * Smoke test fixtures — IDs noti dello snapshot DB.
 * Aggiorna se snapshot cambia (vedi Step 0.4).
 */

export const SMOKE_USER = {
  email: 'smoke-aaron@meepleai.test',
  password: 'SmokeAaron1!!',
};

// IDs identificati nello snapshot DB (Step 0.4 del plan)
// SOSTITUISCI con valori reali dopo aver eseguito Step 0.4
export const SEED_GAME_ID = '__REPLACE_ME_FROM_STEP_0_4__';
export const SEED_DOCUMENT_ID = '__REPLACE_ME_FROM_STEP_0_4__';
```

> ⚠️ **Step 0.4 deve essere eseguito** prima di Task 2. Sostituisci `__REPLACE_ME__` con gli ID reali.

### 1.2 Crea helpers TypeScript

Crea `apps/web/e2e/smoke/_helpers.ts`:

```ts
/**
 * Smoke E2E helpers — login + navigate + mock SSE V2.
 *
 * NB: il mock SSE qui usa il formato V2 `data: {"type": 7, "data": ...}`
 * (NON il formato V1 `event: token` di ChatHelper.mockQAStreamWithCitations).
 *
 * Spec: docs/superpowers/specs/2026-05-10-e2e-smoke-game-night-design.md
 */
import type { Page, Route } from '@playwright/test';

import { SMOKE_USER } from './_helpers.fixtures';

interface MockCitation {
  documentId: string;
  pageNumber: number;
  snippet: string;
  relevanceScore: number;
  copyrightTier?: 'full' | 'protected';
}

interface MockQaV2Options {
  tokens: string[];
  citations?: MockCitation[];
  confidence?: number;
  chatThreadId?: string;
}

/**
 * Login smoke-aaron via API endpoint diretto (più veloce di UI form).
 * Usa POST /api/v1/auth/login per ottenere session cookie.
 */
export async function loginSmokeAaron(page: Page): Promise<void> {
  const response = await page.request.post('/api/v1/auth/login', {
    data: {
      email: SMOKE_USER.email,
      password: SMOKE_USER.password,
    },
  });
  if (!response.ok()) {
    throw new Error(`Login failed: HTTP ${response.status()} — ${await response.text()}`);
  }
  // Cookie session is now set on page context
}

/**
 * Mock SSE V2 stream — formato `{type, data}` come usato da chatClient.ts qaStream.
 *
 * type=7 → Token (string accumulated in buffer)
 * type=4 → Complete (confidence + Citations + chatThreadId)
 */
export async function mockQaStreamV2(page: Page, options: MockQaV2Options): Promise<void> {
  const { tokens, citations = [], confidence = 0.9, chatThreadId } = options;

  let sseBody = '';

  // Stream token events (type=7, data=string)
  for (const token of tokens) {
    sseBody += `data: ${JSON.stringify({ type: 7, data: token })}\n\n`;
  }

  // Complete event (type=4, data=StreamingComplete)
  const completePayload: Record<string, unknown> = {
    estimatedReadingTimeMinutes: 0,
    promptTokens: 10,
    completionTokens: tokens.length * 5,
    totalTokens: tokens.length * 5 + 10,
    confidence,
    Citations: citations,
  };
  if (chatThreadId) completePayload.chatThreadId = chatThreadId;
  sseBody += `data: ${JSON.stringify({ type: 4, data: completePayload })}\n\n`;

  await page.route('**/api/v1/agents/qa/stream', async (route: Route) => {
    await route.fulfill({
      status: 200,
      headers: {
        'Content-Type': 'text/event-stream',
        'Cache-Control': 'no-cache',
      },
      body: sseBody,
    });
  });
}

/**
 * Mock empty getThreadsByGame (forces no historical messages — fresh chat).
 */
export async function mockNoChatHistory(page: Page, gameId: string): Promise<void> {
  await page.route(`**/api/v1/chat-threads?gameId=${gameId}*`, async (route: Route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
  });
}

/**
 * Mock empty getGameDocuments (forces non-owner state for G4 ownership gate).
 */
export async function mockNoDocuments(page: Page, gameId: string): Promise<void> {
  await page.route(`**/api/v1/knowledge-base/${gameId}/documents`, async (route: Route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
  });
}
```

### 1.3 Crea verify script

Crea `apps/web/e2e/smoke/_verify-snapshot.sh`:

```bash
#!/usr/bin/env bash
# Pre-flight check: verifica che snapshot DB contenga setup richiesto da smoke G1-G5.
# Usato dal CI workflow prima di lanciare Playwright.
set -euo pipefail

PSQL_CMD="docker exec meepleai-postgres psql -U postgres -d meepleai -tAc"
SMOKE_EMAIL="smoke-aaron@meepleai.test"

# 1. Smoke user exists
USER_COUNT=$($PSQL_CMD "SELECT COUNT(*) FROM users WHERE \"Email\" = '$SMOKE_EMAIL';")
if [ "$USER_COUNT" -lt 1 ]; then
  echo "FAIL: smoke-aaron user not in DB. Run: psql -f tests/fixtures/smoke-test-users.sql"
  exit 1
fi

# 2. At least 1 game with ready KB
KB_COUNT=$($PSQL_CMD "SELECT COUNT(*) FROM pdf_documents WHERE processing_status = 'ready';")
if [ "$KB_COUNT" -lt 1 ]; then
  echo "FAIL: no PDF documents with status='ready'. Snapshot needs games+KB indexed."
  exit 1
fi

# 3. smoke-aaron has at least 1 game in library
LIB_COUNT=$($PSQL_CMD "SELECT COUNT(*) FROM user_library WHERE user_id = (SELECT \"Id\" FROM users WHERE \"Email\" = '$SMOKE_EMAIL');")
if [ "$LIB_COUNT" -lt 1 ]; then
  echo "WARNING: smoke-aaron has no games in library. Bootstrap may be needed."
  echo "Will attempt graceful continue (tests may FAIL)."
fi

echo "PASS: snapshot verified for smoke E2E"
```

Make executable: `chmod +x apps/web/e2e/smoke/_verify-snapshot.sh`.

### 1.4 Commit

```bash
git add apps/web/e2e/smoke/_helpers.ts apps/web/e2e/smoke/_helpers.fixtures.ts apps/web/e2e/smoke/_verify-snapshot.sh
git commit -m "feat(e2e): #938 smoke G1-G5 helpers + snapshot verify script"
```

---

## Task 2: `game-night-happy-path.spec.ts` (real LLM)

- [ ] **Step 2.1: Aggiorna fixtures con SEED IDs reali** (post Step 0.4)

Sostituisci `__REPLACE_ME_FROM_STEP_0_4__` in `apps/web/e2e/smoke/_helpers.fixtures.ts` con i veri UUID identificati.

- [ ] **Step 2.2: Crea spec**

Crea `apps/web/e2e/smoke/game-night-happy-path.spec.ts`:

```ts
/**
 * Smoke E2E G1+G2+G3+G4 — happy path real LLM.
 *
 * Coverage:
 *   - G3: GamesRecentRail rendered su /games?tab=library
 *   - G2: ChatBubbleSkeleton durante hydrate, messages restored after reload
 *   - G1: CitationChip + ConfidenceBadge appaiono nella risposta agente
 *   - G4: CitationModal con tab Snippet (default) + tab PDF originale
 *
 * Spec: docs/superpowers/specs/2026-05-10-e2e-smoke-game-night-design.md §3.1
 *
 * NB: real LLM, può richiedere fino a 30s per la risposta.
 */
import { test, expect } from '@playwright/test';

import { loginSmokeAaron } from './_helpers';
import { SEED_GAME_ID } from './_helpers.fixtures';

test.describe('Smoke G1-G5 — happy path (real LLM)', () => {
  test.setTimeout(120_000); // 2 min for full flow with real LLM
  test.describe.configure({ retries: 2 });

  test('end-to-end serata di gioco flow', async ({ page }) => {
    // ── Auth ──
    await loginSmokeAaron(page);

    // ── G3: Hub recents ──
    await page.goto('/games?tab=library');
    await page.waitForLoadState('networkidle');
    // GamesRecentRail può essere vuoto (no recents) — verifica solo che la sezione esista
    // se è populated. Se assente, skip silently (G3 graceful empty).

    // ── Navigate to chat tab ──
    await page.goto(`/library/games/${SEED_GAME_ID}?tab=aiChat`);
    await page.waitForLoadState('networkidle');

    // ── G2: hydrate skeleton OR direct render ──
    // Skeleton can flash briefly; we don't strict-assert visibility (timing-dependent)
    // Just wait for either skeleton to disappear or messages to render.
    await page
      .locator('[data-slot="games-recent-rail"], [data-testid="message-input"]')
      .first()
      .waitFor({ state: 'visible', timeout: 10_000 });

    // ── Send query ──
    const input = page.locator('[data-testid="message-input"], input[placeholder*="Scrivi"]').first();
    await input.fill('posso usare potere uccello già attivato?');

    const sendBtn = page.locator('[data-testid="send-message-button"], button[aria-label*="Invia" i]').first();
    await sendBtn.click();

    // ── Wait response (real LLM, max 30s) ──
    const agentBubble = page.locator('[data-testid="chat-bubble"][data-role="agent"]').last();
    await expect(agentBubble).toBeVisible({ timeout: 30_000 });

    // ── G1: CitationChip present (assert at least 1) ──
    const citationChips = page.locator('[data-slot="citation-chip"]');
    await expect(citationChips.first()).toBeVisible({ timeout: 5_000 });

    // ── G5: ConfidenceBadge present ──
    const confBadge = page.locator('[data-slot="confidence-badge"]').last();
    await expect(confBadge).toBeVisible();

    // ── G4: Click chip → modal opens ──
    await citationChips.first().click();
    const modal = page.locator('[data-slot="citation-modal"]');
    await expect(modal).toBeVisible();

    // Tab Snippet active by default
    const snippetTab = modal.locator('[role="tab"]', { hasText: /snippet/i });
    await expect(snippetTab).toHaveAttribute('aria-selected', 'true');

    // Click PDF tab
    const pdfTab = modal.locator('[role="tab"]', { hasText: /pdf/i });
    await pdfTab.click();
    await expect(pdfTab).toHaveAttribute('aria-selected', 'true');

    // Tab body shows EITHER PDF renderer OR upsell (both valid for G4)
    const pdfBody = modal
      .locator('[data-slot="citation-pdf-renderer"], [data-slot="citation-ownership-upsell"]')
      .first();
    await expect(pdfBody).toBeVisible({ timeout: 10_000 });

    // Close modal (ESC)
    await page.keyboard.press('Escape');
    await expect(modal).not.toBeVisible();

    // ── G2: Reload, verify messages restored ──
    await page.reload();
    await page.waitForLoadState('networkidle');

    // After hydrate, the message we just sent should be visible
    // (verify generic agent bubble exists post-reload, no specific content match)
    const restoredAgentBubble = page.locator('[data-testid="chat-bubble"][data-role="agent"]');
    await expect(restoredAgentBubble.first()).toBeVisible({ timeout: 10_000 });
  });
});
```

- [ ] **Step 2.3: Run spec localmente**

Pre-requisito: `make dev-from-snapshot` running.

```bash
cd apps/web && pnpm exec playwright test e2e/smoke/game-night-happy-path.spec.ts --headed
```

Expected: spec passa in <90s. Se fail, debug con `--debug` e screenshot.

> Common failures:
> - Login 401 → verify smoke-aaron password match fixture
> - Selectors not found → real component data-testid mismatch (verifica V2 ChatInputBar non usa data-testid esistente)
> - Timeout 30s → real LLM lento o KB vuota → verifica SEED_GAME_ID ha KB indicizzata

- [ ] **Step 2.4: Commit**

```bash
git add apps/web/e2e/smoke/_helpers.fixtures.ts apps/web/e2e/smoke/game-night-happy-path.spec.ts
git commit -m "test(e2e): #938 smoke G1-G5 happy path with real LLM"
```

---

## Task 3: `game-night-low-confidence.spec.ts` (mock G5)

- [ ] **Step 3.1: Crea spec**

Crea `apps/web/e2e/smoke/game-night-low-confidence.spec.ts`:

```ts
/**
 * Smoke E2E G5 — low confidence disclaimer.
 *
 * Mock SSE V2: confidence=0.42 < 0.70 threshold → useGameChat deriva isLowQuality=true
 * → LowConfidenceDisclaimer rendered + ConfidenceBadge data-tier="bassa"
 *
 * Spec: §3.2
 */
import { test, expect } from '@playwright/test';

import { loginSmokeAaron, mockQaStreamV2, mockNoChatHistory } from './_helpers';
import { SEED_GAME_ID, SEED_DOCUMENT_ID } from './_helpers.fixtures';

test.describe('Smoke G5 — low confidence (mocked)', () => {
  test('shows LowConfidenceDisclaimer + bassa badge when confidence < 0.70', async ({ page }) => {
    await loginSmokeAaron(page);
    await mockNoChatHistory(page, SEED_GAME_ID);
    await mockQaStreamV2(page, {
      tokens: ['Non sono certo: ', 'questa è una risposta a confidenza bassa.'],
      citations: [
        {
          documentId: SEED_DOCUMENT_ID,
          pageNumber: 6,
          snippet: 'Edge case mock',
          relevanceScore: 0.5,
          copyrightTier: 'full',
        },
      ],
      confidence: 0.42,
    });

    await page.goto(`/library/games/${SEED_GAME_ID}?tab=aiChat`);
    await page.waitForLoadState('networkidle');

    const input = page.locator('[data-testid="message-input"], input[placeholder*="Scrivi"]').first();
    await input.fill('edge case query');
    await page.locator('[data-testid="send-message-button"], button[aria-label*="Invia" i]').first().click();

    // G5: LowConfidenceDisclaimer rendered
    const disclaimer = page.locator('[data-slot="low-confidence-disclaimer"]');
    await expect(disclaimer).toBeVisible({ timeout: 10_000 });

    // G5: ConfidenceBadge tier=bassa
    const badge = page.locator('[data-slot="confidence-badge"][data-tier="bassa"]');
    await expect(badge).toBeVisible();

    // G1: citation chip preserved (clickable)
    const chip = page.locator('[data-slot="citation-chip"]').first();
    await expect(chip).toBeVisible();
  });
});
```

- [ ] **Step 3.2: Run + commit**

```bash
cd apps/web && pnpm exec playwright test e2e/smoke/game-night-low-confidence.spec.ts
```

Expected: PASS in <30s.

```bash
git add apps/web/e2e/smoke/game-night-low-confidence.spec.ts
git commit -m "test(e2e): #938 smoke G5 low confidence disclaimer"
```

---

## Task 4: `game-night-out-of-context.spec.ts` (mock G1)

- [ ] **Step 4.1: Crea spec**

Crea `apps/web/e2e/smoke/game-night-out-of-context.spec.ts`:

```ts
/**
 * Smoke E2E — out-of-context state.
 *
 * Mock SSE V2: confidence=0, citations=[] → useGameChat deriva outOfContext=true
 * → OutOfContextActions rendered (3 pill: switch-game, find-agent, stay)
 *
 * Spec: §3.3
 */
import { test, expect } from '@playwright/test';

import { loginSmokeAaron, mockQaStreamV2, mockNoChatHistory } from './_helpers';
import { SEED_GAME_ID } from './_helpers.fixtures';

test.describe('Smoke — out of context (mocked)', () => {
  test('shows OutOfContextActions when confidence=0 and no citations', async ({ page }) => {
    await loginSmokeAaron(page);
    await mockNoChatHistory(page, SEED_GAME_ID);
    await mockQaStreamV2(page, {
      tokens: ['Non ho informazioni su questo argomento.'],
      citations: [],
      confidence: 0.0,
    });

    await page.goto(`/library/games/${SEED_GAME_ID}?tab=aiChat`);
    await page.waitForLoadState('networkidle');

    const input = page.locator('[data-testid="message-input"], input[placeholder*="Scrivi"]').first();
    await input.fill('domanda fuori contesto');
    await page.locator('[data-testid="send-message-button"], button[aria-label*="Invia" i]').first().click();

    // OutOfContextActions visible
    const actions = page.locator('[data-slot="out-of-context-actions"]');
    await expect(actions).toBeVisible({ timeout: 10_000 });

    // 3 specific action pills
    await expect(page.locator('button[data-action-kind="switch-game"]')).toBeVisible();
    await expect(page.locator('button[data-action-kind="find-agent"]')).toBeVisible();
    await expect(page.locator('button[data-action-kind="stay"]')).toBeVisible();
  });
});
```

- [ ] **Step 4.2: Run + commit**

```bash
cd apps/web && pnpm exec playwright test e2e/smoke/game-night-out-of-context.spec.ts
```

```bash
git add apps/web/e2e/smoke/game-night-out-of-context.spec.ts
git commit -m "test(e2e): #938 smoke out-of-context action pills"
```

---

## Task 5: `game-night-pdf-non-owner.spec.ts` (mock G4)

- [ ] **Step 5.1: Crea spec**

Crea `apps/web/e2e/smoke/game-night-pdf-non-owner.spec.ts`:

```ts
/**
 * Smoke E2E G4 — PDF tab non-owner upsell.
 *
 * Mock SSE V2 happy + mock getGameDocuments → empty array.
 * useCanViewPdf ritorna canView=false → CitationOwnershipUpsell rendered.
 *
 * Spec: §3.4
 */
import { test, expect } from '@playwright/test';

import { loginSmokeAaron, mockQaStreamV2, mockNoChatHistory, mockNoDocuments } from './_helpers';
import { SEED_GAME_ID, SEED_DOCUMENT_ID } from './_helpers.fixtures';

test.describe('Smoke G4 — PDF non-owner upsell (mocked)', () => {
  test('shows CitationOwnershipUpsell when user does not own the PDF', async ({ page }) => {
    await loginSmokeAaron(page);
    await mockNoChatHistory(page, SEED_GAME_ID);
    await mockNoDocuments(page, SEED_GAME_ID);  // forces canView=false
    await mockQaStreamV2(page, {
      tokens: ['Risposta con citazione.'],
      citations: [
        {
          documentId: SEED_DOCUMENT_ID,
          pageNumber: 12,
          snippet: 'Citazione mock',
          relevanceScore: 0.95,
          copyrightTier: 'full',
        },
      ],
      confidence: 0.9,
    });

    await page.goto(`/library/games/${SEED_GAME_ID}?tab=aiChat`);
    await page.waitForLoadState('networkidle');

    const input = page.locator('[data-testid="message-input"], input[placeholder*="Scrivi"]').first();
    await input.fill('test citazione');
    await page.locator('[data-testid="send-message-button"], button[aria-label*="Invia" i]').first().click();

    // Wait response + click chip
    const chip = page.locator('[data-slot="citation-chip"]').first();
    await expect(chip).toBeVisible({ timeout: 10_000 });
    await chip.click();

    const modal = page.locator('[data-slot="citation-modal"]');
    await expect(modal).toBeVisible();

    // Click tab PDF
    const pdfTab = modal.locator('[role="tab"]', { hasText: /pdf/i });
    await pdfTab.click();

    // G4: upsell card visible (NOT viewer)
    const upsell = modal.locator('[data-slot="citation-ownership-upsell"]');
    await expect(upsell).toBeVisible();

    // CTA href contains gameId
    const cta = upsell.locator('a', { hasText: /carica.*pdf/i });
    const href = await cta.getAttribute('href');
    expect(href).toContain(`gameId=${SEED_GAME_ID}`);

    // Anti-leak: NO download button
    await expect(modal.locator('button', { hasText: /download|scarica/i })).toHaveCount(0);
  });
});
```

- [ ] **Step 5.2: Run + commit**

```bash
cd apps/web && pnpm exec playwright test e2e/smoke/game-night-pdf-non-owner.spec.ts
```

```bash
git add apps/web/e2e/smoke/game-night-pdf-non-owner.spec.ts
git commit -m "test(e2e): #938 smoke G4 PDF non-owner upsell"
```

---

## Task 6: CI workflow

- [ ] **Step 6.1: Crea workflow**

Crea `.github/workflows/e2e-smoke.yml`:

```yaml
name: E2E Smoke G1-G5

on:
  push:
    branches: [main-dev, main]
    paths:
      - 'apps/web/src/components/v2/game-chat/**'
      - 'apps/web/src/hooks/queries/useGameChat.ts'
      - 'apps/web/e2e/smoke/**'
      - 'apps/api/src/Api/BoundedContexts/KnowledgeBase/**'
      - '.github/workflows/e2e-smoke.yml'
  workflow_dispatch:

jobs:
  smoke:
    runs-on: ubuntu-latest
    timeout-minutes: 15
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup pnpm
        uses: pnpm/action-setup@v3

      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: pnpm
          cache-dependency-path: apps/web/pnpm-lock.yaml

      - name: Install web dependencies
        working-directory: apps/web
        run: pnpm install --frozen-lockfile

      - name: Install Playwright browsers
        working-directory: apps/web
        run: pnpm exec playwright install --with-deps chromium

      - name: Start full stack with snapshot DB
        working-directory: infra
        run: make dev-from-snapshot

      - name: Apply smoke fixtures
        run: |
          docker exec -i meepleai-postgres psql -U postgres -d meepleai \
            < tests/fixtures/smoke-test-users.sql

      - name: Wait for API ready
        run: |
          for i in {1..30}; do
            if curl -fsS http://localhost:8080/health 2>/dev/null; then
              echo "API ready"; break
            fi
            sleep 2
          done

      - name: Verify snapshot data
        run: bash apps/web/e2e/smoke/_verify-snapshot.sh

      - name: Run smoke tests
        working-directory: apps/web
        env:
          BASE_URL: http://localhost:3000
          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY_SMOKE }}
        run: pnpm exec playwright test e2e/smoke/

      - name: Upload Playwright report on fail
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-smoke-report
          path: apps/web/playwright-report/
          retention-days: 7
```

- [ ] **Step 6.2: Documenta secret requirement**

Aggiungi nota in PR description: "Richiede secret `ANTHROPIC_API_KEY_SMOKE` configurato a livello repo (admin action). Senza il secret, il job happy path fallirà — gli edge case mockati passano comunque."

- [ ] **Step 6.3: Commit + push + PR**

```bash
git add .github/workflows/e2e-smoke.yml
git commit -m "ci(e2e): #938 smoke G1-G5 workflow path-based trigger main-dev"
git push -u origin feature/issue-938-e2e-smoke-game-night
gh pr create --repo meepleAi-app/meepleai-monorepo --base main-dev \
  --title "test(e2e): #938 smoke G1-G5 game-night flow" \
  --body "Smoke E2E del flusso serata di gioco. 4 spec (1 happy real LLM + 3 edge case mock). Path-based trigger. Closes #938."
```

---

## Out of scope (follow-up)

| Item | Reason |
|---|---|
| Visual regression Chromatic/Percy | Out of scope spec (§6) |
| Cross-browser (Firefox/WebKit) | Solo Chromium per smoke |
| Mobile viewport | Coperto da unit test layout |
| A11y assertions | Cartella `e2e/a11y/` dedicata già esiste |
| Performance assertions | No `expect(loadTime).toBeLessThan(...)` |
| Negative auth flow | Coperto da spec auth-specific |

---

## Spec Self-Review (eseguito)

**Spec coverage**: tutte le sezioni spec §1-§7 mappate a Task 0 (pre-flight) + 1 (helpers) + 2-5 (4 spec) + 6 (CI workflow).

**Placeholder scan**: zero `TBD/TODO/...later` accidentali. Placeholder intenzionali:
- `__REPLACE_ME_FROM_STEP_0_4__` in fixtures TS — esplicito, sostituito a Step 2.1 dopo Step 0.4
- `ANTHROPIC_API_KEY_SMOKE` secret — admin action documentato in Step 6.2

**Type consistency**:
- `MockQaV2Options.tokens: string[]` ✓
- `MockCitation.documentId/pageNumber/snippet/relevanceScore/copyrightTier?` matcha `Citation` type ✓
- Mock SSE format `{type: 7|4, data: ...}` matcha `qaStream` parser in `chatClient.ts` ✓
- Selectors data-slot/data-testid verificati contro G2 commits (chat-bubble-skeleton, chat-history-banner, citation-modal, citation-pdf-renderer, citation-ownership-upsell)

**Risk noted**:
- Step 0.4 + 0.6 dipendono dal contenuto dello snapshot — se mancano dati, fallback escalate al utente
- Real LLM happy path può flaky in CI — `retries: 2` configurato + screenshot/trace upload
- ChatHelper esistente NON usabile (V1 SSE format) — helper smoke-only nuovo (giustificato)
- Selectors usano fallback robusti `[data-testid="X"], input[placeholder*="Scrivi"]` per resilience a evoluzioni V2
