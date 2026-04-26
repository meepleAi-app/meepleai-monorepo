# Visual Regression — Mockup Baseline

> **Issue #571** — V2 Phase 0 — *Mockup-as-baseline* visual regression contract

## Sommario

Il sistema di visual regression usa **i mockup HTML standalone di Claude Design come fonte di verità** per il design contract. Ogni PR che modifica un mockup (o la pipeline di rendering) deve aggiornare la baseline; ogni regressione visiva diventa un check failure su PR.

| Aspetto | Valore |
|---|---|
| **Tool** | Playwright `toHaveScreenshot()` nativo (zero-cost, no Chromatic/Argos/Percy) |
| **Mockup source** | `admin-mockups/design_files/*.html` |
| **Test spec** | `apps/web/e2e/visual-mockups/baseline.spec.ts` |
| **Snapshot dir** | `apps/web/e2e/visual-mockups/baseline.spec.ts-snapshots/` |
| **Static server** | `apps/web/scripts/serve-mockups.cjs` (porta 5174, zero-dep Node `http`) |
| **CI workflow** | `.github/workflows/visual-regression-mockups.yml` |
| **Threshold** | `maxDiffPixelRatio: 0.001` · `threshold: 0.2` · `animations: 'disabled'` |
| **Viewports** | Desktop 1440×900 · Mobile 375×812 (Pixel 5) |

## Perché mockup-as-baseline

