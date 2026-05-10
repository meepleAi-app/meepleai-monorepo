# E2E Smoke G1-G5 — Design Spec

**Date**: 2026-05-10
**Author**: Brainstorming session post-G2 merge
**Issue**: [#938](https://github.com/meepleAi-app/meepleai-monorepo/issues/938)
**Status**: DRAFT — pending user review

---

## 1. Contesto

Smoke test E2E che valida il flusso "serata di gioco" end-to-end (G1+G2+G3+G4+G5) post-merge su `main-dev`. Detection regression prima di staging deploy. Riusa Playwright esistente (configurato già con 100+ spec, POM `ChatHelper`, fixtures) e snapshot DB pre-cooked.

### 1.1 Scope-frame

**In scope:**
- 4 nuovi spec in `apps/web/e2e/smoke/` (~400 LOC totali)
- 1 happy path con real LLM (testa pipeline completa)
- 3 edge case con mock SSE (deterministici per frontend behavior)
- 1 workflow CI `.github/workflows/e2e-smoke.yml` con path-based trigger
- Riusa `smoke-aaron@meepleai.test` fixture esistente
- Riusa snapshot DB (`make dev-from-snapshot`)
- Riusa POM `ChatHelper.mockQAStreamWithCitations` esistente

**Out of scope:**
- Visual regression (Chromatic/Percy)
- Cross-browser (solo Chromium per smoke)
- Mobile viewport (unit test layout sufficienti)
- A11y assertions (cartella dedicata `e2e/a11y/` esiste già)
- Performance assertions strette
- Negative auth flow

### 1.2 Decisioni di design (validate via brainstorming)

| # | Domanda | Scelta |
|---|---|---|
| 1 | Target environment | **B** Local Docker (`make dev-from-snapshot`) |
| 2 | Journey scope | **C** 1 happy path + 3 edge case |
| 3 | LLM strategy | **C** Hybrid (happy=real, edge=mock POM) |
| 4 | CI integration | **B** Post-merge main-dev (path-based trigger) |
| 5 | Test data | **B+D** smoke-aaron user + snapshot DB |

### 1.3 Vincoli SMART

| Vincolo | Misurabile |
|---|---|
| Smoke completo in <15 min CI | Workflow `timeout-minutes: 15` |
| Happy path <90s | Playwright timeout assert |
| Edge case spec <30s ognuno | Mock SSE = deterministic |
| Costo LLM <$1/giorno | Solo happy path real, ~$0.02/run × ~10-20 merge = $0.20-$0.40 |
| Path-based trigger evita run gratuiti | `paths:` filter su `apps/web/src/components/v2/game-chat/**` |
| 0 flaky test post 1 settimana | Real LLM happy path con `retry: 2`, mock con `retry: 0` |

---

## 2. Architecture

```
apps/web/e2e/
├── smoke/                                      ← NEW directory
│   ├── game-night-happy-path.spec.ts          ← Real LLM flow G1+G2+G3+G4
│   ├── game-night-low-confidence.spec.ts      ← Mock G5 disclaimer
│   ├── game-night-out-of-context.spec.ts      ← Mock OOC action pills
│   ├── game-night-pdf-non-owner.spec.ts       ← Mock G4 ownership upsell
│   └── _helpers.ts                             ← NEW (login + navigate helpers smoke-only)
└── pages/
    └── ChatHelper.ts                           ← REUSE (mockQAStreamWithCitations esistente)

.github/workflows/
└── e2e-smoke.yml                               ← NEW workflow

docs/superpowers/
├── specs/2026-05-10-e2e-smoke-game-night-design.md
└── plans/2026-05-10-e2e-smoke-game-night.md
```

---

## 3. Test scenarios (4 spec)

### 3.1 `game-night-happy-path.spec.ts` (real LLM)

```
1. Login smoke-aaron@meepleai.test (login form via API o UI form)
2. Navigate /games?tab=library
3. Verify GamesRecentRail rendered (G3, may be empty)
4. Click first game card OR navigate /library/games/[seedGameId]?tab=aiChat
5. Verify ChatBubbleSkeleton brevemente visibile, poi messages se thread esiste (G2 hydrate)
6. Send query "posso usare potere uccello già attivato?"
7. Wait response (real LLM, timeout 30s)
8. Assert: chat-bubble[data-role=agent] visible
9. Assert: at least 1 citation-chip present (G1)
10. Assert: confidence-badge present (G5 baseline)
11. Click first citation-chip
12. Assert: citation-modal dialog open
13. Assert: tab "Snippet" active by default
14. Click tab "PDF originale"
15. Assert: either citation-pdf-renderer OR citation-ownership-upsell present (G4)
16. Close modal (ESC or close button)
17. page.reload()
18. Assert: ChatBubbleSkeleton brief, then messages restored (G2)
19. Assert: ChatHistoryBanner visible IF prev session had messages (G2 condition)
```

**Acceptance**: tutto in <90s, zero assert su contenuto LLM specifico (solo presenza/structure).

### 3.2 `game-night-low-confidence.spec.ts` (mock)

```
1. Login + navigate /library/games/[seedGameId]?tab=aiChat
2. ChatHelper.mockQAStreamWithCitations({ confidence: 0.42, citations: [{...}] })
3. Send query
4. Assert: low-confidence-disclaimer visible (data-slot)
5. Assert: confidence-badge[data-tier="bassa"] visible (G5 fix)
6. Assert: citation-chip still clickable (G1 preserved)
```

### 3.3 `game-night-out-of-context.spec.ts` (mock)

```
1. Login + navigate
2. ChatHelper.mockQAStreamWithCitations({ confidence: 0.0, citations: [] })
3. Send query
4. Assert: out-of-context-actions visible (3 buttons)
5. Assert: button[data-action-kind=switch-game] present
6. Assert: button[data-action-kind=find-agent] present
7. Assert: button[data-action-kind=stay] present
```

### 3.4 `game-night-pdf-non-owner.spec.ts` (mock)

```
1. Login + navigate
2. Mock SSE happy + page.route('**/knowledge-base/*/documents', empty array)
3. Send query → response with citation
4. Click citation chip → modal opens, tab Snippet active
5. Click tab "PDF originale"
6. Assert: citation-ownership-upsell visible
7. Assert: link "Carica il mio PDF" href contains '/upload?gameId=' (G4)
8. Assert: NO download button visible (anti-leak)
```

---

## 4. Pre-requirements & risk

### 4.1 Snapshot DB requirements

**Critico**: lo snapshot DB deve includere:
- `smoke-aaron@meepleai.test` con tier free, EmailVerified=true (già nel fixture esistente)
- Almeno 1 gioco con KB indicizzato (RAG ready, status='indexed')
- Almeno 1 PDF associato al gioco con `Citation.documentId` ottenibile
- Smoke-aaron in `user_library` per quel gioco con `ownershipDeclaredAt != null`

**Pre-flight check** in CI workflow:
```bash
psql ... -c "SELECT id FROM games WHERE id = '<seed-game-id>';" || exit 1
psql ... -c "SELECT id FROM pdf_documents WHERE game_id = '<seed-game-id>' AND processing_status = 'ready';" || exit 1
```

Fallback se snapshot non include: estendere `tests/fixtures/smoke-test-users.sql` con SQL aggiuntivo per game+KB+PDF link (nel plan).

### 4.2 Selectors stabili (data-testid)

Uso esclusivo di `data-testid` o `data-slot` (locale-independent):
- `data-testid="message-input"`, `data-testid="send-message-button"` (ChatInputBar — esistenti V1, verifica V2)
- `data-testid="chat-bubble"` + `data-role`, `data-historical` (ChatBubble V2)
- `data-testid="chat-bubble-skeleton"` (ChatBubbleSkeleton G2)
- `data-slot="chat-history-banner"` (ChatHistoryBanner G2)
- `data-slot="citation-chip"` (CitationChip G1)
- `data-slot="confidence-badge"` + `data-tier` (G5)
- `data-slot="low-confidence-disclaimer"` (G5)
- `data-slot="out-of-context-actions"` + `data-action-kind` (G1)
- `data-slot="citation-modal"` (G1)
- `data-slot="citation-pdf-renderer"` (G4)
- `data-slot="citation-ownership-upsell"` (G4)

NON usare match su testo italiano per assertions critiche.

### 4.3 LLM cost estimate

Sonnet 4: ~$0.003 input + $0.015 output per query Q&A media.
- Trigger frequency: ~10-20 merge/giorno su main-dev (paths matched)
- Cost/run: 1 query happy path ≈ $0.02
- Daily: $0.20-$0.40
- Monthly: ~$10 — accettabile per smoke value

---

## 5. CI workflow

`.github/workflows/e2e-smoke.yml`:

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
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 20
      - uses: pnpm/action-setup@v3
      - name: Install dependencies
        run: cd apps/web && pnpm install --frozen-lockfile
      - name: Install Playwright browsers
        run: cd apps/web && pnpm exec playwright install chromium
      - name: Start full stack with snapshot DB
        run: cd infra && make dev-from-snapshot
      - name: Apply smoke fixtures
        run: psql -f tests/fixtures/smoke-test-users.sql
      - name: Wait for API ready
        run: ./scripts/wait-for-services.sh || sleep 30
      - name: Verify snapshot data
        run: ./apps/web/e2e/smoke/_verify-snapshot.sh
      - name: Run smoke tests
        run: cd apps/web && pnpm exec playwright test e2e/smoke/
        env:
          BASE_URL: http://localhost:3000
          SMOKE_USER_EMAIL: smoke-aaron@meepleai.test
          SMOKE_USER_PASSWORD: SmokeAaron1!!
          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY_SMOKE }}
      - name: Upload Playwright report on fail
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-smoke-report
          path: apps/web/playwright-report/
          retention-days: 7
