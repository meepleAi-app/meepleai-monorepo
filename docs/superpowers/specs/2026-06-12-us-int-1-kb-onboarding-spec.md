---
status: NOT READY (umbrella — needs 3 ADR + 3 P0 resolved + 4 sub-spec + 1 E2E test)
issue: 2213
spec-panel: 2026-06-11 (Wiegers · Cockburn · Adzic · Fowler · Newman · Nygard · Crispin)
verdict: spec is NOT READY for implementation — open as umbrella tracker until P0 cluster resolved
---

# US-INT-1 — KB Onboarding → Chat (Mage Knight scenario)

## Source story

> Un utente logga nell'app. Cerca "Mage Knight", lo trova ma lo shared games
> non è stato ancora indicizzato. Aggiunge alla sua libreria il PDF di Mage
> Knight. Invia il PDF e quando l'indicizzazione è completa, verrà inviata
> una notification (email/in-app). Dalla pagina del gioco avvia una chat con
> un agente che usa il PDF appena indicizzato.

## Cockburn happy path (7 step)

| # | Step | Actor | System | Acceptance criteria |
|---|---|---|---|---|
| 1 | Login | Andrea | Auth + redirect | session valida; redirect honor `?from=` safe (post #2168) |
| 2 | Search "Mage Knight" | Andrea types DiscoverHub | match in result list | result ≤2s, exact title highlighted |
| 3 | Identifica stato KB | Andrea reads card | badge "non indicizzato" | stato visibile pre-click |
| 4 | Add + upload PDF | Andrea clicks CTA + drop PDF | accoda pipeline | upload ≤200MB, MIME validato, state `Queued` |
| 5 | Indicizzazione async | — | Extraction → Chunking → Embedding → Indexed | state machine, SLA TBD |
| 6 | Notification | — | Email + in-app | dedup per `type+gameId+pdfId`, delivery ≤30s |
| 7 | Avvia chat | Andrea clicks deep link | /games/{id}/chat con agent + pdf | first token ≤3s, citation visible |

## Refined acceptance criteria (full Given/When/Then)

```gherkin
Given Andrea (User role) has session valid
And shared game "Mage Knight" exists in catalog
And shared game has KB state = "NotIndexed"
And Andrea owns local PDF "mage-knight-rulebook.pdf" (≤200MB, application/pdf)

When Andrea navigates /discover, types "Mage Knight", clicks the card
Then Within 2s game detail renders
And A badge "KB non indicizzato — nessun documento" is visible above fold
And A CTA "Aggiungi alla libreria + carica PDF" is enabled

When Andrea clicks CTA, drops PDF, submits
Then Within 1s toast "Upload ricevuto — indicizzazione in corso"
And Library counter increments by 1 (consistent across /dashboard KPI and /library hero — see #2176)
And Game detail "Documenti" tab shows PDF state = "Processing"

When backend completes pipeline (Extraction → Chunking → Embedding → Indexed)
Then Within 30s an in-app notification arrives with type = "kb_indexed"
And Within 60s an email arrives at verified email
And Both notifications include CTA "Avvia chat con l'agente" deep-linking to /games/{id}/chat?suggested=true

When Andrea clicks deep link from in-app notification
Then Within 1s /games/{id}/chat renders a chat UI (NOT redirect — depends on #2194)
And Game agent is pre-selected
And First message "Come funziona il Mana Pool?" returns first token within 3s
And Response cites at least one chunk from uploaded PDF (page number visible)
```

## Decomposition (4 sub-spec + 1 E2E)

When the P0 blockers below land, open the following sub-issues — each is independently testable:

- **US-INT-1a — Game catalog KB state visibility** (Step 3 UI gap) — P1 · M (3gg) · **NEW**, no existing issue covers it
- **US-INT-1b — PDF upload flow from game detail** (Step 4) — P1 · L (5-7gg) · entry point + drop zone + progress
- **US-INT-1c — Notification → Chat deep link contract** (Step 6→7) — P2 · M (3gg) · **depends on #2194 resolved** (ADR-061 game-detail tab canonical)
- **US-INT-1d — E2E Playwright integration test** — P2 · M (3gg) · depends 1a + 1b + 1c

Total effort: ~14gg + upstream dependency on the P0 cluster (~+5-10gg).

## Required ADRs (Fowler + Newman)

1. **ADR-N** — Ownership di PDF utente: private contributor vs shared community KB?
2. **ADR-N+1** — Knowledge base versioning: re-index strategy quando embedding model o chunking schema cambiano
3. **ADR-N+2** — Notification → Chat deep link contract: URL format, suggested context, agent pre-selection

## Blocker dependencies

- **#2168** — P0 login open redirect (RESOLVED via #2219 + #2240 — `assertSafeRelativeOrFallback` shipped 2026-06-12) → unblocks Step 1
- **#2176** — P0 dashboard/library counter mismatch → blocks Step 4 library count post-add (still open)
- **#2194** — P0 game detail orphan routes (RESOLVED via #2204 + ADR-061 — chat orphan removed, 7 canonical tabs) → unblocks Step 7
- **#2197** — back link routing inconsistency → impacts deep link contract design

Re-check status before opening 1a–1d: 2/4 P0 already resolved; #2176 + #2197 remain.

## Failure modes (Nygard — must be defined before implementation)

1. PDF malformato (OCR fail / extraction parziale) → notification "failed" o spinner perenne?
2. Embedding service down → queue cresce, KB resta "Processing" → SLO breach silente?
3. Storage full (S3 / MinIO) → error message UX-friendly?
4. Notification email bounces → retry? log? fallback?
5. Chat agent rate limit → degrade graceful o blocco?
6. Concurrent uploads stesso PDF da 2 device → dedup? race?

**Observability gap**: ≥5 Prometheus metrics + 1 Grafana dashboard required
(`meepleai_kb_pipeline_duration_seconds{stage}` + 4 more).

## Spec-panel verdict

⚠️ **US-INT-1 NON è READY** — moved out of GitHub umbrella (#2213 closed) into this
spec document so it lives next to the implementation plans instead of a stale
tracking issue. Sub-spec 1a–1d will be opened as separate issues once #2176 and
#2197 are resolved; the 3 ADRs run in parallel.

## Refs

- Closed umbrella: #2213
- Audit: `audits/us-verification-log.md` (10/10 US validate prior, 30 issue tracking)
- Spec-panel critique: 2026-06-11
- Delivery plan: `docs/superpowers/plans/2026-06-11-p0-delivery-plan.md`
- ADR-061 (#2204 follow-up — game-detail tab canonical): unblocks Step 7
