# E2E Smoke Game-Night — Integration into Nightly + Free LLM Weekly

**Status**: ✅ COMPLETED 2026-05-10 (PR #956 — nightly integration + weekly real-LLM)
**Date**: 2026-05-10
**Author**: Refactor session post #948
**Status**: APPROVED — B+ ibrida (3 mock nightly + 1 real-LLM weekly free tier)
**Supersedes (partially)**: `2026-05-10-e2e-smoke-game-night-design.md` §5 (workflow)

---

## 1. Contesto

I 4 spec creati in PR #944 (`apps/web/e2e/smoke/game-night-*.spec.ts`) sono attualmente in un workflow isolato (`e2e-smoke.yml`) con auto-trigger disabilitato dopo 3 fail consecutivi (PR #948). La spec originale assumeva `make dev-from-snapshot` + `smoke-aaron@meepleai.test` + Catan SEED IDs hardcoded — approccio incompatibile con il nightly esistente `e2e-smoke-real-backend.yml` che usa `make dev-core` (DB vuoto + admin seedato via env).

Decisione `/sc:spec-panel` (Crispin/Nygard/Wiegers): **B+ ibrida**.

## 2. Architettura target

```
apps/web/e2e/
├── smoke-real-backend/                      ← REUSE nightly cron infra
│   ├── _helpers/
│   │   ├── auth.ts                          ← REUSE smokeLogin/applySessionToPage
│   │   └── game-chat.ts                     ← NEW (mockQaStreamV2 + game seeding helper)
│   ├── game-night-low-confidence.smoke.spec.ts        ← MIGRATE+ADAPT
│   ├── game-night-out-of-context.smoke.spec.ts        ← MIGRATE+ADAPT
│   └── game-night-pdf-non-owner.smoke.spec.ts         ← MIGRATE+ADAPT
└── smoke-real-llm/                          ← NEW (separato per opt-in run)
    ├── _helpers/
    │   └── (riusa smoke-real-backend/_helpers/auth via import path)
    └── game-night-happy-path.spec.ts        ← MIGRATE+ADAPT (free model + retry)

.github/workflows/
├── e2e-smoke-real-backend.yml               ← UPDATE (aggiunge path-trigger su components v2)
└── e2e-smoke-real-llm-weekly.yml            ← NEW (cron weekly + workflow_dispatch)

DELETE:
├── .github/workflows/e2e-smoke.yml
├── apps/web/e2e/smoke/                      ← intero folder
```

## 3. Decisioni architetturali

| # | Decisione | Razionale |
|---|---|---|
| D1 | 3 mock spec → `smoke-real-backend/` (riuso cron nightly) | Zero infra cost, deterministico (no LLM), pattern consistente con altri 11 spec esistenti |
| D2 | 1 real-LLM spec → workflow weekly separato | Schedule-only-weekly evita flake daily; dispatch manuale per pre-staging validation |
| D3 | Free model con fallback chain | Mitigation Nygard CRITICAL: deprecation free tier silenziosa |
| D4 | Auth: `smokeLogin()` admin (smoke-user@meepleai.test) | Riusa fixture nightly esistente; smoke-aaron specifico per smoke-aaron è over-engineering per 3 mock spec |
| D5 | Game seeding: inline via API admin in `beforeAll` | `dev-core` DB vuoto; pattern simile a `library.smoke.spec.ts`; <1s overhead |
| D6 | Real-LLM stack: `dev-from-snapshot` (riuso Catan + rulebook) | Snapshot ha già RAG indicizzato per Catan; real LLM serve query reale su KB reale |
| D7 | Real-LLM timeout 120s + retry 3 + tag `@flaky-by-design` | Mitigation Crispin per latenza variabile free tier (5-60s) |
| D8 | Auto-issue su nightly fail rimane attivo; weekly real-LLM NO auto-issue | Real-LLM weekly può flake legitimately (free tier rate limit, model deprecation) — non vogliamo issue spam |

## 4. Spec dettaglio

### 4.1 Spec mock nightly (3 file → `smoke-real-backend/`)

**Setup pattern condiviso** (in `_helpers/game-chat.ts`):

```ts
// _helpers/game-chat.ts
import type { APIRequestContext, Page } from '@playwright/test';

const API_BASE = process.env.SMOKE_API_BASE ?? 'http://localhost:8080';

/**
 * Seeds a shared-game + adds it to admin library, returns gameId.
 * Idempotent: if game with seed name exists, returns its id.
 */
export async function seedGameForChat(
  request: APIRequestContext,
  cookieHeader: string
): Promise<string> {
  const seedName = `smoke-chat-${process.env.GITHUB_RUN_ID ?? 'local'}`;
  // POST /api/v1/shared-games con auth admin
  const createRes = await request.post(`${API_BASE}/api/v1/shared-games`, {
    headers: { Cookie: cookieHeader, 'Content-Type': 'application/json' },
    data: { name: seedName, publisher: 'smoke', minPlayers: 2, maxPlayers: 4 },
  });
  if (!createRes.ok() && createRes.status() !== 409) {
    throw new Error(`seedGame failed ${createRes.status()}`);
  }
  const body = await createRes.json();
  // Add to admin user library
  await request.post(`${API_BASE}/api/v1/library/games`, {
    headers: { Cookie: cookieHeader, 'Content-Type': 'application/json' },
    data: { sharedGameId: body.id },
  });
  return body.id;
}

/**
 * Mock SSE response per qaStream V2 — accetta confidence/citations/outOfContext
 */
export async function mockQaStreamV2(
  page: Page,
  opts: {
    answer?: string;
    citations?: Array<{ documentId: string; pageNumber: number; snippet: string }>;
    confidence?: number;
    outOfContext?: boolean;
  }
): Promise<void> {
  const sseBody = buildSseBody(opts);
  await page.route('**/api/v1/qa/stream**', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'text/event-stream',
      body: sseBody,
    });
  });
}

function buildSseBody(opts: { /* ... */ }): string {
  // Format: { type: 1, data: "delta" } per token + { type: 7, data: complete }
  // Vedi apps/web/src/lib/chatClient.ts per protocol
  return [
    `data: ${JSON.stringify({ type: 1, data: opts.answer ?? 'mock answer' })}\n\n`,
    `data: ${JSON.stringify({
      type: 7,
      data: {
        citations: opts.citations ?? [],
        confidence: opts.confidence ?? 0.85,
        outOfContext: opts.outOfContext ?? false,
      },
    })}\n\n`,
  ].join('');
}

/**
 * Mock empty document list per game (per ownership upsell test).
 */
export async function mockNoDocuments(page: Page, gameId: string): Promise<void> {
  await page.route(`**/api/v1/knowledge-base/${gameId}/documents`, async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
  });
}
```

**Test pattern** (esempio `game-night-low-confidence.smoke.spec.ts`):

```ts
import { test, expect } from '@playwright/test';
import { smokeLogin, applySessionToPage } from './_helpers/auth';
import { seedGameForChat, mockQaStreamV2 } from './_helpers/game-chat';

test.describe('SMOKE — game-chat low confidence (G5 disclaimer)', () => {
  test('low confidence triggers disclaimer + tier badge bassa', async ({ page, request }) => {
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);
    const gameId = await seedGameForChat(request, cookieHeader);

    await mockQaStreamV2(page, {
      answer: 'Risposta incerta...',
      confidence: 0.42,
      citations: [{ documentId: 'mock-doc-1', pageNumber: 1, snippet: 'snippet text' }],
    });

    await page.goto(`/library/games/${gameId}?tab=aiChat`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-testid="message-input"]', { timeout: 30_000 });

    await page.fill('[data-testid="message-input"]', 'test query');
    await page.click('[data-testid="send-message-button"]');

    await expect(page.locator('[data-slot="low-confidence-disclaimer"]')).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('[data-slot="confidence-badge"][data-tier="bassa"]')).toBeVisible();
    await expect(page.locator('[data-slot="citation-chip"]').first()).toBeVisible();
  });
});
```

**Acceptance criteria mock spec (3)**:
- Spec singolo gira <30s
- Zero LLM calls (mock SSE)
- Idempotent seed (può girare 100 volte di fila)
- Selectors via `data-testid`/`data-slot` (no testo italiano)

### 4.2 Spec real-LLM weekly (`smoke-real-llm/game-night-happy-path.spec.ts`)

**Stack**: `make dev-from-snapshot` (riuso Catan + rulebook). Riusa `SEED_GAME_ID` Catan.

**Auth**: smoke-aaron via SQL fixture (già esistente in `tests/fixtures/smoke-test-users.sql`).

**Test scenario**:
1. Login smoke-aaron via API
2. Navigate `/library/games/{CATAN_ID}?tab=aiChat`
3. Send query "quanti giocatori serve per Catan?"
4. Wait response real LLM (timeout 120s)
5. Assert: chat-bubble agent visible, ≥1 citation-chip, confidence-badge present
6. Click citation → modal opens
7. Switch tab "PDF originale" → renderer OR upsell (dipende ownership)
8. Reload → ChatBubbleSkeleton + history hydrated (G2)

**Free tier mitigation**:
- `test.describe.configure({ retries: 3 })`
- `timeout: 120_000` per ogni step LLM
- Tag spec: `test('@flaky-by-design happy path', ...)`
- Workflow step "verify free model available" via `curl https://openrouter.ai/api/v1/models` prima del run

### 4.3 Free model fallback chain

In `infra/secrets/openrouter.secret` (workflow weekly):
```
OPENROUTER_API_KEY=${{ secrets.OPENROUTER_API_KEY }}
OPENROUTER_DEFAULT_MODEL=meta-llama/llama-3.3-70b-instruct:free
```

Workflow step pre-test:
```bash
- name: Verify free model availability (Nygard mitigation)
  run: |
    AVAILABLE=$(curl -s https://openrouter.ai/api/v1/models | \
      jq -r '.data[].id' | grep -E ':free$' | head -3)
    echo "Available free models: $AVAILABLE"
    PRIMARY="meta-llama/llama-3.3-70b-instruct:free"
    if echo "$AVAILABLE" | grep -q "$PRIMARY"; then
      MODEL="$PRIMARY"
    elif echo "$AVAILABLE" | grep -q "deepseek/deepseek-chat:free"; then
      MODEL="deepseek/deepseek-chat:free"
    elif echo "$AVAILABLE" | grep -q "google/gemini-2.0-flash-exp:free"; then
      MODEL="google/gemini-2.0-flash-exp:free"
    else
      echo "::error::No free model available — aborting"
      exit 1
    fi
    echo "OPENROUTER_DEFAULT_MODEL=$MODEL" >> infra/secrets/openrouter.secret
```

## 5. Workflow updates

### 5.1 `e2e-smoke-real-backend.yml` — UPDATE path-trigger

Add `on.push` block (oltre a `schedule` esistente):
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
  workflow_dispatch:
```

Effetto: i 3 nuovi mock spec girano automatico su PR-merge che tocca chat V2.

### 5.2 `e2e-smoke-real-llm-weekly.yml` — NEW

```yaml
name: E2E Smoke Real LLM Weekly (free tier)

on:
  schedule:
    - cron: '0 4 * * 0'  # Domenica 04:00 UTC
  workflow_dispatch:

jobs:
  smoke-real-llm:
    runs-on: ubuntu-latest
    timeout-minutes: 30
    steps:
      - uses: actions/checkout@v4
      - name: Setup secrets + free model fallback
        # ... vedi §4.3
      - name: Boot full snapshot stack
        working-directory: infra
        run: make dev-from-snapshot
      - name: Apply smoke-aaron fixture
        run: |
          docker exec -i meepleai-postgres psql -U meepleai -d meepleai_staging \
            < tests/fixtures/smoke-test-users.sql
      - name: Setup pnpm + Node + Playwright
        # ... pattern da nightly
      - name: Run real-LLM smoke
        env:
          SMOKE_USER_EMAIL: smoke-aaron@meepleai.test
          SMOKE_USER_PASSWORD: SmokeAaron1!!
          SMOKE_GAME_ID: 3d54901d-fe9d-4d00-a7ae-e9865bb764f7
        run: pnpm playwright test smoke-real-llm/ --project=desktop-chrome --retries=3
      - name: Upload report on failure
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-report-real-llm-weekly
          path: apps/web/playwright-report
      # NB: NO auto-issue creation — flake legittimo non deve spammare issues
```

### 5.3 `e2e-smoke.yml` — DELETE

File rimosso completamente. Sostituito da:
- 3 spec → nightly (5.1)
- 1 spec → weekly (5.2)

## 6. Migration tasks summary

| Task | File | Action |
|---|---|---|
| 1 | `apps/web/e2e/smoke-real-backend/_helpers/game-chat.ts` | CREATE — helpers seed/mock |
| 2 | `apps/web/e2e/smoke-real-backend/game-night-low-confidence.smoke.spec.ts` | CREATE — adapted from smoke/ |
| 3 | `apps/web/e2e/smoke-real-backend/game-night-out-of-context.smoke.spec.ts` | CREATE — adapted from smoke/ |
| 4 | `apps/web/e2e/smoke-real-backend/game-night-pdf-non-owner.smoke.spec.ts` | CREATE — adapted from smoke/ |
| 5 | `apps/web/e2e/smoke-real-llm/game-night-happy-path.spec.ts` | CREATE — adapted from smoke/ |
| 6 | `.github/workflows/e2e-smoke-real-backend.yml` | UPDATE — add path-trigger |
| 7 | `.github/workflows/e2e-smoke-real-llm-weekly.yml` | CREATE — weekly free-LLM workflow |
| 8 | `apps/web/e2e/smoke/` | DELETE — intero folder (4 spec + 2 helpers + script) |
| 9 | `.github/workflows/e2e-smoke.yml` | DELETE |

## 7. Verifica post-merge

1. **Nightly run**: il prossimo run cron 03:00 UTC deve includere i 3 nuovi spec, totale ≤30 min
2. **Path-trigger**: PR test che modifica `apps/web/src/components/v2/game-chat/ChatBubble.tsx` deve triggerare nightly workflow
3. **Weekly run**: trigger manuale `workflow_dispatch` per validare prima del cron
4. **Cleanup**: confermare che `apps/web/e2e/smoke/` è vuoto e `e2e-smoke.yml` non esiste

## 8. Open questions residue

- **OQ1**: API endpoint `POST /api/v1/shared-games` esiste con auth admin? Verifica nel plan Step 1
- **OQ2**: API endpoint `POST /api/v1/library/games` accetta `sharedGameId` come body? Verifica nel plan Step 1
- **OQ3**: La risposta `POST /api/v1/qa/stream` è davvero text/event-stream o `application/x-ndjson`? Verifica `chatClient.ts` per format esatto
- **OQ4**: SSE event `type: 7` (StreamingComplete) ha proprio shape `{citations, confidence, outOfContext}` o serve mappatura? Verifica `Contracts.cs` + `chatClient.ts`

## 9. Riferimenti

- Spec originale: `2026-05-10-e2e-smoke-game-night-design.md`
- Issue: [#938](https://github.com/meepleAi-app/meepleai-monorepo/issues/938)
- Spec-panel decision: B+ ibrida (Crispin+Nygard+Wiegers consensus)
- Workflow nightly esistente: `e2e-smoke-real-backend.yml`
- Pattern auth helper: `apps/web/e2e/smoke-real-backend/_helpers/auth.ts`
