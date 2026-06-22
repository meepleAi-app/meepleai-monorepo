# Translate Viewer Gap Report — `/library/[gameId]/play/[campaignId]/translate` (librogame-runthrough-translate-viewer.html ↔ SP6 Phase A PR #790 + Aaron CORE refinement 2026-05-23)

> Conformity check applying the [`PILOT_GAP_REPORT.md`](./PILOT_GAP_REPORT.md) methodology to the **translate viewer** route, with explicit verification of the 4 Aaron CORE refinement states (loading skeleton · reader mode · multi-lang · manual-mode) introduced in [`2026-05-23-mockup-refinement-aaron-core-design.md`](../../docs/superpowers/specs/2026-05-23-mockup-refinement-aaron-core-design.md). Issue: [#1487](https://github.com/meepleAi-app/meepleai-monorepo/issues/1487).
> Date: 2026-05-26 · Branch: `feature/issue-1487-translate-audit` · Scope: read-only.
> Reference format: [`libro-detail-gap-report.md`](./libro-detail-gap-report.md) (most recent PILOT example, PR #1551 0% drift).

## TL;DR

Unlike the libro-detail (0% drift) and players-detail (10% drift) audits, this route shows **substantial unimplemented scope**: SP6 Phase A (camera → segment → translate → view) shipped correctly via PR #790, but the **Aaron CORE refinement spec (2026-05-23) added 8 new states / behaviors that are NOT yet implemented**. The implementation lag is the root cause, not drift in the technical sense — the mockup advanced beyond what the FE ships.

| Status | Count | Components |
|---|---|---|
| ✅ Implemented + correctly located | 4 | `TranslateViewer`, `SegmentPicker`, `BookPicker`, `TranslationPane` |
| 🟢 Implemented as FSM phase (not standalone) | 1 | Basic 6-state Phase enum (`idle`/`uploading`/`segmenting`/`segments_ready`/`translating`/`translated`) |
| ❌ Missing — Aaron CORE refinement states | 4 | Loading 4-step skeleton · Reader-mode toggle · Multi-language detection + override modal · Manual-mode entry |
| ❌ Missing — other mockup sections | 4 | Wake-lock indicator · Low-confidence banner (state F) · Lang-detection badge (header) · Abort CTA during loading |
| ⚠️ Partial / to verify | 1 | AAA contrast on `TranslationPane` (verify `--c-text-high-contrast` CSS var application) |

→ **Drift ratio**: 8 ÷ 13 mockup states = **61.5%** (vs libro 0%, players 10%, pilot 37.5%, this is the **highest** in the audit series).

→ **Effort residuo**: ~15–20h distributed across **4 P1 Aaron CORE issues** + 1 contrast verify + cross-cutting a11y/tests covered within each feature issue.

## Convenzioni

| Simbolo | Significato |
|---|---|
| ✅ | Implementato + esposto via barrel `index.ts` |
| 🟢 | Implementato (FSM phase / internal) — non esposto by-design |
| ❌ | Mancante — da creare |
| ⚠️ | Esiste in altro path / variant / by-design deferral |
| 🔎 | Coverage gap / da verificare |

## 1. Implementation inventory (P72 verified live)

| File | Path | LOC | Purpose |
|---|---|---|---|
| `TranslateViewer.tsx` | `apps/web/src/components/features/gamebook/TranslateViewer.tsx` | 178 | Main orchestrator (Phase FSM + hooks wiring) |
| `TranslationPane.tsx` | same dir | ~60 | Streaming translation render + source `<details>` collapsible |
| `SegmentPicker.tsx` | same dir | ~45 | Paragraph selector grid |
| `BookPicker.tsx` | same dir | ~71 | Multi-book radio-group picker |
| Route page | `apps/web/src/app/(authenticated)/library/[gameId]/play/[campaignId]/translate/page.tsx` + `_content.tsx` | 16 + 42 | Server wrapper + client content (campaign resolution) |
| Test file | `apps/web/src/components/features/gamebook/__tests__/TranslateViewer.test.tsx` | 219 | 7 tests covering book-picker + multi-book FSM |

**Path**: ✅ canonical `components/features/gamebook/` (post-FREEZE, same as `LibroGameDetailView` / `CampaignSetupDrawer` audited in #1486).

**FSM (TranslateViewer.tsx line 28)**:
```ts
type Phase = 'idle' | 'uploading' | 'segmenting' | 'segments_ready' | 'translating' | 'translated';
```

**Hooks**:
- `usePhotoUpload(campaignId)` — TanStack mutation
- `useSegmentPhoto(campaignId)` — TanStack mutation
- `useTranslateSegmentSSE()` — EventSource SSE, returns `{ partialText, isComplete, appliedTerms, error, start, stop }`
- `useGameBooks(gameRef)` — TanStack query, multi-book selection

## 2. Aaron CORE 4-state coverage matrix (CRITICAL deliverable)

| # | State | Spec section | Mockup shows? | Shipped FE? | Verdict |
|---|---|---|---|---|---|
| G | **Loading 4-step skeleton** | §1b lines 103–141 | ✅ lines 474–635 (4 step labels + shimmer + abort FAB + latency targets `< 17s` + reduced-motion fallback) | ❌ Only `phase === 'uploading' \| 'segmenting' \| 'translating'` text labels (TranslateViewer:147–149) | ❌ **MISSING** (effort **L**, ~3-4h) |
| H | **Reader mode toggle** | §1b lines 127–129 | ✅ lines 647–685 (📖 button, 16pt ↔ 24pt, +30% line-height, max-width 65ch) | ❌ zero grep matches for `reader-mode` / `localStorage` / `📖` in any translate file | ❌ **MISSING** (effort **M**, ~2-3h) |
| K/L | **Multi-language detection + override** | §1d lines 196–210 | ✅ lines 36–70 head (5 langs EN/FR/DE/ES/IT, confidence model `>0.8 / 0.5–0.8 / <0.5`, header pill + modal radio + "Conferma e ritraduci") | ❌ zero grep matches for `lang-detect` / `sourceLang` / language picker; no confidence handling | ❌ **MISSING** (effort **L**, ~4-5h, includes BE lang-detect in same OCR call + cached OCR re-translate flow) |
| M | **Manual-mode entry** | §1d lines 211–225 | ✅ lines 49–69 head (`?mode=manual` query param, textarea max 2000 char + counter, lang dropdown pre-filled, book chip, triple-entry discoverability: error CTA + kebab menu + empty-state card) | ❌ zero grep matches for `mode=manual` / `manual-mode` / `source_method` | ❌ **MISSING** (effort **L**, ~4-5h, includes route param handling + dynamic skip-OCR flow + analytics tracking) |

**All 4 Aaron CORE states: MISSING.** Root cause: Phase A shipped before the refinement spec landed; no Phase A.1 implementation plan exists yet.

## 3. Mockup component diff (full)

| Mockup section | Lines | Status | Shipped path |
|---|---|---|---|
| A — Camera viewfinder | head:11 (`camera-ready`) | ✅ | `TranslateViewer` photo `<input>` + "Scatta o scegli foto" button |
| B — Segmenting progress | head:12 (`segmenting`) | ✅ | `phase='uploading' / 'segmenting'` text labels |
| C — Segments list | head:13 (`segments-list`) | ✅ | `SegmentPicker` |
| D — Translating progress | head:14 (`translating`) | ✅ | `TranslationPane` streaming `partialText` |
| E — Fullscreen viewer | head:15 (`fullscreen-viewer`) | ✅ | `TranslationPane` full display + source `<details>` |
| F — Low-confidence banner | head:16 (`low-confidence`) | ❌ | Not implemented (effort **M**, banner + 3 CTA paths) |
| G — Loading 4-step skeleton | head:17 (`loading-4step`) | ❌ Aaron CORE | _see § 2_ |
| H — Reader mode | head:18 (`reader-mode`) | ❌ Aaron CORE | _see § 2_ |
| I — Wake-lock indicator | head:19 (`wake-lock`) | ❌ | Not implemented (effort **M**, badge 🔆 sticky + Web API + 3 states) |
| J — AAA contrast | head:20 (`contrasto-aaa`) | ⚠️ to verify | Verify `TranslationPane` uses `--c-text-high-contrast` var (effort **S**) |
| K — Lang-detection badge | head:21 (`lang-detection-badge`) | ❌ Aaron CORE | _see § 2_ |
| L — Lang-override modal | head:22 (`lang-override-modal`) | ❌ Aaron CORE | _see § 2_ |
| M — Manual-input mode | head:23 (`manual-input-mode`) | ❌ Aaron CORE | _see § 2_ |

**5 / 13 sections matched**; **8 / 13 missing**; **0 / 13 deferred-by-design** (all gaps are real).

## 4. Hooks + state management gaps

| Feature | Spec mandate | Shipped status |
|---|---|---|
| Source language detection | LLM in same OCR call, populate `lang_detection_confidence` in SSE response | ❌ not implemented; SSE returns no lang metadata |
| Target language | IT fixed v1, future profile setting | 🟢 IT hardcoded (acceptable for v1) |
| OCR cache + re-translate-step-3-only on lang override | Reuse cached OCR text when user changes source lang | ❌ no caching infra |
| `source_method: "ocr" \| "manual"` analytics tag | Track in state for analytics | ❌ no tracking field |
| Loading 4-step FSM | Step-named transitions (uploading/OCR/translating/glossary) | ❌ only 3 generic phase labels |
| Abort flow | Stop SSE + revert to previous phase | ❌ no abort affordance |
| Reader mode persistence | `localStorage['reader-mode-enabled']` boolean | ❌ no localStorage integration |
| Manual-mode route param | `?mode=manual` query param triggers textarea UI | ❌ no `useSearchParams` for `mode` |

## 5. A11y / interaction sub-check

| Aspect | Spec intent | Shipped | Status |
|---|---|---|---|
| Skeleton `aria-busy` + `aria-live="polite"` | Async loading announce | ❌ no skeleton exists | ❌ |
| Reader-mode toggle `aria-pressed` or `role="switch"` | Toggle semantics | ❌ toggle missing | ❌ |
| Lang picker `<label>` + `aria-label` | Form labeling | ❌ no picker exists | ❌ |
| Manual textarea `<label>` + char-counter `aria-live` | Form + live region | ❌ no textarea exists | ❌ |
| `SegmentPicker` keyboard nav | Tab order in list | ✅ native `<button>` list | ✅ |
| `BookPicker` `role="radiogroup"` + `aria-checked` | Radio semantics | ✅ correctly implemented | ✅ |
| `TranslationPane` error `role="alert"` | Error announce | ✅ implemented | ✅ |
| Abort CTA during loading | Stop affordance keyboard-accessible | ❌ no abort exists | ❌ |
| AAA contrast `--c-text-high-contrast` on body text | High contrast for translated paragraph | ⚠️ verify on `TranslationPane` | 🔎 |

## 6. Out of scope (NOT to spawn issues for)

- **`GamebookErrorBanner`** cross-cutting primitive — explicitly out of scope per issue body, pending FREEZE lift (separate matrix entry).
- **Encounter cheatsheet** (PR #1525) — different route under same campaign tree, audited separately.
- **Libro-detail** (PR #1551, issue #1486) — already audited 2026-05-26.

## 7. Issue follow-up — proposed

The 4 Aaron CORE gaps + 1 contrast-verify item are the actionable backlog. The "low-confidence banner" (state F), "wake-lock indicator" (state I), and the cross-cutting a11y/test work can be bundled inside each feature issue's AC.

| # | Title | Body summary | Effort | Priority |
|---|---|---|---|---|
| F1 | `feat(translate-viewer): Loading 4-step skeleton + abort CTA (#1487 follow-up Aaron CORE G)` | Implement multi-step FSM (uploading→OCR→translating→glossary-check). Skeleton component (3-line + shimmer-sweep, `prefers-reduced-motion` fallback). Abort CTA visible from step 2+ (mobile FAB / desktop top-right). Latency targets in comments (<17s soft, 20s hard timeout). Integrate into Phase FSM. Tests + jest-axe (`aria-busy` + `aria-live`). | **L** (~3-4h) | P1 |
| F2 | `feat(translate-viewer): Reader-mode toggle (#1487 follow-up Aaron CORE H)` | Button 📖 in header. `localStorage['reader-mode-enabled']` boolean. Font 16pt ↔ 24pt + line-height +30% + padding via CSS vars scoped to viewer. Apply to skeleton AND `TranslationPane`. Tests + jest-axe (`aria-pressed`). | **M** (~2-3h) | P1 |
| F3 | `feat(translate-viewer): Multi-language source detection + override (#1487 follow-up Aaron CORE K/L)` | Wire `lang_detection_confidence` from SSE response (BE work: detect EN/FR/DE/ES/IT in same OCR LLM call, populate field). Header badge post-OCR (confidence model: >0.8 informational / 0.5–0.8 tap-to-confirm blocking / <0.5 force-override). Modal radio group (5 langs) + "Conferma e ritraduci" CTA → re-trigger SSE step 3 only, OCR cached. Track `source_lang`. Tests + jest-axe. | **L** (~4-5h, **BE+FE coordinated**) | P1 |
| F4 | `feat(translate-viewer): Manual-mode textarea entry (#1487 follow-up Aaron CORE M)` | `?mode=manual` query param via `useSearchParams`. Textarea (max 2000 char + live counter `aria-live`). Lang dropdown pre-filled from `last_used_lang`. Book chip read-only (current campaign). Triple-entry discoverability: error-state CTA + kebab menu + empty-state card. On submit skip OCR phase (step 2) → directly to step 3 (translating). Track `source_method: "manual"`. Tests + jest-axe. | **L** (~4-5h) | P1 |
| F5 | `chore(translate-viewer): verify AAA contrast on TranslationPane body text (#1487 follow-up J)` | Verify `TranslationPane` body text uses `--c-text-high-contrast` CSS var (mockup state J ratio ≥7:1). If not applied, swap from default text color. Add jest-axe color-contrast assertion. | **S** (~1h) | P2 |

**Total effort backlog**: ~14-18h. F3 has a BE coordination dependency (lang-detection in OCR LLM call) — may need a BE sub-issue if not bundled.

## 8. Conformity verdict

⚠️ **PHASE A SHIPPED — AARON CORE REFINEMENT NOT YET IMPLEMENTED.**

The shipped TranslateViewer correctly implements the original SP6 Phase A flow (camera → segment → translate → view) with solid hooks, FSM, and tests for the multi-book scenario. However, the post-Phase-A refinement spec ([`2026-05-23-mockup-refinement-aaron-core-design.md`](../../docs/superpowers/specs/2026-05-23-mockup-refinement-aaron-core-design.md)) introduced **8 new mockup states** of which **4 are Aaron CORE priorities** (loading skeleton, reader-mode, multi-lang, manual-mode), and the spec→impl conversion was never opened as Phase A.1 issues.

This is **not a regression** or visual drift — the shipped code is internally correct; it is simply at version N while the design source-of-truth is at version N+1. Resolution path: open the 4 follow-up issues (F1–F4) as Phase A.1 work; once shipped, drift returns to 0%.

The audit verdict, expressed in PILOT terms: **❌ NOT production-ready against current mockup.** Production-ready against the original Phase A scope only.

**Comparison with the other 2 C-series audits**:

| Audit | Drift | Verdict |
|---|---|---|
| `/players/[id]` (#1485, PR #1544) | **10%** | ✅ production-ready (2 nice-to-have cards missing) |
| `/library/[gameId]` libro variant (#1486, PR #1551) | **0%** | ✅ production-ready (best alignment) |
| `/library/[gameId]/play/[campaignId]/translate` (this audit, #1487) | **61.5%** | ⚠️ Phase A done, Aaron CORE refinement pending |

---

**Generated by Claude Code (Opus 4.7) in read-only mode.** Issue #1487. PR with matrix `audit_pr` update + audit-report-final entry + 5 follow-up issues to be created post-merge per user direction.
