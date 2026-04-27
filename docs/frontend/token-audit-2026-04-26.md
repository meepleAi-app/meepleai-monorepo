# V2 Design Token Audit — 2026-04-26

**Issue**: meepleai-monorepo#572 (V2 Phase 0 — entityHsl helper)
**Source of truth**: `admin-mockups/design_files/tokens.css` (Claude Design)
**Target**: `apps/web/src/styles/design-tokens.css`

## Background

The `entityHsl(entity, alpha?)` helper introduced in #572 emits
`hsl(var(--c-<entity>))` references. These CSS custom properties must exist
in the web app's runtime stylesheet, otherwise the helper produces invalid
color values.

The mockup tokens use the short `--c-{entity}` naming convention; the web
app currently uses the longer `--color-entity-{key}` convention with one
key mismatch (`document` instead of `kb`) and one missing entity (`toolkit`).

## Discrepancy Matrix

### Entity Colors (light theme)

| Mockup token | Web app current | HSL match | Action |
|---|---|---|---|
| `--c-game: 25 95% 45%` | `--color-entity-game: 25 95% 45%` | ✅ | Add alias |
| `--c-player: 262 83% 58%` | `--color-entity-player: 262 83% 58%` | ✅ | Add alias |
| `--c-session: 240 60% 55%` | `--color-entity-session: 240 60% 55%` | ✅ | Add alias |
| `--c-agent: 38 92% 50%` | `--color-entity-agent: 38 92% 50%` | ✅ | Add alias |
| `--c-kb: 174 60% 40%` (Teal) | `--color-entity-document: 210 40% 55%` (Slate) | ❌ | **Add new** (mockup wins per Phase 0 mandate) |
| `--c-chat: 220 80% 55%` | `--color-entity-chat: 220 80% 55%` | ✅ | Add alias |
| `--c-event: 350 89% 60%` | `--color-entity-event: 350 89% 60%` | ✅ | Add alias |
| `--c-toolkit: 142 70% 45%` | (missing) | ❌ | **Add new** (Wave 1 SP4) |
| `--c-tool: 195 80% 50%` | `--color-entity-tool: 195 80% 50%` | ✅ | Add alias |

### Entity Colors (dark theme)

The mockup's `[data-theme="dark"]` block defines lighter entity HSLs for
contrast on dark backgrounds. The web app uses `.dark` class for dark
mode (line 523 of `design-tokens.css`). We mirror the dark overrides
under `.dark`.

| Token | Light value | Dark value (lighter for contrast) |
|---|---|---|
| `--c-game` | `25 95% 45%` | `28 95% 58%` |
| `--c-player` | `262 83% 58%` | `262 75% 70%` |
| `--c-session` | `240 60% 55%` | `235 70% 70%` |
| `--c-agent` | `38 92% 50%` | `38 92% 62%` |
| `--c-kb` | `174 60% 40%` | `174 60% 55%` |
| `--c-chat` | `220 80% 55%` | `218 80% 68%` |
| `--c-event` | `350 89% 60%` | `350 85% 70%` |
| `--c-toolkit` | `142 70% 45%` | `142 60% 58%` |
| `--c-tool` | `195 80% 50%` | `195 75% 62%` |

### Semantic Colors

| Mockup | Web app current | Action |
|---|---|---|
| `--c-success: 142 70% 45%` | `--color-success: 142 76% 36%` | **Add** `--c-success` (mockup value) — keep `--color-success` for legacy callers |
| `--c-warning: 38 92% 50%` | `--color-warning: 36 100% 50%` | **Add** `--c-warning` (mockup value) |
| `--c-danger: 350 89% 60%` | `--color-error: 0 84.2% 60.2%` | **Add** `--c-danger` (mockup value, different name) |
| `--c-info: 220 80% 55%` | `--color-info: 221 83% 53%` | **Add** `--c-info` (mockup value) |

## Decisions

1. **Add new `--c-{entity}` and `--c-{semantic}` tokens to web app**, sourced
   from mockup. Don't rename existing `--color-entity-*` / `--color-success`
   etc. — those have call-sites in V1 components. Phase 1+ migrations
   will progressively switch call-sites to the new tokens.

2. **`--c-kb` uses mockup Teal (`174 60% 40%`)**, not web app's existing
   Slate `--color-entity-document`. The mockup is the design system source
   of truth per Phase 0 (#571). No existing component uses `--c-kb` —
   no breakage. Future v2 components consuming `entityHsl('kb')` get Teal.

3. **`--c-toolkit` is a new entity** (Wave 1 SP4). Not previously in web app.
   Value `142 70% 45%` (Green).

4. **Dark mode overrides** placed under existing `.dark { }` block (line 523),
   matching the established web app pattern.

## Patch Summary

**File**: `apps/web/src/styles/design-tokens.css`

- `:root { ... }`: add 9 entity + 4 semantic `--c-*` tokens
- `.dark { ... }`: add 9 entity dark-mode overrides

## Verification

After patch:

- `entityHsl('game')` → `hsl(var(--c-game))` → resolves to `hsl(25 95% 45%)` (light) / `hsl(28 95% 58%)` (dark)
- `entityHsl('toolkit', 0.1)` → `hsl(var(--c-toolkit) / 0.1)` → resolves to `hsl(142 70% 45% / 0.1)`
- All 9 entities + 4 semantic tokens resolve in both themes

No call-site changes needed in this PR; the helper is unused outside of
its own unit tests until Phase 1 migrations and the codemod (Task 8).
