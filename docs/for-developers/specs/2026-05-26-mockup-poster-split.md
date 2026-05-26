# Mockup Poster → Standalone Page Split

> **Date**: 2026-05-26
> **Status**: Approved (user confirmed Q1+Q2+Q6 — 2026-05-26)
> **Owner**: Frontend / Design Handoff
> **Related**: [admin-mockups/README.md](../../../admin-mockups/README.md), [MOCKUPS_INDEX.md](../../../admin-mockups/MOCKUPS_INDEX.md), [scripts/mockup_demo/](../../../admin-mockups/scripts/mockup_demo/)
> **Discussion record**: spec-panel review (Wiegers / Adzic / Fowler / Hohpe / Crispin) — see § Appendix

## TL;DR

I file in `admin-mockups/mockup/*.html` sono **poster di confronto** (N varianti affiancate per design review), non pagine HTML standalone. Quando Claude prova a replicarli, riproduce il poster completo invece della singola pagina UI. Soluzione: **estendere `scripts/mockup_demo/` con uno script `split_presentation.py` che genera N file HTML standalone** (uno per variante) in `admin-mockups/standalone/`, ereditando i token canonici da `design_files/tokens.css`. Master = poster (sorgente designer), output = N pagine isolate (consumer Claude/dev).

## Problem

### Cosa abbiamo oggi

`admin-mockups/` contiene due ecosistemi di mockup distinti:

| Path | Pattern | Esempi | Usabile da Claude? |
|---|---|---|:---:|
| `design_files/sp4-*.html` | 1 file = 1 pagina, shell minimale che importa `tokens.css` + `components.css` + carica `sp4-X.jsx` | `sp4-dashboard.html` (21 righe), `sp4-game-detail.html`, `sp4-discover.html`, ecc. (61 mockup) | ✅ Sì |
| `mockup/*.html` | 1 file = N varianti affiancate, CSS inline (token re-inlinati), wrapper di galleria `.mockup-row > .mockup-col > .mockup-label + .phone` | `dashboard-new-user-mockup.html` (646 righe, 3 varianti), `mobile-card-layout-mockup.html` (1261 righe), `us32-play-records-mockups.html` (1197 righe) | ❌ No |

Quando l'utente chiede "replica `dashboard-new-user-mockup.html`", l'agente vede:
- `body { background: #e8e4df; padding: 24px 16px; display: flex; ... }` ← chrome di poster, non viewport reale
- `<div class="mockup-row">` con 3 `<div class="mockup-col">` affiancati ← layout di galleria, non layout app
- `<div class="mockup-label">Games Block</div>` ← annotazione di presentazione
- 3 `<div class="phone">` ← 3 schermate da replicare separatamente, ma in galleria affiancata

Risultato: l'agente produce una pagina-galleria, non l'app singola.

### Inventario poster

Su 10 file in `admin-mockups/mockup/`:

| File | `.mockup-col` | `.phone` | Sotto-tipo | Note |
|---|:---:|:---:|---|---|
| `dashboard-new-user-mockup.html` | 3 | 3 | **A — Scroll-sections** | 3 viste della stessa pagina scrollata a posizioni diverse (Games / Sessions / Toolkit). Da fondere in **1** file standalone. |
| `mobile-card-layout-mockup.html` | 2 | 2 | **B — Mockup-col wrapped** | 2 varianti reali (Card View Focus + Drawer View). Split in **2** file. |
| `meeple-card-drawer-tabs-mockup.html` | 1 | 1 | **B — Singolo wrap** | Già 1 variante singola, ma con chrome di poster. Convertire in **1** file pulito. |
| `mobile-card-entity-types.html` | 0 | 7 | **C — Phone-only (no wrap)** | 7 phone senza `.mockup-col`. Da etichettare e splittare in **7** file. |
| `toolkit-mobile-mockup.html` | 0 | 4 | **C — Phone-only** | 4 phone senza wrap. Split in **4** file. |
| `us32-play-records-mockups.html` | 0 | 6 | **C — Phone-only** | 6 phone senza wrap (notare plurale `mockups`). Split in **6** file. |
| `meeple-card-nav-buttons-mockup.html` | 0 | 0 | **D — Componente UI** | Matrice 6 entity × 11 sezioni di NavigationFooter (2887 righe). Pattern `.mc` ripetuto, no phone. |
| `meeple-card-real-app-render.html` | 0 | 0 | **D — Componente UI** | "Come appaiono DAVVERO nell'app" — render reale dei MeepleCard in contesto pagina (1231 righe). |
| `meeple-card-summary-render.html` | 0 | 0 | **D — Componente UI** | Component Summary — Visual Reference (554 righe). |
| `meeple-card-visual-test.html` | 0 | 0 | **D — Componente UI** | Full Features visual test (968 righe). |

