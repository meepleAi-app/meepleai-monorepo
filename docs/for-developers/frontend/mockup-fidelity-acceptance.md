# Mockup Fidelity Acceptance — `mockup.fidelity.{yml,json}` Schema

**Origine**: DS-17-4 (#2072), parte di [Umbrella #2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063) DS-17 Mockup-to-App Fidelity.
**Schema authority**: `apps/web/scripts/mockup-annotations/validate-fidelity.mjs` (`FidelitySchema` zod definition).
**Spec doc**: [`docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md`](../../superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md) (Sezione 3 — Wiegers CRIT-5).

---

## TL;DR

Ogni mockup migration deve essere accompagnata da un file `<mockup-name>.fidelity.{yml,json}` che definisce **misurabile** cosa significa "pixel-perfect" per quel mockup. Il file viene validato in CI (Phase 1: opt-in; Phase 4: blocking gate).

- **Template**: [`templates/mockup.fidelity.yml.template`](./templates/mockup.fidelity.yml.template) (con inline docs)
- **Esempio**: [`templates/examples/sp4-dashboard.fidelity.json`](./templates/examples/sp4-dashboard.fidelity.json) (JSON mode, no extra deps)
- **Validator CLI**: `node apps/web/scripts/mockup-annotations/validate-fidelity.mjs <file>` (oppure `--all` per scan repo)

---

## Quando usarlo

- ✅ **Phase 2+** (DS-17-6): ogni story `*.stories.tsx` deve avere `*.fidelity.yml` co-located
- ✅ **Phase 1** (oggi): pilot per `sp4-dashboard` come standalone esempio
- ✅ **Signoff gate SHIPPED** (#2997, [ADR-077](../../for-claude/architecture/adr/adr-077-designer-signoff-ci-gate.md)): `designer_approved_by` **required non-empty** per ogni `design_intent: "current"`, enforced in CI via `pnpm lint:fidelity` (step `frontend-lint`). Il token `self-waiver P250` è un'approvazione accettata (contesto solo-maintainer).
- ⏭️ **Phase 4**: visual gate scoped legge `visual_diff_max_px` + `color_delta_e_max` come threshold

---

## Schema reference

### `mockup.source` (string, required)

Path al file mockup canonico relativo al repo root.
- ✅ `admin-mockups/design_files/sp4-dashboard.html`
- ❌ `./sp4-dashboard.html` (relative al fidelity file — non supportato)

Validator verifica che il file esista. Errore se path inesistente.

### `mockup.states` (array<StateName>, required, min 1)

Quali stati questo mockup copre. Deve essere **set-equal** a `acceptance.states_covered`.

Valid values:
- `default` — happy path, populated data
- `empty` — zero items / first-time user state
- `loading` — initial fetch, skeleton screen
- `error` — fetch failure, validation error
- `sse` — Server-Sent Events streaming (chat, live session)
- `offline` — PWA offline state (libro-game, cached views)

### `acceptance.visual_diff_max_px` (int ≥0, default 5)

Pixel delta massimo tollerato per block-level diff. Lower = stricter.
- 5 = ragionevole per font hinting + sub-pixel rendering
- 1-2 = strict, rischio falsi positivi su Windows/macOS font diff
- 10+ = lax, perde drift visibile

### `acceptance.color_delta_e_max` (float ≥0, default 3)

Max Color Delta-E (CIEDE2000). <3 = imperceptible to most humans.

### `acceptance.tokens_used` (`canonical_only` | `mixed_legacy_allowed`, default `canonical_only`)

- `canonical_only`: post-DS-15 names obbligatori (`var(--bg)`, `var(--bg-card)`, ecc.)
- `mixed_legacy_allowed`: **TEMPORARY waiver** — usare solo per mockup pre-Phase 3 cleanup. Documentare razionale in commit message.

### `acceptance.legacy_token_names_forbidden` (boolean, default true)

Se `true`, lint blocca `--bg-base`, `--gaming-*`, `--nh-*`, `--e-*`. Enforcement via DS-17-2 (`pnpm lint:tokens`).

### `acceptance.states_covered` (array<StateName>, required, min 1)

Stati implementati nella story / mockup. **Set-equal a `mockup.states`** — validator enforce.

### `acceptance.a11y_axe` (`AA` | `AAA`, default `AA`)

Livello WCAG da rispettare. Default AA per progetto (CLAUDE.md § "A11y restore COMPLETE 2026-05-18").

### `acceptance.a11y_violations_max` (int ≥0, default 0)

Max violazioni axe tollerate. 0 = strict (default).

### `acceptance.responsive_breakpoints` (array<int>, default [375, 768, 1024, 1440])

Breakpoint px da testare. Default copre mobile/tablet/desktop/wide.
- Aggiungi `320` se mobile-first
- Aggiungi `2560` se 4K-relevant

### `acceptance.designer_approved_by` (string, default "")

Approvazione del designer. **Enforced (#2997, ADR-077)**: per `design_intent: "current"` deve essere **non-vuoto** o il gate `pnpm lint:fidelity` fallisce (signoff strict, mai baselined). Valori accettati:
- Nome/handle reale del designer (es. `Jane Designer`, `@design-lead`).
- Token solo-maintainer `self-waiver P250`, es. `"you@meepleAi (self-waiver P250, single-person team)"` — documenta l'eccezione developer-è-designer.

Intenti `forward-refactor` / `forward-refactor-obsolete` / `deferred` **non** sono signoff-gated (advisory: superfici speculative o non-ancora-costruite).

### `acceptance.designer_approved_on` (string ISO date, default "")

Data approvazione formato `YYYY-MM-DD`. Validator regex check.

### `acceptance.story_path` (string, default "")

Path Storybook story relativo a repo root. Phase 2+ campo. Validator verifica esistenza se non-empty.

### `acceptance.fixtures_path` (string, default "")

Path fixtures file (TypeScript const o JSON). Phase 2+ campo. Validator verifica esistenza se non-empty.

---

## Validator usage

### Single file
```bash
node apps/web/scripts/mockup-annotations/validate-fidelity.mjs docs/for-developers/frontend/templates/examples/sp4-dashboard.fidelity.json
# PASS  docs/for-developers/frontend/templates/examples/sp4-dashboard.fidelity.json
```

### Scan all fidelity files in repo (CI gate)
```bash
pnpm lint:fidelity   # = validate-fidelity.mjs --all --max-baseline 3
```
`--all` esclude `**/templates/**` (i file di esempio in `templates/examples/` sono illustrazioni, non superfici gated). `--max-baseline N` tollera fino a N fallimenti **strutturali** pre-esistenti (schema / `mockup.source` cross-ref); NON rilassa il signoff (sempre strict). Baseline attuale = 3 (fidelity orfani con `mockup.source` cancellato: `sp4-play-records-data` / `scaffold` / `pr-form-core` — da riconciliare per abbassare la baseline).

### Print schema
```bash
node apps/web/scripts/mockup-annotations/validate-fidelity.mjs --schema
```

### Exit codes

| Code | Meaning |
|------|---------|
| 0 | Signoff OK **e** fallimenti strutturali ≤ `--max-baseline` |
| 1 | Signoff mancante su una superficie `current`, **oppure** fallimenti strutturali oltre baseline |
| 2 | Invocation error (missing arg, file not found) |

---

## YAML vs JSON mode

| Mode | Pros | Cons | Status Phase 1 |
|------|------|------|----------------|
| **JSON** (`.json`) | No extra deps, works out-of-box | Verbose, no inline comments | ✅ **Active** |
| **YAML** (`.yml` / `.yaml`) | Concise, inline comments | Requires `yaml` devDep + Vite plugin config | ⏸️ Deferred to Phase 2 (DS-17-5) |

**Phase 1 = JSON-only**. Il template `.yml` esiste come reference (è human-readable + ha inline docs), ma il validator runtime rifiuta `.yml` con errore esplicito:

```
YAML parsing not enabled in Phase 1 (no "yaml" devDep). Convert <file> to .json,
or wait for Phase 2 (DS-17-5) which adds yaml devDep. Template .yml is reference-only.
```

**Rationale**: Vite static-analyzer blocca `await import('yaml')` anche in try/catch (vite:import-analysis fails build). Per supportare YAML serve `yaml` devDep installato + lockfile update. Decisione deferred a Phase 2 per ridurre scope DS-17-4 (no lockfile diff in PR Wave 1).

**Migration path Phase 1 → Phase 2**:
1. Phase 2 DS-17-5 setup Storybook
2. Add `yaml` to `apps/web` devDeps + `pnpm install` (lockfile update)
3. Update `validate-fidelity.mjs` parseFile() per usare `import yaml from 'yaml'`
4. Convertire `*.fidelity.json` esistenti a `*.fidelity.yml` (opzionale, JSON resta supportato)

---

## Anti-patterns

- ❌ **Inventare campi custom** non in schema → validator FAIL. Discutere in spec doc prima di estendere.
- ❌ **`tokens_used: mixed_legacy_allowed` permanente** → waiver dovrebbe essere temporary con TODO in commit
- ❌ **`states_covered` ≠ `mockup.states`** → validator FAIL (set inequality)
- ❌ **Path mockup.source relativo al fidelity file** → validator richiede path relativo al repo root
- ❌ **`design_intent: "current"` con `designer_approved_by` vuoto** → gate `pnpm lint:fidelity` FAIL (#2997, ADR-077). Compila il signoff o usa il token `self-waiver P250`.

---

## Roadmap (post DS-17-4)

| Phase | Item | Dipendenza |
|-------|------|------------|
| Phase 2 (DS-17-5) | Storybook setup → fidelity files co-located con stories | DS-17-4 done |
| Phase 2 (DS-17-7) | `fixtures.json` pattern definito → `fixtures_path` populated | DS-17-5 done |
| Phase 3 (DS-17-9..13) | Migration sweep mockup → ogni story ha fidelity file | DS-17-7 done |
| Phase 4 (DS-17-14) | Visual gate scoped legge `visual_diff_max_px` | DS-17-13 done |
| Phase 4 (DS-17-15) | Weekly drift report usa fidelity contract | DS-17-13 done |

---

## References

- Issue: [#2072](https://github.com/meepleAi-app/meepleai-monorepo/issues/2072) — DS-17-4 Crea acceptance criteria template
- Umbrella: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063) — DS-17 Mockup-to-App Fidelity
- Spec doc: `docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md` (Sezione 3 — Wiegers CRIT-5)
- Plan doc: `docs/superpowers/plans/2026-06-09-ds-17-phase-1-implementation-plan.md` (Sezione 4.2 — DS-17-4 tasks)
- Validator source: `apps/web/scripts/mockup-annotations/validate-fidelity.mjs`
- Template: `docs/for-developers/frontend/templates/mockup.fidelity.yml.template`
- Esempio: `docs/for-developers/frontend/templates/examples/sp4-dashboard.fidelity.json`
