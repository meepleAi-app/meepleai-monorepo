# DS-17 Phase 4 Prelude — IntlProvider Hardening Design

**Date**: 2026-06-10
**Umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063) — DS-17 Mockup-to-App Fidelity
**Origine**: brainstorming sess.46l 2026-06-10 post DS-17 Phase 2.5 merge `da6aff26e` (#2113)
**Status**: ⏳ awaiting user review of this spec → writing-plans next

---

## 1. Context

Phase 2.5 hardening ha shipped retroactive pilot rewrite a argTypes matrix (Library 9 Desktop frames + GameDetail 3 Desktop frames = 12 stories) ma **baseline PNG capture è DEFERRED** per IntlProvider runtime context blocker:

- `pnpm build-storybook` succeeds
- Bundle includes react-intl module + useIntl
- `.storybook/preview.tsx` AllProviders wraps `<ReactIntlProvider messages={FLAT_IT_MESSAGES} locale="it" defaultLocale="it">` con flattened it.json messages
- Decorator order: `withThemeByClassName` → `AllProviders` → Story
- Runtime: pilot stories rendono "[React Intl] Could not find required `intl` object. <IntlProvider> needs to exist in the component ancestry." → `useIntl()` returns undefined context

**Suspected root cause**: dual react-intl module instances (preview bundle vs `iframe.bundle.js` Webpack chunk splitting) → 2 distinct React Context instances → `useIntl()` reads from never-populated context.

**Impact**: senza fix, snapshot gate `continue-on-error: true` non può flippare a blocking. Phase 3 sub-issue contributors NON possono regression-test stories. Designer review limitato a manual Storybook UI inspection.

Phase 4 hardening (umbrella body §62-66) include DS-17-14 Visual gate scoped, DS-17-15 Weekly drift report, DS-17-16 admin-mockups cleanup. Tutti dipendono da working baseline. Quindi: **prelude** sub-issue per IntlProvider fix prima di DS-17-14.

---

## 2. DEC user-locked (brainstorming sess.46l)

| ID | Decisione | Scelta | Rationale |
|---|---|---|---|
| **DEC-Phase4-Prelude-1** | Phase 4 IntlProvider prelude vs Phase 3 inizio | **Phase 4 prelude first** | Unblocks snapshot gate per tutti Phase 3 sub-issue successivi. Phase 3 senza working baseline = stories produced ma non regression-test'd |
| **DEC-Phase4-Prelude-2** | Fix approach | **Adaptive: simple fixes first, escalate to investigation** | Min effort if simple fix works, escalate only quando needed. Sequenza Step A → B → C → D documentata |
| **DEC-Phase4-Prelude-3** | Time budget | **Hard 2gg total**. Solo user può extending. Plan executor STOP + raise se budget exceeded | Investigation budget non bounded = rischio. 2gg sufficienti per simple fixes + 1 escalation. Past quello = defer Phase 3 inizio comunque |

---

## 3. Scope — single sub-issue (~0.5-2gg adaptive)

### 3.1 Diagnostic phase (~30min)

1. **Control test**: open existing AuthModal/LoginForm stories in `pnpm storybook` — verify if they render OK (they use `useTranslation`). If they also fail con error wall → issue is iframe-wide, NOT new-stories specific.
2. **Console.log AllProviders decorator**: add `console.log('AllProviders rendered with messages', Object.keys(FLAT_IT_MESSAGES).length)` to confirm decorator runs at render time. Inspect browser console.
3. **React DevTools inspection**: install React DevTools extension. Inspect component tree of failing pilot story → find IntlProvider absence in ancestry tree.

### 3.2 Fix phase — sequential attempts (escalate only on fail)

**Step A** (~30min): swap decorator order in `.storybook/preview.tsx`:

```tsx
decorators: [
  Story => (
    <AllProviders>
      <Story />
    </AllProviders>
  ),
  withThemeByClassName({ ... }),
],
```

Hypothesis: `withThemeByClassName` decorator may intercept children or not pass through to next decorator.

**Step B** (~1h, if A fails): try alternatives:
- (i) Replace direct `ReactIntlProvider` import con `@/components/providers/IntlProvider` (production wrapper).
- (ii) Try dynamic import `await import('react-intl')` to force single chunk loading.

**Step C** (~3-6h, if B fails): root-cause investigation.
- Add `webpack-bundle-analyzer` to `.storybook/main.ts` debug config
- Generate webpack stats: `pnpm build-storybook --debug-webpack`
- Inspect chunk boundaries for `react-intl` occurrences — confirm dual instances
- Apply `resolve.alias` config in `.storybook/main.ts`:
  ```ts
  webpackFinal: async (config) => {
    config.resolve.alias = {
      ...config.resolve.alias,
      'react-intl': path.resolve(__dirname, '../node_modules/react-intl'),
    };
    return config;
  }
  ```

**Step D** (fallback ~2-3h, if C fails): custom `useTranslation` mock alias in Storybook builder webpack config:

```ts
// .storybook/main.ts
webpackFinal: async (config) => {
  config.resolve.alias = {
    ...config.resolve.alias,
    '@/hooks/useTranslation': path.resolve(__dirname, '../src/test-utils/__mocks__/useTranslation-storybook.ts'),
  };
  return config;
}
```

Mock provides stub `t()` function senza richiedere IntlProvider context. Trade-off: stories non testano i18n behavior reale (translation keys → fixed strings).

### 3.3 Verification phase

| Verification | Command | Expected |
|---|---|---|
| Pilot stories render | `pnpm storybook` → open Library Frame09, GameDetail Frame07 | No error wall |
| Bundle includes IntlProvider | `grep -c "ReactIntlProvider\|IntlProvider" storybook-static/main.*.js` | > 0 |
| Existing stories unaffected | Open AuthModal, LoginForm random story | Renders OK (regression check) |
| Baseline capture | `pnpm test:storybook:snapshots:update` | 12 PNGs in `apps/web/e2e/storybook/__snapshots__/` |
| Snapshot gate green | `pnpm test:storybook:snapshots` | 12/12 PASS |
| Typecheck | `pnpm typecheck` | clean |
| Build | `pnpm build-storybook` | succeeds |

**Smoke test gate works**:
- Modify Library fixture (change a game title) → snapshot diff > 5% → tests fail locally
- Revert → 12/12 PASS

---

## 4. Time budget + escalation gate

**Hard time budget**: 2gg total (16h work).

| Step | Duration | Decision gate to escalate |
|---|---|---|
| Diagnostic | 30min | Always proceed to Step A |
| Step A swap order | 30min | If pilot renders → done. If error wall persists → escalate B |
| Step B alternative provider | 1h | If renders → done. If error wall persists → escalate C |
| Step C webpack investigation | 3-6h | If chunk dual instances found + alias fix works → done. If 1.5gg total reached or no clear root cause → escalate D |
| Step D custom mock alias fallback | 2-3h | Last resort, trade-off documented |

**If 2gg budget exceeded without fix**:
- STOP investigation
- Document hypothesis + next steps in tracking issue
- Phase 3 inizio anyway (DS-17-9) accepting baseline still deferred
- Snapshot gate remains `continue-on-error: true`

**Decision authority**: solo l'utente può autorizzare extending budget past 2gg. Plan executor STOP + raise.

---

## 5. Docs update

**`docs/for-developers/frontend/page-mock-story-pattern.md`**:
- Replace `## ⚠️ Known limitation Phase 2.5 — IntlProvider runtime context` section con `## Fix log — IntlProvider hardening (Phase 4 prelude)`:
  - Diagnostic findings (control test, console.log results, devtools inspection)
  - Step path taken (A/B/C/D)
  - Root cause identified (if found) OR fallback rationale
  - Fix applied (decorator order swap / alias / mock — whichever worked)

**`CLAUDE.md`** (existing DS-17 paragraph):
- Replace note "Baseline PNG capture deferred a Phase 4" con "Baseline 12 PNGs captured, CI gate `continue-on-error: true` (`--blocking` post 14gg stable trajectory)"

**`.github/workflows/ci.yml`** (Storybook snapshot step):
- IF stable: flip `continue-on-error: false` (blocking)
- ELSE: keep `continue-on-error: true` + comment "Phase 4 stable trajectory pending"

**Umbrella body update**:
- Phase 2.5 note: "Baseline PNG capture **CAPTURED** post Phase 4 prelude #TBD merge"
- Phase 4 sub-issue list: add DS-17-13.5 "IntlProvider hardening" come prerequisite a DS-17-14

---

## 6. Risk matrix

| Rischio | P | I | Score | Mitigation |
|---|---|---|---|---|
| R1 · Investigation budget exceeded (Step C unbounded) | 3 | 4 | **12** | Hard 2gg gate + Step D fallback custom mock alias |
| R2 · Step A swap order breaks existing 133 stories | 2 | 4 | **8** | Verification phase regression check (open random existing story) |
| R3 · Custom mock alias diverges from production i18n behavior | 3 | 2 | **6** | Documented trade-off; integration tests + e2e cover i18n end-to-end |
| R4 · Webpack-bundle-analyzer slows build significantly | 2 | 2 | **4** | Add only as debug config, removed pre-commit |
| R5 · Baselines captured post-fix have rendering glitches (font drift, locale) | 2 | 3 | **6** | Verification phase smoke test + visual inspection 12 PNGs prima di commit |

---

## 7. Anti-patterns (NON fare)

- ❌ **Skip diagnostic phase**: control test informs hypothesis. Senza control test, attempts sono guess-work.
- ❌ **Extend budget past 2gg senza user approval**: plan executor STOP + raise. No silent extension.
- ❌ **Custom mock alias come Step A**: only last resort. Real fix preferred so stories test real i18n.
- ❌ **Capture baselines senza visual inspection**: glitchy baselines lock pixel bugs.
- ❌ **Flip CI step a blocking immediately**: needs 14gg stable trajectory observation prima.
- ❌ **Decorator order swap regressioni**: regression check on AuthModal/LoginForm stories mandatory.

---

## 8. Effort breakdown

| Phase | Effort | Decision gate |
|---|---|---|
| Diagnostic | 30min | Proceed Step A always |
| Step A | 30min | Done OR escalate |
| Step B | 1h | Done OR escalate |
| Step C | 3-6h | Done OR escalate D |
| Step D fallback | 2-3h | Done (last resort) |
| Verification + smoke test | 1h | Done |
| Docs update | 1h | Done |
| PR + admin-merge + cleanup | 30min | Done |
| **Best case (Step A worked)** | **~3h** | — |
| **Worst case (Step D fallback)** | **~13h (~1.5gg)** | — |
| **Budget hard cap** | **2gg** | User authorization to extend |

---

## 9. References

- Umbrella: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063)
- DS-17 Phase 2.5 sub-issue: [#2113](https://github.com/meepleAi-app/meepleai-monorepo/issues/2113) — MERGED PR #2117 `da6aff26e`
- Dashboard mockup obsolete tracking: [#2114](https://github.com/meepleAi-app/meepleai-monorepo/issues/2114)
- Spec Phase 2.5: `docs/superpowers/specs/2026-06-10-ds-17-phase-2.5-and-3-redesign.md`
- Plan Phase 2.5: `docs/superpowers/plans/2026-06-10-ds-17-phase-2.5-and-3-redesign-plan.md`
- Phase 2 v2 spec (deprecated): `docs/superpowers/specs/2026-06-09-ds-17-phase-2-design.md`

---

🤖 Generated with [Claude Code](https://claude.com/claude-code) — brainstorming sess.46l 2026-06-10