**Totale output atteso (sotto-tipi A+B+C)**: 1 + 2 + 1 + 7 + 4 + 6 = **21 file standalone** da 6 poster.

**Sotto-tipo D**: i 4 file `meeple-card-*` sono **mockup di componenti** (showcase MeepleCard variants), non pagine. Niente `.phone` da estrarre. **Decisione utente 2026-05-26**: includere nel rollout via **modalità `per-component`** distinta (vedi § Algorithm), in **Wave 3** post-pilot. Pattern: estrarre ogni `.section` o gruppo coerente di `.mc` come file isolato.

## Goal & Non-Goals

### Goal

1. **G1** — Ogni variante UI rilevante in `mockup/*.html` deve esistere come file HTML standalone (1 file = 1 pagina, viewport reale, chrome-free).
2. **G2** — Gli standalone devono importare `tokens.css` + `components.css` canonici da `design_files/`, eliminando duplicazione CSS inline.
3. **G3** — Il poster originale resta **source of truth** (la designer continua a editarlo); gli standalone sono **artefatti generati** rigenerabili idempotentemente.
4. **G4** — Lo script segue il pattern già consolidato di `scripts/mockup_demo/` (Python 3.11+ stdlib, no deps, idempotente con marker).

### Non-Goal

- ❌ **Non riscrivere a mano** i poster (sarebbe l'opzione A; scartata).
- ❌ **Non eliminare i poster sorgente** (restano source of truth per il designer; gli standalone sono derivati).
- ❌ **Non ripristinare il visual gate** (rimosso 2026-05-20 per false positives; validazione conformity resta manuale).
- ❌ **Non toccare `admin-mockups/design_files/`** (61 mockup già conformi).
- ❌ **Non sostituire `MOCKUPS_INDEX.md`** (sarà aggiornato per riferire anche gli standalone; nessuna riscrittura).

## Solution

### Output structure

```
admin-mockups/
├── mockup/                              (poster — SOURCE OF TRUTH, editato dal designer)
│   ├── dashboard-new-user-mockup.html
│   ├── mobile-card-layout-mockup.html
│   └── ... (10 poster invariati)
├── standalone/                          (auto-generato — CONSUMER Claude/dev)
│   ├── dashboard-new-user.html             (sotto-tipo A: 1 pagina fusa)
│   ├── mobile-card--card-view-focus.html   (sotto-tipo B: 1 per variante)
│   ├── mobile-card--drawer-view.html
│   ├── meeple-card-drawer-tabs.html        (sotto-tipo B: già singolo)
│   ├── mobile-card-entity--game.html       (sotto-tipo C: 1 per phone)
│   ├── mobile-card-entity--player.html
│   ├── ...
│   └── _index.html                         (navigazione tra gli standalone)
├── design_files/                        (61 mockup conformi — INVARIATO)
│   ├── tokens.css                          (referenziato dagli standalone via ../design_files/tokens.css)
│   ├── components.css
│   └── sp4-*.html, sp3-*.html, ...
├── scripts/
│   └── mockup_demo/
│       ├── build_map.py                    (esistente)
│       ├── apply_map.py                    (esistente)
│       ├── classify_todos.py               (esistente)
│       ├── validate.py                     (esistente)
│       └── split_presentation.py           ← NUOVO
└── MOCKUPS_INDEX.md                     (aggiornato con sezione "Standalone derivati")
```

### Convention (su poster source)

Affinché lo script possa estrarre varianti deterministicamente, i poster di sotto-tipo A/B/C richiedono **minimal annotation** via `data-*`:

#### Sotto-tipo A (scroll-sections, fonde tutto in 1 file)

```html
<head>
  <meta name="mockup-split-mode" content="merge-sections">
  <meta name="mockup-output-name" content="dashboard-new-user">
</head>
<body>
  <div class="mockup-row">
    <div class="mockup-col" data-section="games"><div class="phone">...</div></div>
    <div class="mockup-col" data-section="sessions-agents"><div class="phone">...</div></div>
    <div class="mockup-col" data-section="toolkit"><div class="phone">...</div></div>
  </div>
</body>
```

→ output: `standalone/dashboard-new-user.html` (1 file, sezioni concatenate nel `.pgc` scrollabile).

#### Sotto-tipo B (mockup-col wrapped, N varianti)

```html
<head>
  <meta name="mockup-split-mode" content="per-variant">
  <meta name="mockup-output-prefix" content="mobile-card">
</head>
<body>
  <div class="mockup-row">
    <div class="mockup-col" data-variant="card-view-focus"><div class="phone">...</div></div>
    <div class="mockup-col" data-variant="drawer-view"><div class="phone">...</div></div>
  </div>
</body>
```

→ output: `standalone/mobile-card--card-view-focus.html` + `mobile-card--drawer-view.html`.

#### Sotto-tipo C (phone-only, no wrap)

Lo script tratta direttamente ogni `.phone` come variante isolata, usando come `data-variant` un attributo aggiunto al `.phone`:

```html
<head>
  <meta name="mockup-split-mode" content="per-variant">
  <meta name="mockup-output-prefix" content="us32-play-records">
</head>
<body>
  <div class="phone" data-variant="empty">...</div>
  <div class="phone" data-variant="single-play">...</div>
  <div class="phone" data-variant="multi-play-active">...</div>
  ...
</body>
```

→ output: `standalone/us32-play-records--empty.html`, `us32-play-records--single-play.html`, ecc.

#### Sotto-tipo D (component showcase — `per-component` mode)

```html
<head>
  <meta name="mockup-split-mode" content="per-component">
  <meta name="mockup-output-prefix" content="meeple-card-real-app">
</head>
<body>
  <div class="section" data-component="grid-game-not-in-collection"><div class="mc">...</div></div>
  <div class="section" data-component="grid-game-in-collection"><div class="mc">...</div></div>
  <div class="section" data-component="grid-player"><div class="mc">...</div></div>
  ...
</body>
```

→ output: `standalone/meeple-card-real-app--grid-game-not-in-collection.html`, ecc. Stesso pattern di estrazione ma su `.section[data-component]` invece di `.phone[data-variant]`. Output viewport reale (no phone frame).

### Algorithm (split_presentation.py)

```
INPUT  : admin-mockups/mockup/*.html (ogni file con <meta name="mockup-split-mode">)
OUTPUT : admin-mockups/standalone/<output-name|prefix>--<variant>.html
MARKER : <!-- SPLIT-GEN: do not edit, regenerate via split_presentation.py -->
DEPS   : Python 3.11+ stdlib only (html.parser, pathlib, re, argparse, sys)

ALGORITHM:

for each poster in mockup/*.html:
    parse <head> → estrai <meta name="mockup-split-mode"> + output naming
    if mode is missing:
        WARN ("poster needs annotation, skipping") + continue

    parse <style> blocks → split in:
      - "poster chrome" (selettori .mockup-row, .mockup-col, .mockup-label, .pg-title, .legend, body { padding/background di galleria })
      - "page styles" (tutto il resto)

    for each variant in (.mockup-col[data-variant] | .phone[data-variant] | .section[data-component]):
        build standalone HTML:
            <!DOCTYPE html>
            <html lang="it" data-theme="light">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{poster-title} — {variant}</title>
              <!-- SPLIT-GEN: source=mockup/{poster}.html variant={variant} regenerated={ISO-timestamp} -->
              <link rel="preconnect" href="https://fonts.googleapis.com"/>
              <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin/>
              <link href="https://fonts.googleapis.com/css2?family=Quicksand:wght@400;500;600;700&family=Nunito:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;500;600;700;800&display=swap" rel="stylesheet"/>
              <link rel="stylesheet" href="../design_files/tokens.css"/>
              <link rel="stylesheet" href="../design_files/components.css"/>
              <style>{page-styles only — chrome stripped}</style>
            </head>
            <body class="standalone-viewport">
              {extracted .phone content, lifted out of .mockup-col wrapper}
            </body>
            </html>

        write to standalone/<output-prefix>--<variant>.html
            (overwrites only if file starts with SPLIT-GEN marker — never overwrites manual files)

    for mode "merge-sections":
        concat ALL .phone contents into single .pgc scrollable area
        output ONE file: standalone/<output-name>.html

generate standalone/_index.html:
    list of all generated files con thumbnail iframe + link al sorgente poster
```

### Idempotency contract

- File con marker `<!-- SPLIT-GEN: ... -->` nelle prime 20 righe → **sovrascrivibile** dallo script.
- File senza marker → **mai sovrascritto** (errore con messaggio chiaro).
- Re-run dello script senza modifiche al poster → output bit-identico (deterministico).

### Validation strategy

#### Pilot validation

Per il poster pilot (raccomandato: `dashboard-new-user-mockup.html`):
1. Generare gli standalone con lo script
2. Aprire il poster originale in un browser (a sinistra)
3. Aprire ciascuno standalone in un browser/iframe (a destra)
4. Confronto visivo manuale — l'agente esegue check su:
   - **Token coverage**: nessun colore hardcoded leftover (greppare `#[0-9a-f]{3,6}` negli standalone)
   - **Component reuse**: nessuna definizione duplicata di `.phone`, `.topbar`, `.bbar` (devono venire da `components.css`)
   - **Visual diff**: spot-check 5-7 elementi (logo, color tokens, padding, shadow, font weight)
5. Report finale in `docs/for-developers/audits/2026-05-26-mockup-split-pilot.md`

#### Post-pilot rollout validation

- Per i 5 poster rimanenti (B + C), pipeline veloce:
  - Generare → ispezionare `_index.html` → eyeball ogni iframe → fix annotations se necessario.
- Aggiornare `MOCKUPS_INDEX.md` con sezione "Standalone derivati (auto-gen)".

#### Long-term validation

Lo script `validate.py` esistente può essere esteso (in PR successivo, fuori scope) per:
- Verificare che ogni poster sotto-tipo A/B/C abbia `<meta name="mockup-split-mode">`.
- Verificare che ogni file in `standalone/` con marker abbia un poster sorgente valido.
- Verificare che gli standalone non duplichino selettori CSS già presenti in `components.css`.

## Pilot plan

### Step 1 — Annotate pilot poster (~30 min)

File: `admin-mockups/mockup/dashboard-new-user-mockup.html`

Modifiche:
- Aggiungere `<meta name="mockup-split-mode" content="merge-sections">` in `<head>`
- Aggiungere `<meta name="mockup-output-name" content="dashboard-new-user">` in `<head>`
- Aggiungere `data-section="games"`, `data-section="sessions-agents"`, `data-section="toolkit"` ai 3 `.mockup-col`
- Commit a parte: `chore(mockups): annotate dashboard-new-user-mockup for split (issue #X)`

### Step 2 — Implement split_presentation.py (~2-3 h)

File: `admin-mockups/scripts/mockup_demo/split_presentation.py`

Output:
- Script funzionante per modalità `merge-sections` e `per-variant`
- CLI: `python -m scripts.mockup_demo.split_presentation [--poster PATH] [--all] [--dry-run]`
- Test inline (docstring + `if __name__ == "__main__"` self-test su pilot)

### Step 3 — Run pilot + validate (~30-60 min)

```bash
cd admin-mockups
python -m scripts.mockup_demo.split_presentation --poster mockup/dashboard-new-user-mockup.html
# inspect standalone/dashboard-new-user.html
# open browser side-by-side
# write audit report
```

### Step 4 — User review checkpoint

Pausa per review utente. Punti di feedback attesi:
- Output naming convention va bene?
- Output structure (`standalone/` come sibling di `mockup/`) va bene o preferisce sottocartella di `mockup/`?
- Eventuali tweak alle annotations dei poster (es. preferisce un single attribute `data-split` invece di `data-variant`/`data-section`)?
- Vuole il `_index.html` o no?

## Rollout plan

Una volta validato il pilot:

### Wave 1 — Sotto-tipo B (varianti reali, già wrapped)
- `mobile-card-layout-mockup.html` → 2 standalone
- `meeple-card-drawer-tabs-mockup.html` → 1 standalone

Effort: ~30 min (annotation + run). Bassa friction perché `.mockup-col` esiste già.

### Wave 2 — Sotto-tipo C (phone-only, richiede `data-variant` su `.phone`)
- `mobile-card-entity-types.html` → 7 standalone
- `toolkit-mobile-mockup.html` → 4 standalone
- `us32-play-records-mockups.html` → 6 standalone

Effort: ~1-1.5 h (più phone da annotare). Naming `data-variant` richiede leggere il contesto di ciascun phone.

### Wave 3 — Sotto-tipo D (component showcase, modalità `per-component`)
- `meeple-card-real-app-render.html` → N standalone (1 per `.section[data-component]`)
- `meeple-card-summary-render.html` → N standalone
- `meeple-card-visual-test.html` → N standalone
- `meeple-card-nav-buttons-mockup.html` → N standalone (più complesso, 2887 righe)

Effort: ~2-3 h. Annotation richiede leggere ogni `.section` per assegnare `data-component` semantico. Estensione dello script per supportare la modalità `per-component`.

### Wave 4 — Aggiornamento docs
- Aggiornare `admin-mockups/MOCKUPS_INDEX.md` con sezione "Standalone derivati".
- Aggiornare `admin-mockups/README.md` § "Wiring toolchain" con il nuovo script.
- Audit doc finale in `docs/for-developers/audits/2026-05-26-mockup-split-final.md`.

### Out-of-scope (futuri PR)

- **Visual regression auto** per gli standalone (richiede ripristino del visual gate rimosso — fuori scope, vedi CLAUDE.md § Active Freezes).
- **Integration con il navigation toolchain** (`build_map.py`/`apply_map.py` per fare anche gli standalone navigabili da `index.html`). Possibile, non urgente.
- **JS-rendered posters**: `meeple-card-drawer-tabs-mockup.html` rende i phone via `setH('mockup-row', '<div class="mockup-col">...')` runtime (vedi riga ~220). Il parser stdlib del nostro script vede solo il DOM statico iniziale (vuoto). Richiede strategia diversa: Playwright DOM export (post-`renderAll()`) → freeze HTML → split via script statico. Differito a un PR successivo.

### Pilot/Wave run log

| Wave | Date | Poster | Outputs | Notes |
|---|---|---|---|---|
| Pilot | 2026-05-26 | `dashboard-new-user-mockup.html` | `dashboard-new-user.html` | merge-sections, 3 phones stacked (per scelta utente — refinement vero-merge differito) |
| 1 | 2026-05-26 | `mobile-card-layout-mockup.html` | `mobile-card--card-view-focus.html`, `mobile-card--drawer-view.html` | per-variant, OK |
| 1 (deferred) | — | `meeple-card-drawer-tabs-mockup.html` | — | **Skip**: phone generati via JS runtime, parser statico non li vede. Vedi § Out-of-scope. |
| 2.1 | 2026-05-26 | `mobile-card-entity-types.html` | `mobile-card-entity--{game,session,agent,chat,kb,player,event}.html` (7) | per-variant, OK |
| 2.2 | 2026-05-26 | `toolkit-mobile-mockup.html` | `toolkit-mobile--{dice-build,dice-result,quick-log,win-tracker}.html` (4) | per-variant, OK |
| 2.3 | 2026-05-26 | `us32-play-records-mockups.html` | `play-records--{list,new-step1-game,new-step2-players,new-step3-summary,detail,stats}.html` (6) | per-variant, dark theme preservato, OK |
| 3 (D) | TBD | `meeple-card-{nav-buttons-mockup,real-app-render,summary-render,visual-test}.html` | TBD | **Pending**: component showcase, modalità `per-component`. Effort separato (file 554–2887 righe). |

**Totale Wave 1+2 (5 poster, 20 standalone)**: Pilot (1) + Wave 1 (2) + Wave 2.1 (7) + Wave 2.2 (4) + Wave 2.3 (6).

## Open Questions — Decisions log

| # | Question | Decision (2026-05-26) | Status |
|---|---|---|:---:|
| Q1 | Pilot file | **`dashboard-new-user-mockup.html`** (sotto-tipo A, merge-sections) | ✅ Confermato |
| Q2 | Output cartella | **`admin-mockups/standalone/`** (sibling di `mockup/`) | ✅ Confermato |
| Q3 | Generare `_index.html` di navigazione | **Sì** (default proposal) | ✅ Default |
| Q4 | Convenzione attributo | **Separati**: `data-variant` per per-variant, `data-section` per merge-sections, `data-component` per per-component | ✅ Default |
| Q5 | CSS chrome detection | **Hardcoded list iniziale** (selettori `.mockup-row`, `.mockup-col`, `.mockup-label`, `.pg-title`, `.legend`, `body { padding }`, ecc.) | ✅ Default |
| Q6 | Sotto-tipo D (component showcase) | **Include nel rollout** come Wave 3, modalità `per-component` distinta | ✅ Confermato |

## Appendix — Spec panel discussion record

Discussion mode review with 5 experts (date: 2026-05-26):

- **Karl Wiegers** (Requirements Engineering): "1 file = 1 acceptance criterion. Poster con N varianti viola testabilità singola — manca una specifica univoca verificabile."
- **Gojko Adzic** (Specification by Example): "Living docs pattern — master narrativo per designer + estratti eseguibili per Claude/dev. Cross-pollination con Karl: i due punti convergono su 'spec eseguibile per ciascun esempio'."
- **Martin Fowler** (Architecture): "DRY violation: tokens re-inlinati nei poster. La separation of concerns di `design_files/` (tokens + components esterni) è già la soluzione architetturale."
- **Gregor Hohpe** (Integration Patterns): "Publish-subscribe: poster = publisher (1 sorgente), standalone = subscriber (N consumer). Estendere il toolchain già esistente in `scripts/mockup_demo/` allinea il pattern."
- **Lisa Crispin** (Agile Testing): "Senza visual gate, side-by-side iframe = nuovo conformity test. Gli standalone aumentano in importanza, non diminuiscono."

**Convergent insights**:
- Toolchain extension > rewrite (Hohpe + Fowler)
- 1 file = 1 spec verificabile (Wiegers + Adzic)
- Manual side-by-side è ora il nostro tier-1 di verifica visiva (Crispin)

**Productive tensions**:
- Wiegers ("1 file per acceptance") vs design realtà (poster sotto-tipo A è 1 pagina con 3 sezioni di scroll) → risolto col mode `merge-sections` (1 acceptance = 1 pagina, anche se sorgente ha 3 viste)

**Strategic questions deferred** (per futuri PR):
- Quando re-introdurre un visual gate automatico per gli standalone?
- Vale la pena estendere lo split anche a `design_files/02-desktop-patterns.html` e `03-drawer-variants.html` (anch'essi poster comparativi)?
