# Design — Tool side-by-side mockup↔live route (GAP-6.3, #2999)

**Data**: 2026-07-17
**Issue**: [#2999](https://github.com/meepleAi-app/meepleai-monorepo/issues/2999) (GAP-6.3, sub di #2993, umbrella #2342)
**Decisione**: Opzione **A** — build greenfield di un tool side-by-side mockup↔live (scelta dall'utente su A vs B "re-promote pixel gate").

---

## 1. Contesto e problema

#2999 constata che **non esiste alcun tool side-by-side mockup↔live**. Il più vicino è la suite Storybook pixel-snapshot (`playwright.storybook.config.ts` + `e2e/storybook/*.snapshot.spec.ts`), **descoped dalla CI il 2026-07-15 (#2063)** perché:

- confronta **story-vs-baseline PNG** (deriva live-vs-live nel tempo), **non** mockup-vs-live;
- le baseline committate sono `*-win32.png` mentre la CI gira su ubuntu → nessun segnale reale (green theatre).

Il gap reale: un designer/dev non ha modo di vedere **il mockup di riferimento accanto all'implementazione live** per valutare la fedeltà. La ragione del descope (baseline win32↔linux divergono) è esattamente ciò che un tool di **confronto visivo manuale** (nessuna baseline, nessun pixel-diff, nessun gate CI) evita per costruzione — il senso di aver scelto A su B.

## 2. Goals / Non-goals

**Goals**
- Generare una **gallery HTML statica self-contained** che, per ogni coppia mockup↔route, mostri i due screenshot affiancati con uno **slider drag-to-reveal** per la sovrapposizione.
- Catturare il "live" dalle **route reali** dell'app (fedeltà produzione), rese deterministiche via **auth-bypass + mock API `page.route()`**.
- Riusare l'infrastruttura Playwright esistente (`playwright.config.ts`, `AdminHelper`, fixture `mockup-pilots/`).
- Essere un **dev tool locale** eseguibile con un solo comando (`pnpm compare:mockups`).

**Non-goals**
- **NON** è un gate CI né un pixel-diff automatico (immune al problema win32↔linux). Il giudizio di fedeltà resta umano.
- **NON** copre tutte le ~68 route al primo colpo — MVP su slice verticale, estensione via manifest.
- **NON** tocca la suite pixel Storybook descoped (resta dev-tool separato).
- **NON** richiede backend/DB seedati (i dati arrivano da mock `page.route()`).

## 3. Decisioni fissate (dalla sessione di brainstorming)

| # | Decisione | Alternativa scartata | Perché |
|---|---|---|---|
| D1 | Live = **route reali** app (`localhost:3000`) | Storybook story | Fedeltà produzione richiesta dall'utente |
| D2 | Dati = **auth-bypass + `page.route()` mock per-route** | rendering default / stack seedato reale | Deterministico, offline, dati comparabili al mockup; riusa pattern `admin-dashboard-visual.spec.ts` |
| D3 | Output = **gallery HTML statica con slider** | pannello iframe live / solo PNG | Portabile, si apre senza server, ottima UX; confronto manuale = niente baseline |

## 4. Architettura — 4 unità isolate

### 4.1 Manifest — `apps/web/e2e/mockup-compare/manifest.ts`
Dichiara le coppie. Nessun'altra responsabilità.
```ts
export interface MockupComparePair {
  id: string;                 // slug stabile, es. "library-desktop"
  label: string;              // titolo umano nella gallery
  mockupHtml: string;         // path relativo a admin-mockups/design_files/*.html
  route: string;              // route live, es. "/library"
  viewport?: { width: number; height: number }; // default 1920x1080
  auth?: 'user' | 'admin' | 'none';             // default 'user'
  mock?: (page: Page) => Promise<void>;         // page.route() setup opzionale
}
export const PAIRS: readonly MockupComparePair[] = [ /* ... */ ];
```
Sorgente del pairing: righe **page-mock** di `admin-mockups/MOCKUPS_INDEX.md` + annotazioni `@mockup` sui `page.tsx`.

### 4.2 Capture spec — `apps/web/e2e/mockup-compare/capture.spec.ts`
Playwright, riusa `playwright.config.ts` (webServer con `PLAYWRIGHT_AUTH_BYPASS=true`, `AdminHelper`, `page.route`). Per ogni pair:
1. **Mockup**: naviga al file HTML statico servito da `http-server` (già dep `^14.1.1`) su una porta dedicata (es. 5175), screenshot full-page → `<id>__mockup.png`.
2. **Live**: setup auth (`AdminHelper.setupAdminAuth` per admin, bypass per user), applica `pair.mock?.(page)`, naviga `pair.route`, attende `networkidle` + settle, screenshot full-page → `<id>__live.png`.
3. Emette un record in `captures.json` (`{ id, label, route, viewport, mockupPng, livePng, liveError? }`).

Output in `apps/web/mockup-compare-output/` (**gitignored**). Su fallimento live: cattura assente, `liveError` valorizzato, si prosegue (no crash).

### 4.3 Report builder — `apps/web/scripts/build-mockup-compare-report.mjs`
**Funzione pura** `buildReport(captures, fidelityMeta) → htmlString`. Legge `captures.json`, embedda i PNG come `data:` URI (self-contained), impagina una **gallery**: una riga per pair con:
- header (label, route, viewport, `design_intent` dal `.fidelity.json` se presente);
- **comparatore slider** (drag-to-reveal) + toggle side-by-side/overlay;
- placeholder "live capture failed — <error>" se `liveError`.
Scrive `apps/web/mockup-compare-output/gallery.html`.

### 4.4 Template gallery
HTML/CSS/JS **inline** (nessuna risorsa esterna). Slider: due immagini sovrapposte + input range che regola `clip-path`/width del layer superiore. Toggle side-by-side. Vanilla JS, zero dipendenze.

## 5. Data flow

```
manifest.ts
   │
   ▼
capture.spec.ts  ── http-server (mockup) ──▶  <id>__mockup.png
   │             ── app :3000 (live+mock) ──▶  <id>__live.png
   ▼
captures.json
   │
   ▼
build-mockup-compare-report.mjs  ──▶  gallery.html  ──▶  open
```

Orchestrazione: script npm **`compare:mockups`** in `apps/web/package.json`:
```
"compare:mockups": "dotenv -e .env.test -- playwright test --config playwright.mockup-compare.config.ts && node scripts/build-mockup-compare-report.mjs"
```
Il report builder, al termine, **stampa il path assoluto** di `gallery.html` (non auto-apre, per determinismo cross-platform e per non bloccare in CI/headless). Un flag opzionale `--open` tenta un best-effort cross-platform (`start`/`open`/`xdg-open`).

Config Playwright dedicata `playwright.mockup-compare.config.ts` (deriva da quella principale; avvia sia il Next webServer auth-bypass sia `http-server` per i mockup su porta dedicata).

## 6. Scope MVP (YAGNI — slice verticale)

Prima release su **3-4 coppie che hanno già fixture/pattern**, per provare l'harness end-to-end senza scrivere molte mock:
- `/library` (fixture `mockup-pilots/library.ts`) ↔ `sp4-library-desktop.html`
- `/games/[id]` (fixture `mockup-pilots/game-detail.ts`) ↔ mockup game-detail
- `/admin` (pattern mock già in `admin-dashboard-visual.spec.ts`) ↔ mockup admin dashboard

La capture spec **`log()`-a** le coppie del manifest ancora scoperte (nessun cap silenzioso). Estensione = aggiungere righe al manifest + fixture mock dove serve.

## 7. Error handling
- Route live fallisce load/auth → `liveError` nel record; gallery mostra mockup + placeholder rosso, gli altri pair non sono impattati.
- Mockup HTML mancante → pair skippato con `log()` esplicito.
- `http-server`/webServer non parte → Playwright fallisce fast; messaggio actionable nel config.

## 8. Testing
- **Report builder** = funzione pura → unit test Vitest: input `captures.json` fixture (con e senza `liveError`) → asserzioni su struttura HTML (presenza righe, data-URI, placeholder errore, slider markup).
- **Capture spec**: validata eseguendo `pnpm compare:mockups` sullo slice MVP (smoke: 3-4 PNG generati + gallery aperta).
- **Manifest** = dati; un test minimale asserisce id univoci + path mockup esistenti.

## 9. Rischi / note
- **Fedeltà mockup vs live**: molte route mostreranno drift legittimo (il tool serve proprio a evidenziarlo); la gallery non giudica, mostra.
- **Full-page screenshot height**: mockup e live avranno altezze diverse → lo slider allinea in alto; nota nel template.
- **Manutenzione manifest**: per non divergere da `MOCKUPS_INDEX.md`, un test asserisce che i `mockupHtml` del manifest esistano su disco.
- **Output gitignored**: aggiungere `apps/web/mockup-compare-output/` a `.gitignore`.

## 10. Riferimenti
- Issue #2999 · umbrella #2993/#2342 · descope #2063 · Linux-baseline #2095
- `apps/web/playwright.config.ts` (auth-bypass webServer, `AdminHelper`)
- `apps/web/e2e/visual/admin-dashboard-visual.spec.ts` (pattern page.route mock)
- `apps/web/src/__tests__/fixtures/mockup-pilots/` (fixture riusabili)
- `admin-mockups/MOCKUPS_INDEX.md` (pairing page-mock↔route)
- `docs/for-developers/frontend/page-mock-story-pattern.md` (§ descope pixel gate)