Con il programma di V2 Design Migration (issue parent #571), i mockup di Claude Design sono il *contratto di design* contro il quale le migrazioni Phase 1+2 vengono validate.

- **Pro**: design e implementazione condividono la stessa fonte di verità; un drift visivo sull'app v2 diventa una diff misurabile rispetto al mockup committato.
- **Contro**: i mockup usano React UMD + Babel standalone runtime — più lenti del JSX compilato, ma sufficiente per visual diff (no perf budget).

Alternative scartate (no budget): Chromatic, Argos, Percy.

## Bootstrap iniziale (PR #0a → PR #0b)

**Stato attuale**: PR #0a introduce l'infrastruttura (spec, server, workflow, doc) **senza** PNG di baseline committate. La prima generazione produce PNG Linux x86-64 canonici per il runner CI. Tre opzioni:

### Opzione 1 — CI bootstrap (raccomandato, no Docker locale)

```bash
# Trigger manuale del workflow in modalità bootstrap
gh workflow run visual-regression-mockups.yml -f mode=bootstrap
# Attendi il run, scarica l'artefatto `visual-mockups-baselines-<run>`
gh run download <run-id> -n visual-mockups-baselines-<run>
# Copia i PNG nella cartella snapshot e committa
mv visual-mockups-baselines-*/* apps/web/e2e/visual-mockups/baseline.spec.ts-snapshots/
git add apps/web/e2e/visual-mockups/baseline.spec.ts-snapshots/
git commit -m "chore(visual): bootstrap mockup baselines (Linux x86-64)"
```

### Opzione 2 — Docker locale (riproducibile)

```bash
# Genera PNG Linux usando l'immagine ufficiale Playwright (matcha il runner CI)
docker run --rm -v "$(pwd):/work" -w /work/apps/web \
  --network host \
  mcr.microsoft.com/playwright:v1.57.0-noble \
  bash -c "npm install -g pnpm@10 && pnpm install --frozen-lockfile && \
           pnpm test:visual:mockups:update"
git add apps/web/e2e/visual-mockups/baseline.spec.ts-snapshots/
git commit -m "chore(visual): bootstrap mockup baselines (Linux x86-64)"
```

> ⚠️ Su Docker Desktop Windows `--network host` non funziona allo stesso modo che su Linux. Usa l'Opzione 1 (CI) se non hai un host Linux/macOS nativo.

### Opzione 3 — Run locale Windows/macOS (NON committare)

Per verifica visiva durante lo sviluppo (non per baseline canonici):

```bash
cd apps/web && pnpm test:visual:mockups:update
# I PNG generati (*-win32.png o *-darwin.png) NON devono essere committati;
# sono utili solo per verifica locale.
```

I file `*-linux.png` sono i canonici per CI; gli altri suffix vengono ignorati dal runner Linux.

## Workflow developer

### Aggiungere un nuovo mockup al baseline

1. Crea il mockup `admin-mockups/design_files/<slug>.html` (HTML statico + React UMD + JSX, vedi pattern `_common.md`).
2. Aggiungi un'entry al registry `MOCKUPS` in `apps/web/e2e/visual-mockups/baseline.spec.ts`:
   ```ts
   { slug: '<slug>', label: 'Human label', group: 'wave-N' },
   ```
3. Genera la baseline:
   ```bash
   cd apps/web
   pnpm test:visual:mockups:update
   ```
4. Verifica i PNG generati:
   ```bash
   ls e2e/visual-mockups/baseline.spec.ts-snapshots/<slug>-*.png
   ```
5. Commit della baseline insieme al mockup.

### Aggiornare la baseline dopo modifica mockup

```bash
cd apps/web
pnpm test:visual:mockups:update
git add e2e/visual-mockups/baseline.spec.ts-snapshots/
git commit -m "chore(visual): update mockup baseline — <reason>"
```

> ⚠️ Solo i mockup *intenzionalmente modificati* dovrebbero produrre diff. Se diff inattesi compaiono, indaga prima di committare.

### Verificare in locale prima di PR

```bash
cd apps/web
pnpm test:visual:mockups          # tutti i mockup, desktop + mobile
pnpm test:visual:mockups:desktop  # solo desktop
pnpm test:visual:mockups:mobile   # solo mobile
```

Output: `playwright-report/index.html` con diff side-by-side per ogni failure.

### Cosa fare quando CI fallisce

Lo workflow `Visual Regression — Mockup Baseline` posta un commento sulla PR con istruzioni. Sintesi:

1. Scarica artefatto `visual-mockups-report-<run>` da Actions tab.
2. Apri `playwright-report/index.html` localmente — diff side-by-side per i mockup falliti.
3. Decidi:
   - **Cambio voluto** → rigenera localmente `pnpm test:visual:mockups:update`, commit le PNG aggiornate.
   - **Cambio non voluto** → revert il commit incriminato; investiga (font drift, CSS regressione, JSX bug, ecc.).

## Architettura

```
┌──────────────────────────────────────────────────────────────────┐
│  apps/web/playwright.config.ts                                   │
│                                                                  │
│  webServer: [                                                    │
│    Next.js on :3000           ← richiesto da altri progetti      │
│    serve-mockups.cjs on :5174 ← serve admin-mockups/design_files │
│  ]                                                               │
│                                                                  │
│  projects: [                                                     │
│    mockup-baseline-desktop  (1440×900, threshold 0.001)          │
│    mockup-baseline-mobile   (375×812, Pixel 5, threshold 0.001)  │
│  ]                                                               │
└──────────────────────────────────────────────────────────────────┘
                                ↓
┌──────────────────────────────────────────────────────────────────┐
│  baseline.spec.ts                                                │
│                                                                  │
│  for (const mockup of MOCKUPS) {                                 │
│    test(...)                                                     │
│      → page.goto(`/${mockup.slug}.html`, networkidle)            │
│      → waitForMockupReady(page)                                  │
│        ├─ #root | #desktop-root | #mobile-root has children      │
│        ├─ document.fonts.ready                                   │
│        └─ 2× requestAnimationFrame                               │
│      → expect(page).toHaveScreenshot(`${slug}.png`)              │
│  }                                                               │
└──────────────────────────────────────────────────────────────────┘
```

### Mount point eterogenei

I mockup non condividono un mount point fisso:

| Mount ID | Mockup |
|---|---|
| `#root` | la maggioranza (auth-flow, public, sp4-*, sp3-*, ecc.) |
| `#desktop-root` + `#mobile-root` | split desktop/mobile (es. `settings.html`) |

`waitForMockupReady` accetta uno qualunque dei tre — risolve appena uno ha figli. Nuovi mount ID? Aggiungi al `candidates` array in `baseline.spec.ts:101`.

### Mobile vs desktop

`MockupEntry.desktopOnly: true` skippa la baseline mobile per layout solo desktop (es. `sp4-library-desktop`, `sp4-session-live`). Default: ogni mockup viene snapshottato su entrambi i viewport.

## Troubleshooting

| Sintomo | Causa probabile | Fix |
|---|---|---|
| `0 tests run` + esce in <1s | `process.env.CI = 'false'` (truthy stringa) e webServer non riusa server già attivi (porta 3000 in uso) | Esegui con `CI=` (vuoto) o stoppa Next.js dev prima |
| `TimeoutError: page.waitForFunction: Timeout 30000ms` | Mockup pesante, Babel impiega >30s | Aumenta timeout in `waitForMockupReady` |
| `expected screenshot to match` ma il diff è puro font rendering | Drift di font tra macchina locale e CI runner | Rigenera baseline su Linux x86-64 (matcha runner CI) |
| `webServer error: ECONNREFUSED :5174` | `serve-mockups.cjs` non si è avviato | Verifica `admin-mockups/design_files/` esista; vedi `MOCKUP_PORT` env |
| Snapshot pull request CI passa, locale fallisce | OS/font diff (Windows local vs Linux CI) | Le PNG committate sono *Linux x86-64*; locale dev macOS/Windows può vedere diff. Esegui in Docker se serve match esatto |

### Architecture drift (ARM64)

ADR-044 documenta che il rendering font/subpixel differisce tra x86-64 e ARM64. Il workflow CI gira su `ubuntu-latest` (x86-64); le baseline PNG committate sono x86-64. Se il runner cambia architettura, **rigenera tutte le baseline**.

## CI workflow

`.github/workflows/visual-regression-mockups.yml`:

- **Trigger**: PR su `main`, `main-dev`, `main-staging`, `frontend-dev` con paths:
  - `admin-mockups/design_files/**`
  - `apps/web/e2e/visual-mockups/**`
  - `apps/web/scripts/serve-mockups.cjs`
  - `apps/web/playwright.config.ts`
  - workflow stesso
- **Runner**: `ubuntu-latest`
- **Steps**: checkout → setup pnpm + node + playwright browsers → cp `.env.test.example` → `pnpm test:visual:mockups`
- **Artefatti su failure**: `playwright-report/` (14d retention) + `test-results/` con diff PNG
- **PR comment** automatico con istruzioni di rimedio

> Nota: la build Next.js NON è richiesta — i test colpiscono solo lo static server porta 5174.

## Limiti noti

- **Threshold molto stretto** (`maxDiffPixelRatio: 0.001`): 1 pixel su 1000 può far fallire il test. È intenzionale: la baseline è il design contract, non un'approssimazione.
- **No coverage cross-browser**: solo Chromium. WebKit/Firefox non coperti (budget; design contract è sufficient su Chromium).
- **JSX standalone runtime**: alcuni mockup possono impiegare 8–12s a bootstrap (Babel in-browser). Il timeout è 30s.
- **Fonts esterni** (Quicksand / Nunito / JetBrains Mono via `<link>`): se i CDN cambiano peso/hinting, le baseline vanno rigenerate. Nessun lock locale.
- **Mockup skippati temporaneamente** (campo `skipReason` nel registry):
  - `sp4-session-summary` — bootstrap >30s su CI runner (728 lines + parts file). Follow-up: split parts / lazy-mount / bundle-size investigation.
  - `onboarding` — bootstrap >30s su Windows locale (possibile errore JS runtime o state machine pesante). Follow-up: console-error investigation.
  Entrambi tracciati in issue follow-up; rimuovere `skipReason` dopo fix per riattivare il baseline.

## Riferimenti

- Issue parent: meepleai/meepleai#571
- Pattern brief mockup: `admin-mockups/briefs/_common.md`
- ADR-044 (architecture drift): `docs/architecture/adr/adr-044-self-hosted-arm64-runner.md`
- Playwright docs: https://playwright.dev/docs/test-snapshots
