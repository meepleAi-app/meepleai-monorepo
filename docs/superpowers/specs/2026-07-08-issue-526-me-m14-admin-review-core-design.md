# Design — #526 ME-M1.4 Admin Review UI (core iteration)

- **Issue**: [#526](https://github.com/meepleAi-app/meepleai-monorepo/issues/526) `[ME-M1.4] Admin Review UI: per-claim approve/reject + citation viewer`
- **Parent ADR**: [ADR-051 Mechanic Extractor IP Policy](../../for-claude/architecture/adr/adr-051-mechanic-extractor-ip-policy.md)
- **Date**: 2026-07-08
- **Status**: PROPOSED (design approved in brainstorming; awaiting spec review → writing-plans)
- **Branch**: `feature/issue-526-me-m14-admin-review-ui` (parent `main-dev`)

---

## 1. Context

`#526` is the admin per-claim review surface for the Mechanic Extractor pipeline. PR #592 already shipped ~50% of the feature. A structured recon of the current codebase (7-agent understand fan-out, 2026-07-08) surfaced that the repository is in several places **ahead of** the issue body, and that two acceptance criteria carry **cross-issue contradictions** that must not be resolved unilaterally inside this issue.

### Already shipped (PR #592 — verify + close)
- Route consumes `mechanic_analyses` (not the legacy `mechanic_drafts`). **NB the route is the single-file `.../mechanic-extractor/analyses/page.tsx` deep-linked via `?analysisId=` query param — NOT a `[id]` dynamic route** (the issue's AC-Done text is inaccurate on this).
- Per-claim approve/reject + `RejectClaimDialog` (mandatory note, 1–500 chars).
- `MechanicAnalysesListCard` discovery table; `ClaimsSection` grouped-by-section claim list; single "Bulk approve pending" button.
- Backend **`POST /admin/mechanic-analyses/{id}/claims/bulk-reject`** already exists (command + validator + handler + tests) — only the frontend wiring is missing.
- Analysis lifecycle already present: `submit-review` → `approve` (`InReview`→`Published`, **409 if any claim is not Approved**) → `suppress`.
- One E2E: `apps/web/e2e/admin-mechanic-extractor-validation/load-existing-analysis.spec.ts`.

### Ground-truth facts from recon (evidence-backed)
- **Guardrail (T1–T4) outcomes are transient.** `MechanicValidationViolation` (`IMechanicOutputValidator.cs:33` = `record (string Rule, string Message, string? Path)`) is produced only during the pipeline to drive rejection-sampling retries (`MechanicAnalysisPipeline.cs:220,238`) and then discarded. There is **no persisted per-claim validation column, and no `MechanicValidationViolation` domain entity.** `MechanicClaim` fields are: `AnalysisId, Section, Text, DisplayOrder, Status, ReviewedBy, ReviewedAt, RejectionNote, Citations, IsNew` (`MechanicClaim.cs`).
- **Corollary:** any claim reaching the review queue has, by construction, passed its section's enabled guardrails (rejection sampling retries the section until guardrails pass, or aborts the analysis to `PartiallyExtracted`/`Rejected`). So real per-claim T1–T4 data is largely degenerate (all-pass) — persisting it is low value; a **derived** signal captures nearly all of it.
- **Guardrail → rule-family map:** T1 = `QuoteCapGuardrail`, T2 = `RejectionSamplingGuardrail` (long-verbatim), T3a = `CitationPresenceGuardrail`, T3b = `GroundingGuardrail`, T4 = `PageSubstringGuardrail`.
- **Analysis status enum** (`MechanicAnalysisStatus`): `Draft(0), InReview(1), Published(2), Rejected(3), PartiallyExtracted(4)`. There is **no `Approved` and no `NeedsReview`** state — the issue body's AC-4 wording assumes states that do not exist.
- **`mechanic_card` aggregate does not exist yet** (0 files). It is created by #527's explicit Publish, not by #526.
- **PDF stack already exists:** `react-pdf@10.4.1` + `pdfjs-dist@5.7.284` (pinned), worker bundled locally. Reusable viewers live in `apps/web/src/components/pdf/` (`PdfInlineViewer` documented as the shared viewer, `PdfViewerModal` already renders the text layer). **No on-page quote highlighting exists anywhere.** No bounding-box / pixel-coordinate data exists for quotes (blocks "Pattern B").
- **`ApproveMechanicClaimCommand`** = `record (AnalysisId, ClaimId, ReviewerId)` — **no note field**; the domain `MechanicClaim.Approve()` clears `RejectionNote`. Approve-with-note therefore needs a new note sink.

---

## 2. Scope (locked)

### IN — this PR (core)
| AC | Deliverable |
|----|-------------|
| AC-1 (contract only) | Extend `MechanicClaimDto` with `validations[]` (**derived**, not persisted) + render T1–T4 badges from it (green/red/gray). |
| AC-2 | `<PdfQuoteHighlighter>` at `components/pdf/`, Pattern A (text-layer search) + page-level fallback; wired into `ClaimsSection` citation rows. |
| AC-3 | "Bulk action" dropdown: *approve-all-pending* (existing) + *reject-all-quote>20-words* (client predicate → existing bulk-reject endpoint) + count-confirm dialog. |
| AC-5 | `<MechanicAnalysisFooterAttribution>` (ADR-051 canonical string); remove forbidden Variant-C string; grep gate → 0 hits. |
| AC-6 (MVP) | Approve-with-note (optional): adds one nullable `ReviewNote` column + extends approve command. |
| AC-7 (light) | `mechanic_review_bulk_actions_total{action}` counter via `MeepleAiMetrics`; test isolated per the static-Meter pollution pitfall (#2752). |
| AC-8 | a11y: badge `aria-label`s, PDF modal focus-trap, dark-mode highlight contrast. |
| AC-9 | Tests (Vitest + backend handler + E2E extension). |

### OUT — deferred to scoped follow-up issues
- **FU-1 (AC-1 real):** persist per-claim guardrail outcomes + scores (e.g. T3 grounding score) at pipeline time; flip the derived `validations[]` to persisted; unblocks the *reject-all-failing-T2* predicate and T3 score display.
- **FU-2 (AC-4):** analysis-level Finalize + state-machine reconciliation with #527/ADR-051. **Amend** the #526 AC-4/DoD line "*Finalize → becomes mechanic_card*" — card creation belongs to #527's explicit Publish (ADR-051 §7 requires deliberate publication; #527 AD-5). Resolve the `FinalizeMechanicAnalysisCommand` name collision (the existing command is the legacy Variant-C draft flow).

---

## 3. Locked decisions

1. **Scope** = core-completable + 2 follow-ups (above).
2. **`<PdfQuoteHighlighter>` path** = `apps/web/src/components/pdf/PdfQuoteHighlighter.tsx` (matches all existing PDF primitives). Reconcile #530's spec text (which names `components/citations/`) to this path.
3. **AC-1 data** = **derived from the pass-invariant** in `GetMechanicAnalysisClaimsQueryHandler`, not persisted. Documented as derived in code + DTO XML doc. Real persistence = FU-1.
4. **AC-4 (finalize)** = **not in this PR.** #526 does NOT create the `mechanic_card`.
5. **AC-2 algorithm** = Pattern A (text-layer substring search) with page-level-highlight + banner fallback (pre-authorized by #526 F2 and #530). Component API is hybrid-shaped (accepts optional coordinates) so a future Pattern-B backend slots in without an API change. Admin context does not require antiLeak → text layer is rendered.
6. **AC-6** = the single nullable `ReviewNote` column is the **only migration** in core (AC-1 validations are derived, no migration). *Open to trimming to "reject-note only, defer approve-note to FU-1" at the review gate if a zero-migration core is preferred.*

---

## 4. Design detail

### 4.1 Backend

**`MechanicClaimDto` + validations contract** — add:
```
public sealed record MechanicClaimValidationDto(
    string Rule,        // "T1" | "T2" | "T3" | "T4"
    string Outcome,     // "pass" | "fail" | "notRun"
    string? Message);   // populated only on fail (from MechanicValidationViolation.Message)
```
`MechanicClaimDto.Validations : IReadOnlyList<MechanicClaimValidationDto>`. `GetMechanicAnalysisClaimsQueryHandler` derives one entry per badge family (T1, T2, T3 = grounding+citation-present, T4): `pass` for enabled guardrails (any persisted claim passed them), `notRun` for guardrails disabled in `MechanicGuardrailOptions`. Never emits `fail` in the derived path (a failing claim would not be in the queue) — the `fail`/`Message` path lights up only once FU-1 persists real outcomes. #527 snapshots this array into `mechanic_cards.content` (score fields arrive with FU-1).

**Approve-with-note (AC-6)** — add nullable `ReviewNote` (snake_case `review_note`) to `MechanicClaim` + `MechanicClaimEntity` + config + additive migration; add optional `Note` to `ApproveMechanicClaimCommand` + domain `MechanicClaim.Approve(reviewerId, utcNow, note?)`. Reject-note flow is unchanged (already shipped).

**No new endpoints.** `bulk-reject` exists. AC-7 adds a counter increment in the bulk-reject/bulk-approve handlers.

### 4.2 Frontend

**`<PdfQuoteHighlighter pdfUrl page quote onClose coordinates?/>`** (`components/pdf/`) — composes `PdfInlineViewer`, opens at `page`, renders the text layer, normalizes whitespace/soft-hyphenation, substring-matches `quote` against positioned spans, overlays `<mark>` (`rgba(255,235,59,0.4)`). On no-match: page-level highlight + banner *"Quote non individuabile automaticamente; verifica manualmente"*. WCAG AA ≥3:1 + text-only toggle. PDF URL resolved from the analysis `pdfDocumentId` (`MechanicCitationDto` carries only `{pdfPage, quote}`).

**`ClaimsSection` changes:**
- **Badges (AC-1):** 4 badges/claim from `claim.validations ?? []`; green(pass)/red(fail)/gray(notRun|absent); tooltip = `message` on fail; `aria-label` per badge. Gray fallback when field empty → real-data flip is a pure BE change.
- **Citation rows (AC-2):** the plain-text `p.N — "quote"` becomes a button → opens `<PdfQuoteHighlighter>`.
- **Bulk dropdown (AC-3):** replace the single button with a `Select`: *Approva tutti i pending* (existing `bulkApproveMechanicClaims`); *Rifiuta tutti con quote >20 parole* (client predicate on `citations[].quote` word count → compute `claimIds` → existing `bulk-reject`), with a confirm dialog showing the predicted count (`"Rifiuta 7 claim?"`). **No 5-second undo** (count-confirm is the guard; keeps the audit log clean). Add FE: route constant `bulkRejectClaims`, `BulkRejectMechanicClaimsResponseDtoSchema`, `bulkRejectMechanicClaims` client method.
- **Approve-with-note (AC-6):** optional collapsible textarea on approve (reuse the `RejectClaimDialog` pattern).

**`<MechanicAnalysisFooterAttribution>`** (shared, reused by #528) renders the ADR-051 string:
> *"Analisi elaborata dall'AI sul manuale del gioco. Ogni affermazione è riformulata in parole originali e cita la pagina del regolamento. Copyright © degli editori per il testo originale del manuale."*

Remove the forbidden *"L'AI non ha mai letto il testo del PDF originale."* from `review/page.tsx` (and any other hit). **DoD gate:** `grep "L'AI non ha mai letto" apps/web/src` → 0 hits.

### 4.3 Conventions (match existing admin code)
shadcn primitives (`@/components/ui/{primitives,data-display,overlays}/…`), **not** MeepleCard. Keep the file-level `eslint-disable local/no-hardcoded-color-utility` (DS-13d admin scope) with the existing amber/green/rose class maps — **do not** introduce `--admin-*` tokens (that is DS-15/16). React Query keys `['mechanic-analysis', id, 'claims']` etc. Pervasive `data-testid`. EF: `HasColumnName("review_note")` (no auto snake_case).

---

## 5. Testing (AC-9)

- **Vitest (FE):** badge render matrix (green/red/gray × T1–T4 fixtures); `<PdfQuoteHighlighter>` not-found fallback banner; bulk dropdown predicate → count → confirm; footer swap (old string absent, new string present); approve-with-note optional path.
- **Backend:** `GetMechanicAnalysisClaimsQueryHandler` asserts derived `validations[]` (pass for enabled, notRun for disabled); `ApproveMechanicClaimCommand` with/without note; AC-7 counter test isolated (distinctive tag or `[Collection]` disabling parallel, per #2752).
- **E2E:** extend `load-existing-analysis.spec.ts` → bulk-reject-by-predicate flow + citation-open flow.

---

## 6. Follow-up issues to open

- **FU-1 — AC-1 real validation persistence.** Persist per-claim guardrail outcomes + scores at pipeline time; flip derived `validations[]` → persisted; enable *reject-all-failing-T2* predicate + T3 score display.
- **FU-2 — AC-4 finalize + state reconciliation.** Analysis-level Finalize reconciled with #527/ADR-051; amend the "becomes mechanic_card" AC (card = #527 Publish); resolve the `FinalizeMechanicAnalysisCommand` name collision.

Both must be filed **before** closing #526, and #526's AC-4/AC-1-real items moved into them so #526's DoD reflects the actual delivered scope.

---

## 7. Risks / open items

- **Quote text-layer match rate** on OCR'd/photo-batch PDFs is unknown; Pattern A may fall back to page-level highlight frequently. Acceptable (fallback is pre-blessed), but worth a quick spike on a seeded game (e.g. Catan) during implementation.
- **Admin PDF fetch authorization:** confirm an admin can fetch an arbitrary shared-game rulebook PDF via `pdfDocumentId` (existing `api.pdf.getPdfDownloadUrl` or a new admin route). If a new route is needed, it is a small addition.
- **`PartiallyExtracted` analyses** in the review UI: badges render fine (surviving claims passed); bulk actions operate on present claims. No special handling needed for core.
- Branch currently tracks `origin/main-dev`; push with `git push -u origin feature/issue-526-me-m14-admin-review-ui` to retarget upstream.

---

## 8. Key references
- `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/analyses/page.tsx` — review route (`?analysisId=`).
- `apps/web/src/components/admin/mechanic-extractor/claims/ClaimsSection.tsx` — primary FE surface.
- `apps/web/src/components/admin/mechanic-extractor/claims/RejectClaimDialog.tsx` — reuse for approve-note.
- `apps/web/src/lib/api/schemas/mechanic-analyses.schemas.ts` — Zod DTOs + routes.
- `apps/web/src/lib/api/clients/admin/adminContentClient.ts` — admin client methods.
- `apps/web/src/components/pdf/PdfInlineViewer.tsx` — base for `<PdfQuoteHighlighter>`.
- `apps/api/src/Api/Routing/AdminMechanicAnalysesEndpoints.cs` — REST surface (bulk-reject at :258).
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicClaimDto.cs` — DTO to extend.
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicClaim.cs` — add `ReviewNote`.
- `apps/api/.../Application/Services/MechanicExtractor/Guardrails/*` — T1–T4 rule families.
- `docs/for-claude/architecture/adr/adr-051-mechanic-extractor-ip-policy.md` — footer string, T-constraints, status enum addendum.