```

---

## 6. Testing strategy meta

- **Snapshot validity**: pre-step `_verify-snapshot.sh` controlla presenza game+KB+PDF
- **Flaky mitigation**: real LLM happy path `retry: 2`, mock spec `retry: 0`
- **Failure debug**: `playwright-report/` upload su fail (screenshot + trace)
- **Status visibility**: badge in README "Last smoke run"

---

## 7. Open questions residue (delegate al plan)

- **OQ1**: il snapshot DB (`make dev-from-snapshot`) include game+KB+PDF utilizzabili da smoke-aaron? Verifica nel plan Step 0; fallback estendere fixture SQL.
- **OQ2**: esiste `scripts/wait-for-services.sh`? Se no, usare `sleep 30` o health-check loop.
- **OQ3**: `_verify-snapshot.sh` requires `psql` su CI runner — disponibile via `apt-get` setup step.
- **OQ4**: `ANTHROPIC_API_KEY_SMOKE` secret va creato a livello repo settings (admin action, fuori scope code).
- **OQ5**: il login form di smoke-aaron — usare API endpoint diretto (più veloce) o UI form? Decisione delegata al plan.

---

## 8. Riferimenti

- Playwright config: `apps/web/playwright.config.ts`
- POM esistente: `apps/web/e2e/pages/ChatHelper.ts` (`mockQAStreamWithCitations`)
- Fixture user: `tests/fixtures/smoke-test-users.sql` (smoke-aaron persona)
- Snapshot workflow: `infra/Makefile` target `dev-from-snapshot`
- G-series merged PRs: #907 (G3), #918 (G1+G5), #926 (G4), #933 (G2)
- Issue: [#938](https://github.com/meepleAi-app/meepleai-monorepo/issues/938)
