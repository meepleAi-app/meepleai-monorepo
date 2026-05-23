# Mockup Refinement — Aaron Core + Cross-cutting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Raffinare i mockup del cluster libro-game (CORE Aaron) + creare `chat-fullscreen.html` (B11b) + creare `state-matrix.html` (B18), incluse 2 actor variations (passive co-player + phone-passing).

**Architecture:** Workflow ibrido per mockup HTML: (1) engineer prepara brief contestualizzato dalla spec, (2) user esegue Claude Design Web producendo HTML, (3) engineer integra output nel file target (`admin-mockups/design_files/`), valida acceptance criteria via grep + visual inspection, committa. Pattern "extend existing" per `error-states`, `translate-viewer`, `glossary-editor`, `photo-upload` (preserva continuità); pattern "create new" per `chat-fullscreen.html` e `state-matrix.html`.

**Tech Stack:** HTML + CSS (componenti già presenti in `admin-mockups/design_files/components.css`), JSX twins per i mockup principali, design tokens canonici da `tokens.css`. Nessuna build step — i file sono statici. Riuso di pattern da `nanolith-runthrough-*`, `sp6-libro-game-*`, `sp4-citation-pdf-viewer`.

**Spec sorgente**: [`docs/superpowers/specs/2026-05-23-mockup-refinement-aaron-core-design.md`](../specs/2026-05-23-mockup-refinement-aaron-core-design.md)

**Branching**: feature branch `feature/mockup-refinement-aaron-design` da `main-dev`, PR target `main-dev`.

---

## File Structure

### Files da modificare (extend existing)

| File | Sezione spec | Cosa cambia |
|---|---|---|
| `admin-mockups/design_files/nanolith-runthrough-error-states.html` | 1a | + 6 nuovi stati OCR/AI (ocr-low-confidence, photo-blur-pre-ocr, translation-timeout, source-lang-unknown, network-mid-ocr, quota-exhausted-mid-stream) |
| `admin-mockups/design_files/nanolith-runthrough-translate-viewer.html` | 1b + 1d | + 4-step loading sequence, reader mode toggle 📖, wake-lock indicator 🔆, contrasto AAA, 3 sub-screen multi-lang (lang-detection-badge, lang-override-modal, manual-input-mode) |
| `admin-mockups/design_files/sp6-libro-game-photo-upload.html` | 1d | + kebab menu *"Digita manualmente"* entry point per manual-input-mode |
| `admin-mockups/design_files/nanolith-runthrough-glossary-editor.html` | 1c | + 4 sub-screen (conflict-dialog, bulk-import-card, variant-expansion, filter-strip+context-badges) + commento modello dati `GlossaryEntry` |
| `admin-mockups/design_files/sp6-libro-game-glossary-editor.jsx` | 1c | Sync .jsx twin con HTML modificato |
| `admin-mockups/design_files/sp6-libro-game-translation-viewer.jsx` | 1b + 1d | Sync .jsx twin con HTML modificato |
| `admin-mockups/MOCKUPS_INDEX.md` | 9 (AC globali) | Aggiornare count (page-mock 48→49, dev-fixture 10→11) + nuovi entry |
| `docs/for-developers/frontend/v2-migration-matrix.md` | 9 (AC globali) | Aggiornare route mapping per `/chat/[threadId]` (chat-fullscreen) + entry stati |
| `docs/for-developers/audits/2026-05-22-mockup-gaps.md` | 9 (AC globali) | Marcare Phase 1 in implementation |

### Files da creare (new)

| File | Sezione spec | Tipo MOCKUPS_INDEX |
|---|---|---|
| `admin-mockups/design_files/chat-fullscreen.html` | 2 (B11b) | page-mock |
| `admin-mockups/design_files/chat-fullscreen.jsx` | 2 (B11b) | twin jsx |
| `admin-mockups/design_files/state-matrix.html` | 3 (B18) | dev-fixture |

---

## Workflow per ogni task mockup

Ogni task mockup segue questo pattern (~30-60 min totali):

1. **Read target file** — capire lo stato attuale (Read tool)
2. **Prepare brief** — scrivere prompt strutturato per Claude Design Web (mostrato inline nel plan, copy-paste ready)
3. **User executes Claude Design Web** — produce HTML/JSX output. Engineer pausa e aspetta.
4. **Integrate output** — engineer applica il contenuto al file target (Edit/Write tool)
5. **Validate AC** — grep per termini chiave + visual review browser
6. **Commit** — branch feature, conventional commit message

---

## Task 0: Setup branch + verifica baseline

**Files:**
- No file changes, only git ops + verification

- [ ] **Step 0.1: Verifica branch corrente è clean su main-dev**

Run:
```bash
git status
git branch --show-current
git pull --ff-only
```

Expected: branch `main-dev`, working tree clean, no divergence.

- [ ] **Step 0.2: Crea feature branch**

Run:
```bash
git checkout -b feature/mockup-refinement-aaron-design
git config branch.feature/mockup-refinement-aaron-design.parent main-dev
```

Expected: `Switched to a new branch 'feature/mockup-refinement-aaron-design'`

- [ ] **Step 0.3: Verifica spec + audit committati (da sessione precedente)**

Run:
```bash
git log --oneline -3
ls docs/superpowers/specs/2026-05-23-mockup-refinement-aaron-core-design.md
ls docs/for-developers/audits/2026-05-22-mockup-gaps.md
```

Expected: entrambi i file presenti. Se non committati, fare commit prima:
```bash
git add docs/superpowers/specs/2026-05-23-mockup-refinement-aaron-core-design.md docs/for-developers/audits/2026-05-22-mockup-gaps.md
git commit -m "docs(design): mockup refinement spec + audit 2026-05-22"
```

- [ ] **Step 0.4: Push branch**

Run:
```bash
git push -u origin feature/mockup-refinement-aaron-design
```

---

## Task 1: Sezione 1a — `nanolith-runthrough-error-states.html` (+ 6 stati)

**Files:**
- Modify: `admin-mockups/design_files/nanolith-runthrough-error-states.html`

- [ ] **Step 1.1: Read existing file**

Run via Read tool: `admin-mockups/design_files/nanolith-runthrough-error-states.html`

Identificare:
- Pattern attuale per le 4 cellule esistenti (stream-timeout, OCR-fail, LLM-503, segmentation-fail)
- Sezione `<head>` con design tokens link
- Struttura grid contenente le cellule

- [ ] **Step 1.2: Prepare Claude Design Web brief**

Brief da copia-incollare in Claude Design Web:

```
Contesto: sto estendendo il mockup HTML esistente
`admin-mockups/design_files/nanolith-runthrough-error-states.html` aggiungendo
6 nuovi stati di errore al pattern cross-cutting già usato per le 4 celle
esistenti (stream-timeout, OCR-fail, LLM-503, segmentation-fail).

Mantieni STILE IDENTICO al pattern esistente: stesso layout grid, stessi token
CSS, stessa struttura per ogni cellula (icon + message + 2 CTA: retry +
secondary).

Aggiungi queste 6 cellule (NEL FILE esistente, NON nuovo file):

1. `ocr-low-confidence` — Icon ⚠️ giallo · Title "Testo poco chiaro" ·
   Message "Alcune regioni della foto sono difficili da leggere
   (confidence < 0.7)" · CTA primary "Ritocca foto" · CTA secondary
   "Procedi comunque"

2. `photo-blur-pre-ocr` — Icon 📷 · Title "Foto sfocata" · Message
   "L'autofocus non ha agganciato il libro. Riprova mantenendo il
   telefono fermo." · CTA primary "Riprova" (no roundtrip server) · NO
   CTA secondary (è rilevazione client-side)

3. `translation-timeout` — Icon ⏱️ · Title "Traduzione lenta" · Message
   "L'AI ci sta mettendo più di 20 secondi. Probabilmente sovraccarico
   del modello." · CTA primary "Riprova" · CTA secondary "Digita
   manualmente"

4. `source-lang-unknown` — Icon ❓ · Title "Lingua non riconosciuta" ·
   Message "Non riesco a capire la lingua del libro. Confermala
   manualmente." · UI inline: dropdown con 5 lingue (EN, FR, DE, ES, IT)
   · CTA primary "Conferma e ritraduci"

5. `network-mid-ocr` — Icon 📡 · Title "Connessione persa" · Message
   "Foto salvata in locale. Riprova quando torni online." · banner
   persistent · CTA primary "Riprova ora"

6. `quota-exhausted-mid-stream` — Icon ⚡ · Title "Token esauriti" ·
   Message "La traduzione AI non è disponibile. Hai il testo OCR già
   pronto." · Bridge a quota-credits overlay · CTA primary "Esplora
   piani" · CTA secondary "Usa solo testo OCR" (no token aggiuntivi)

Aggiungi commento HTML head per ogni cellula con:
- trigger condition (precisa)
- recovery action (cosa fa l'utente)
- fallback path (cosa succede se recovery fail)

Light + dark mode (usa CSS variabili già definite in tokens.css).
Mobile + desktop (mantieni grid responsive esistente).
```

- [ ] **Step 1.3: User executes Claude Design Web**

Engineer pause. User esegue brief, riceve HTML output integrato nel file
esistente.

- [ ] **Step 1.4: Integrate HTML output**

Engineer usa Edit tool per inserire le 6 nuove cellule prima della chiusura
del grid container. Verifica preservazione delle 4 cellule esistenti.

- [ ] **Step 1.5: Validate AC via grep**

Run:
```bash
grep -c "ocr-low-confidence\|photo-blur-pre-ocr\|translation-timeout\|source-lang-unknown\|network-mid-ocr\|quota-exhausted-mid-stream" admin-mockups/design_files/nanolith-runthrough-error-states.html
```

Expected: `6` (un match per stato).

```bash
grep -c "stream-timeout\|OCR-fail\|LLM-503\|segmentation-fail" admin-mockups/design_files/nanolith-runthrough-error-states.html
```

Expected: `4` (preservazione cellule esistenti).

- [ ] **Step 1.6: Visual review**

Aprire `admin-mockups/design_files/nanolith-runthrough-error-states.html` nel browser. Verificare:
- 10 cellule totali visibili (4 esistenti + 6 nuove)
- Light + dark toggle funziona su tutte
- Mobile 375 + desktop 1280 (resize browser)
- Ogni cellula ha icon + message + CTA primary (+ secondary dove applicabile)

- [ ] **Step 1.7: Commit**

```bash
git add admin-mockups/design_files/nanolith-runthrough-error-states.html
git commit -m "feat(mockup): sezione 1a — 6 nuovi stati OCR/AI in error-states (#491-follow-up)"
```

---

## Task 2: Sezione 1b — `nanolith-runthrough-translate-viewer.html` (+ loading multi-step + reader mode + wake-lock)

**Files:**
- Modify: `admin-mockups/design_files/nanolith-runthrough-translate-viewer.html`

- [ ] **Step 2.1: Read existing file**

Run via Read tool: `admin-mockups/design_files/nanolith-runthrough-translate-viewer.html`

Identificare:
- Layout principale (header, body translate, footer)
- Stato attuale "happy-path" (foto + paragrafo tradotto)
- Token CSS usati

- [ ] **Step 2.2: Prepare Claude Design Web brief**

```
Contesto: estendo il mockup esistente
`admin-mockups/design_files/nanolith-runthrough-translate-viewer.html` con:
A) Loading sequence multi-step
B) Reader mode toggle
C) Wake-lock indicator
D) Contrasto AAA-target sul paragrafo

A) LOADING SEQUENCE (4 step nominati, label IT):

   1. "Caricamento foto..."     (0-2s)  — linear progress bar + thumbnail
   2. "Sto leggendo il libro..." (2-7s) — skeleton paragrafo 3-righe +
                                          shimmer-sweep gradient
   3. "Sto traducendo..."        (7-15s) — paragrafo OCR @ 60% opacity sopra
                                          + overlay tradotto token-by-token
   4. "Cerco parole nel glossario..." (15-17s) — highlight progressivo
                                                 parole nuove (badge ⊕)

   Pattern: ogni step ha un'icon spinner + label IT + percentuale (solo step 1)
   o "Sto..." progressivo (step 2-4).

   Abort CTA visibile dal step 2 in poi (FAB bottom-right mobile, top-right
   desktop). Click abort → torna a camera step.

   Skeleton size: 3 righe @ 16pt body (default font size). Se reader mode
   attivo (vedi B), scala a 24pt.

   `prefers-reduced-motion` → fallback statico (no shimmer).

B) READER MODE TOGGLE:

   Pulsante 📖 nel header (top-right, accanto a hamburger menu se esiste).
   Click → switch font body 16pt → 24pt, line-height +30%, padding maggiore
   sul container del paragrafo.

   Stato persistito in `localStorage` con key `reader-mode-enabled` (boolean).

   Quando attivo, applicare la scala anche al skeleton (step 2 del loading).

   Visivamente nel mockup: mostrare 2 varianti side-by-side o tab toggle:
   - "Default mode": body 16pt
   - "Reader mode": body 24pt, paragrafo centrato max-width 65ch

C) WAKE-LOCK INDICATOR:

   Badge 🔆 *"Schermo attivo"* in bottom-right (sticky), color subtle
   (`--c-muted-fg`). Click → disable wake-lock con toast feedback "Schermo
   torna alla normalità".

   Quando wake-lock disabilitato, badge cambia in 🔅 *"Auto-blocco attivo"*
   (stesso click toggle).

   NOTA: per browser senza Web Wake Lock API support (Safari < 16.4),
   mostrare variant del badge: 🔆 *"Non supportato"* con tooltip
   "Disattiva auto-blocco nelle impostazioni del telefono."

D) CONTRASTO AAA-TARGET:

   Paragrafo tradotto (main content) usa coppia colore con contrast ratio
   ≥7:1 (vs AA standard 4.5:1). Centrato, max-width 65ch. Usa CSS variable
   `--c-text-high-contrast` (definire in tokens.css fallback se non esiste).

Variants viewport:
- Mobile 375: step list verticale stack, paragrafo sotto, abort FAB
  bottom-right, reader toggle in hamburger menu
- Desktop 1280: step list orizzontale top breadcrumb, paragrafo central,
  abort top-right, reader toggle visibile in header

Latency targets (commenti HTML head):
- Step 1 < 2s · Step 2 < 5s · Step 3 < 10s · Step 4 < 2s · Total < 17s
- Hard timeout 20s totale → trigger `translation-timeout` (vedi
  nanolith-runthrough-error-states.html)

Light + dark mode. Preservare stato happy-path attuale (foto + paragrafo
tradotto) come "step finale dopo loading".
```

- [ ] **Step 2.3: User executes Claude Design Web**

Engineer pause.

- [ ] **Step 2.4: Integrate HTML output**

Edit tool per integrare: loading sequence + reader mode + wake-lock +
contrasto AAA. Preservare happy-path esistente.

- [ ] **Step 2.5: Validate AC via grep**

Run:
```bash
grep -c "Caricamento foto\|Sto leggendo\|Sto traducendo\|Cerco parole" admin-mockups/design_files/nanolith-runthrough-translate-viewer.html
```

Expected: `4` (un match per label step).

```bash
grep -c "reader-mode-enabled\|wake-lock\|max-width.*65ch" admin-mockups/design_files/nanolith-runthrough-translate-viewer.html
```

Expected: `≥3` (almeno un match per ognuno dei 3 pattern).

```bash
grep "prefers-reduced-motion" admin-mockups/design_files/nanolith-runthrough-translate-viewer.html
```

Expected: almeno 1 occorrenza.

- [ ] **Step 2.6: Visual review**

Browser apre file. Verificare:
- Loading 4 step visibili (mostrati progressivamente o in tab/state switch)
- Reader mode toggle funziona (font cambia 16pt ↔ 24pt)
- Wake-lock badge presente
- Light + dark + mobile + desktop tutti coerenti

- [ ] **Step 2.7: Commit**

```bash
git add admin-mockups/design_files/nanolith-runthrough-translate-viewer.html
git commit -m "feat(mockup): sezione 1b — loading multi-step + reader mode + wake-lock in translate-viewer"
```

---

## Task 3: Sezione 1c — `nanolith-runthrough-glossary-editor.html` + jsx twin (+ 4 sub-screen)

**Files:**
- Modify: `admin-mockups/design_files/nanolith-runthrough-glossary-editor.html`
- Modify: `admin-mockups/design_files/sp6-libro-game-glossary-editor.jsx`

- [ ] **Step 3.1: Read existing files**

Run via Read tool su entrambi:
- `admin-mockups/design_files/nanolith-runthrough-glossary-editor.html`
- `admin-mockups/design_files/sp6-libro-game-glossary-editor.jsx`

Identificare: layout glossary index attuale, edit/add patterns.

- [ ] **Step 3.2: Prepare Claude Design Web brief**

```
Contesto: estendo il mockup esistente
`admin-mockups/design_files/nanolith-runthrough-glossary-editor.html` (e il
twin jsx `sp6-libro-game-glossary-editor.jsx`) per supportare context-aware
glossary entries.

MODELLO DATI (da inserire come commento HTML head + commento jsx top):

GlossaryEntry {
  term: "sentinel"
  base_definition: "soldato di guardia"          ← primary
  contexts: [
    { book_id: "press-start", definition: null,  ← uses base
      first_seen: paragraph_id_X },
    { book_id: "rules-game", definition: "punto di osservazione strategica",
      first_seen: paragraph_id_Y }
  ]
}

AGGIUNGI 4 SUB-SCREEN:

1. `conflict-dialog` (Modal):
   Trigger: OCR trova parola già nel glossario con
   `context_similarity < 0.85`.
   UI: Modal con title "La parola 'sentinel' è già nel glossario" + body
   mostra entrambe le definizioni side-by-side (esistente + nuova) +
   3 CTA radio:
   ① "Mantieni esistente" (default, soft-CTA)
   ② "Sostituisci"
   ③ "Aggiungi variante per Rules Game"
   + footer CTA "Conferma" / "Annulla".

2. `bulk-import-card` (Inline card):
   Trigger: OCR trova ≥3 parole nuove non-glossario in un paragrafo.
   UI: Card inline sotto la traduzione (translate-viewer page) con title
   "5 nuove parole rilevate" + chip preview dei 5 termini + 2 CTA:
   ① "Aggiungi tutte" (1-click bulk save)
   ② "Rivedi" → apre modal multi-select con per-term checkbox
              + edit definition inline.

3. `variant-expansion` (Row expand):
   Trigger: Click su term row con `contexts.length > 1`.
   UI: Row glossary index espande mostrando N varianti, ognuna con:
   - book badge "📖 Press Start" / "📖 Rules Game"
   - definition specifica (o "(usa base)" se context.definition is null)
   - first_seen link "Vedi paragrafo originale"
   - delete variant button
   Collapse by default. Click row → expand. ChevronDown icon che ruota.

4. `filter-strip + context-badges` (Sempre visibile in glossary index):
   Header glossary index con:
   - search input "Cerca per termine, definizione, libro..."
   - filter by book: chip multi-select con tutti i book della collection
   - filter toggle "Solo con varianti" (boolean)
   Ogni term row mostra context badges inline (📖 Press Start +
   📖 Rules Game) accanto al term name.

PATTERN DECISIONS (commenti HTML):
- Default conflict resolution: auto-keep esistente se
  `context_similarity ≥ 0.85` (silenzioso); mostra dialog solo `< 0.85`.
- Context similarity: server-side via embedding cosine (riusa
  `IEmbeddingService`); threshold `0.85`.
- Bulk import trigger: `≥3` parole, non `≥1`.
- Variant deletion: se elimina l'ultima variante non-base, term torna
  single-context.

DECISIONE ARCHITETTURALE INCIDENTALE (commento HTML head):
"Il modello GlossaryEntry con contexts[] richiede schema migration backend
su tabella glossary_entries. Spec backend separata, non in questo mockup."

Light + dark + mobile + desktop. Sync .jsx twin con HTML modificato.
```

- [ ] **Step 3.3: User executes Claude Design Web (HTML + jsx)**

Engineer pause. User esegue brief 2 volte (HTML + jsx) o una volta con output
diviso.

- [ ] **Step 3.4: Integrate HTML output**

Edit tool su `nanolith-runthrough-glossary-editor.html` per aggiungere 4
sub-screen + commento modello dati.

- [ ] **Step 3.5: Integrate jsx output**

Edit tool su `sp6-libro-game-glossary-editor.jsx` per sync.

- [ ] **Step 3.6: Validate AC via grep**

Run:
```bash
grep -c "conflict-dialog\|bulk-import-card\|variant-expansion\|filter-strip" admin-mockups/design_files/nanolith-runthrough-glossary-editor.html
```

Expected: `≥4`.

```bash
grep "context_similarity\|0.85\|contexts:" admin-mockups/design_files/nanolith-runthrough-glossary-editor.html
```

Expected: ≥3 occorrenze.

```bash
grep "GlossaryEntry\|contexts" admin-mockups/design_files/sp6-libro-game-glossary-editor.jsx
```

Expected: ≥1 occorrenza in jsx.

- [ ] **Step 3.7: Visual review**

Browser apre HTML. Verifica:
- 4 sub-screen presenti
- Conflict dialog mostra 3 CTA radio
- Bulk import card mostra chip preview
- Variant expansion ha chevron + book badges
- Filter strip presente

- [ ] **Step 3.8: Commit**

```bash
git add admin-mockups/design_files/nanolith-runthrough-glossary-editor.html admin-mockups/design_files/sp6-libro-game-glossary-editor.jsx
git commit -m "feat(mockup): sezione 1c — glossary context-aware (conflict/merge/variants) + jsx sync"
```

---

## Task 4: Sezione 1d — `translate-viewer.html` (multi-lang) + `photo-upload.html` (entry point manual) + jsx twin

**Files:**
- Modify: `admin-mockups/design_files/nanolith-runthrough-translate-viewer.html` (estende Task 2)
- Modify: `admin-mockups/design_files/sp6-libro-game-photo-upload.html`
- Modify: `admin-mockups/design_files/sp6-libro-game-translation-viewer.jsx`

- [ ] **Step 4.1: Read existing files**

Run via Read tool su:
- `admin-mockups/design_files/nanolith-runthrough-translate-viewer.html` (post Task 2)
- `admin-mockups/design_files/sp6-libro-game-photo-upload.html`
- `admin-mockups/design_files/sp6-libro-game-translation-viewer.jsx`

- [ ] **Step 4.2: Prepare Claude Design Web brief**

```
Contesto: estendo `nanolith-runthrough-translate-viewer.html` (post Task 2
con loading sequence) aggiungendo support per multi-language source +
manual input fallback. Estendo anche `sp6-libro-game-photo-upload.html`
con entry-point al manual mode.

AGGIUNGI 3 SUB-SCREEN AL translate-viewer.html:

1. `lang-detection-badge` (Header pill):
   Trigger: post OCR step 2 completato.
   UI: pill nel header top-right "Sorgente: EN · Conferma o cambia" con
   icon 🌐. Color: `--c-info` background, `--c-info-fg` text.
   Behavior:
   - confidence > 0.8 → badge informativo (no friction)
   - confidence 0.5-0.8 → badge tap-to-confirm prima di translate
   - confidence < 0.5 → trigger source-lang-unknown error state
   Click → apre lang-override-modal (sub-screen 2).

2. `lang-override-modal` (Modal):
   Trigger: Tap sul lang-detection-badge.
   UI: Modal con title "Lingua sorgente del libro" + radio group 5 lingue:
   - 🇬🇧 English (EN)
   - 🇫🇷 Français (FR)
   - 🇩🇪 Deutsch (DE)
   - 🇪🇸 Español (ES)
   - 🇮🇹 Italiano (IT)
   + CTA primary "Conferma e ritraduci" + secondary "Annulla".
   Re-trigger AI translation step 3 (skip step 2 OCR cache hit).
   Pre-selected: lingua auto-detected.

3. `manual-input-mode` (Layout variant):
   Trigger: query param `?mode=manual` (deeplink/share) OR triple-entry
   discoverability (vedi sotto).
   UI: layout translate-viewer SENZA foto thumbnail. Componenti:
   - Header sticky con title "Inserimento manuale" + back button + lang
     dropdown pre-filled last-used
   - Textarea multiline (max 2000 char + counter visibile bottom-right)
   - Book ref pre-filled current campaign book (read-only chip)
   - CTA submit "Traduci" → skip OCR, va direttamente a step 3 (AI
     translation) della loading sequence del Task 2

TRIPLE-ENTRY DISCOVERABILITY (commento HTML head):
1. CTA "Digita manualmente" in error state `photo-blur-pre-ocr` (sezione 1a)
2. Kebab menu ⋮ del camera step (sp6-libro-game-photo-upload.html — vedi
   sotto)
3. Empty state quando /translate aperto senza foto: card "Libro non a
   portata? Digita il paragrafo"

AGGIUNGI AL `sp6-libro-game-photo-upload.html`:

- Kebab menu ⋮ top-right nel camera UI con voce:
  "Digita manualmente" → naviga a `/translate?mode=manual`

PATTERN DECISIONS (commenti HTML):
- Target lang fixed IT v1 (target dropdown NON visibile)
- 5 lingue v1 (EN, FR, DE, ES, IT) copre ~95% libri-game europei
- Manual mode parity: stessi step 3-4 della loading sequence; unica diff =
  step 2 OCR sostituito da textarea
- `source_method: "ocr" | "manual"` tracked nello stato (commento)
- Override re-translate: lang change preserva OCR result (cache hit) —
  re-translate solo step AI
- `?mode=manual` query param vs sub-route: scelto query param (UI-only,
  no backend route distinta)

Sync `sp6-libro-game-translation-viewer.jsx` con i cambi HTML.

Light + dark + mobile + desktop.
```

- [ ] **Step 4.3: User executes Claude Design Web**

Engineer pause.

- [ ] **Step 4.4: Integrate HTML/JSX output**

3 file da modificare:
- `nanolith-runthrough-translate-viewer.html` (+ 3 sub-screen)
- `sp6-libro-game-photo-upload.html` (+ kebab menu)
- `sp6-libro-game-translation-viewer.jsx` (sync)

- [ ] **Step 4.5: Validate AC via grep**

Run:
```bash
grep -c "lang-detection-badge\|lang-override-modal\|manual-input-mode" admin-mockups/design_files/nanolith-runthrough-translate-viewer.html
```

Expected: `≥3`.

```bash
grep -c "EN\|FR\|DE\|ES\|IT" admin-mockups/design_files/nanolith-runthrough-translate-viewer.html
```

Expected: ≥5 occorrenze.

```bash
grep "?mode=manual\|mode=manual" admin-mockups/design_files/sp6-libro-game-photo-upload.html
```

Expected: ≥1.

```bash
grep "Digita manualmente" admin-mockups/design_files/sp6-libro-game-photo-upload.html
```

Expected: 1 match.

- [ ] **Step 4.6: Visual review**

Browser su entrambi i file. Verifica:
- translate-viewer: 3 sub-screen presenti (badge + modal + manual layout)
- photo-upload: kebab menu con "Digita manualmente"

- [ ] **Step 4.7: Commit**

```bash
git add admin-mockups/design_files/nanolith-runthrough-translate-viewer.html admin-mockups/design_files/sp6-libro-game-photo-upload.html admin-mockups/design_files/sp6-libro-game-translation-viewer.jsx
git commit -m "feat(mockup): sezione 1d — multi-lang source + manual input fallback + jsx sync"
```

---

## Task 5: Sezione 2 — Creare `chat-fullscreen.html` + jsx (B11b carry-over #491)

**Files:**
- Create: `admin-mockups/design_files/chat-fullscreen.html`
- Create: `admin-mockups/design_files/chat-fullscreen.jsx`

- [ ] **Step 5.1: Read reference files**

Run via Read tool su:
- `admin-mockups/design_files/nanolith-nav-chat-panel.html` (slide-over, riferimento)
- `admin-mockups/design_files/sp4-citation-pdf-viewer.html` (citation overlay riuso)
- `admin-mockups/design_files/sp4-agent-detail.html` (agent mini-card riuso)
- `admin-mockups/design_files/components.css` (CSS shared)

- [ ] **Step 5.2: Prepare Claude Design Web brief**

```
Contesto: creo un NUOVO mockup `admin-mockups/design_files/chat-fullscreen.html`
(+ twin jsx) per la chat full-screen page-level. NON estendere
nanolith-nav-chat-panel.html (che resta component-mock slide-over per
cross-page quick-access).

Route impattate: /chat/[threadId] (page principale) · /chat/new (empty state)

3 SCREEN DA CREARE:

SCREEN B11b-1: Desktop 3-col (1280px)

Layout:
- Sidebar sx (260px):
  • Tabs orizzontali: Tutti / Preferiti / Archiviati
  • Search input "Cerca conversazioni..."
  • Filter by agent: chip dropdown
  • CTA "+ Nuova chat" prominente
  • Thread list: avatar + title + last-msg-preview + timestamp + unread
    badge

- Main center (~720px):
  • Header: thread title + agent avatar + actions (star, archive, delete
    kebab menu)
  • Chat stream bubble (user-right, agent-left con avatar)
  • Citations pills inline ai messaggi agent (style: pill rounded, color
    `--c-info-soft`, click apre PDF overlay)
  • Typing indicator (3 dots animate) durante stream
  • Cancel CTA visibile durante stream attivo
  • Composer bottom: textarea multiline autoresize (1-row default,
    expand-on-content) + attachment button (paperclip) + voice button
    (microphone, placeholder) + send button (paper plane)

- Sidebar dx (260px):
  • Agent info mini-card (riuso pattern sp4-agent-detail mini)
  • Sezione "Fonti citate" — list cards delle citation referenced nei
    msgs del thread (click → PDF overlay)
  • Sezione "Azioni suggerite" — CTA contestuali (es. "Apri il libro
    a pag 42", "Crea play record", ecc.)

SCREEN B11b-2: Mobile (375)

Layout single-pane:
- Header sticky: ← back + thread title + agent avatar + kebab menu ⋮
- Message stream scroll fluido
- Bottom composer fixed (autoresize 1-row default)
- Tap header → bottom sheet con context panel (agent + sources + actions)
- Tap citation pill → PDF viewer overlay full-screen

SCREEN B11b-3: Empty state (/chat/new)

Hero centrato:
- Icon 💬 grande
- Title "Inizia una conversazione"
- Subtitle "Chiedi al tuo agent una regola, una strategia o un riassunto."
- 4 quick-starter cards (grid 2x2 mobile, 1x4 desktop) — VARIANTI:

  Variant LIBRO-GAME (Aaron):
  • "Quali sono le regole di Press Start?"
  • "Spiega questa Encounter"
  • "Riassumi paragrafo 42"
  • "Lista personaggi nella campagna"

  Variant BOARDGAMER:
  • "Setup iniziale"
  • "Regole edge case"
  • "Strategie comuni"
  • "FAQ del gioco"

  Variant DEFAULT (no agent):
  • CTA "Scegli un agent" + dropdown picker prominent (lista agent
    disponibili con avatar + name + last-used)

PATTERN DECISIONS (commenti HTML):
- Slide-over vs full-screen routing: automatico per viewport
  • Mobile/tablet (<1024px) → full-screen sempre (slide-over diventa
    drawer fallback)
  • Desktop (≥1024px) → slide-over default, toggle "View as full-screen"
    nel slide-over header per immersive mode
- Streaming token-by-token con AbortController (riusa useThreadMessages
  pattern PR#551)
- Citations: pill click → sp4-citation-pdf-viewer overlay (NO fork)
- Attachment Aaron-specific: foto inline apre flow translate come
  quick-action; risposta translate ritorna come citation pill nella chat
- Voice: UI placeholder, backend Whisper integration deferita
- Quick-starter selection: agent type da agent.metadata.category
  (verificare esistenza field; fallback graceful a "Default" se mancante)

READER MODE + WAKE-LOCK (per passive co-player):
- Stesso toggle 📖 della sezione 1b (translate viewer) — applicabile a
  bubble dei messaggi (font 16pt → 24pt)
- Stessa `localStorage` key (`reader-mode-enabled`) per coerenza
  cross-route Aaron
- Wake-lock 🔆 badge attivo durante chat con streaming attivo

STATI DI STREAM (mostra tutti e 4 nel mockup):
- `idle` — empty composer
- `streaming` — token-by-token + cancel CTA visibile
- `stream-error` — banner "Errore stream — riprova" + last user message
  conservato in composer (no perdita)
- `stream-abort` — utente click cancel → message preservato come
  "[interrotto]" badge

Crea anche twin jsx con stesso layout React-style.

Light + dark + mobile + desktop. Riuso pattern: nanolith-nav-chat-panel
(slide-over secondary) · sp4-citation-pdf-viewer (overlay) ·
sp4-agent-detail (mini agent card).
```

- [ ] **Step 5.3: User executes Claude Design Web**

Engineer pause.

- [ ] **Step 5.4: Integrate HTML output**

Write tool per creare `admin-mockups/design_files/chat-fullscreen.html`.

- [ ] **Step 5.5: Integrate jsx output**

Write tool per creare `admin-mockups/design_files/chat-fullscreen.jsx`.

- [ ] **Step 5.6: Validate AC via grep**

Run:
```bash
grep -c "Desktop 3-col\|Mobile\|Empty state\|/chat/new\|/chat/\[threadId\]" admin-mockups/design_files/chat-fullscreen.html
```

Expected: ≥3 (almeno una occorrenza per dimensione).

```bash
grep -c "idle\|streaming\|stream-error\|stream-abort" admin-mockups/design_files/chat-fullscreen.html
```

Expected: `4` (un match per stato).

```bash
grep "reader-mode-enabled\|wake-lock" admin-mockups/design_files/chat-fullscreen.html
```

Expected: ≥2 occorrenze.

```bash
grep "Libro-game\|Boardgamer\|Default" admin-mockups/design_files/chat-fullscreen.html
```

Expected: ≥3 (3 varianti quick-starter).

- [ ] **Step 5.7: Visual review**

Browser apre file. Verifica:
- 3 screen visibili (desktop 3-col, mobile, empty state)
- Stream states 4 mostrati
- Reader mode + wake-lock visible
- Quick-starter 3 varianti

- [ ] **Step 5.8: Commit**

```bash
git add admin-mockups/design_files/chat-fullscreen.html admin-mockups/design_files/chat-fullscreen.jsx
git commit -m "feat(mockup): sezione 2 (B11b) — chat-fullscreen page-mock + jsx (closes #491 partial)"
```

---

## Task 6: Sezione 3 — Creare `state-matrix.html` (B18)

**Files:**
- Create: `admin-mockups/design_files/state-matrix.html`

- [ ] **Step 6.1: Read reference file**

Run via Read tool su:
- `admin-mockups/design_files/01-screens.html` (riferimento dev-fixture matrix
  phone-frame grid)
- `admin-mockups/design_files/nanolith-runthrough-error-states.html` (post
  Task 1, riferimento pattern errors)

- [ ] **Step 6.2: Prepare Claude Design Web brief**

```
Contesto: creo NUOVO mockup `admin-mockups/design_files/state-matrix.html`
come dev-fixture (stile 01-screens.html — phone-frame matrix grid).
Asse verticale: 8 route critiche · asse orizzontale: 5 stati. Totale
40 cell (alcune `—` = skip).

Layout: grid 8 rows x 6 cols (1 col header + 5 cols stati).
Mobile-only viewport (375 phone-frame per cell, come 01-screens.html).

ROUTE × STATI (riempi ogni cell con phone-frame mockup):

| Route                                              | Empty | Error | Loading | Permission | Offline |
|----------------------------------------------------|-------|-------|---------|------------|---------|
| /library/[gameId]/play/[campaignId]/translate      | A1    | A2    | A3      | A4         | A5      |
| /library/[gameId]/play                             | B1    | B2    | B3      | —          | B5      |
| /chat/[threadId]                                   | C1    | C2    | C3      | C4         | C5      |
| /game-nights                                       | D1    | D2    | D3      | —          | D5      |
| /sessions/live/[sessionId]                         | E1    | E2    | E3      | E4         | E5      |
| /shared-games                                      | F1    | F2    | F3      | —          | F5      |
| /discover                                          | G1    | G2    | G3      | —          | G5      |
| /notifications                                     | H1    | H2    | H3      | —          | H5      |

Contenuto per cell (testi IT, no placeholder generici):

A1 (translate empty): icon 📷 + "Fotografa una pagina" + CTA "Apri camera"
A2 (translate error): riusa pattern nanolith-runthrough-error-states ocr-fail
A3 (translate loading): 4-step skeleton (riusa Task 2 sezione 1b)
A4 (translate permission): 🔒 + "Quota OCR esaurita" + tier badge "Pro" +
   CTA "Esplora piani"
A5 (translate offline): banner 📡 + last-translation cached @ 80% opacity

B1 (play empty): icon 📖 + "Nessuna campagna" + CTA "Sfoglia libreria"
B2 (play error): "Errore caricamento — riprova" + retry button
B3 (play loading): 3 campaign card skeleton
B5 (play offline): banner + campagne cached visibili

C1 (chat empty): icon 💬 + "Inizia conversazione" + quick-starters
   (riusa sezione 2 / Task 5 variant libro-game)
C2 (chat error): banner "Errore stream — riprova"
C3 (chat loading): typing indicator + skeleton bubble
C4 (chat permission): 🔒 + "Quota chat esaurita" + tier badge + CTA
C5 (chat offline): banner + msg history cached

D1 (game-nights empty): icon 🎲 + "Crea prima serata" + CTA "+ Nuova
   serata"
D2 (game-nights error): "Calendar non disponibile"
D3 (game-nights loading): calendar skeleton 7-day grid
D5 (game-nights offline): banner + serate cached

E1 (sessions-live empty): icon ⏳ + "Aspetta che host inizi"
E2 (sessions-live error): SSE disconnect banner + "Riconnessione..."
E3 (sessions-live loading): spinner + game cover thumbnail
E4 (sessions-live permission): 🔒 + "Solo l'host può iniziare — aspetta
   che la sessione si attivi"
E5 (sessions-live offline): banner — modalità local-only

F1 (shared-games empty): icon 📚 + "Catalog vuoto — riprova" (improbabile)
F2 (shared-games error): "Catalogo non disponibile"
F3 (shared-games loading): card-grid 6-skeleton
F5 (shared-games offline): banner + cached results

G1 (discover empty): icon 🧭 + "Nessun consiglio ora"
G2 (discover error): "Discovery offline"
G3 (discover loading): rail skeleton 3-row
G5 (discover offline): banner + cached

H1 (notifications empty): icon 🎉 + "Sei in pari!"
H2 (notifications error): "Errore caricamento notifiche"
H3 (notifications loading): list skeleton
H5 (notifications offline): banner + cached

PATTERN UNIFICATI (riusa nello stesso file):

Empty: icon 96px centrale + gradient bg entity color (color depende dalla
       route, mapping: translate/play → orange Aaron · chat → purple ·
       game-nights/sessions → rose · shared-games/discover → teal ·
       notifications → blue) + tagline IT + primary CTA per uscire.

Error: riuso pattern nanolith-runthrough-error-states (icon + message
       umano + retry + report-bug). Annota in commento HTML head
       `<!-- error: see nanolith-runthrough-error-states -->`.

Loading skeleton: 3-row card dimensionato al contenuto reale. Shimmer-sweep
                  gradient. `prefers-reduced-motion` → fallback statico.

Permission-denied: 🔒 lock + tier badge "Pro"/"Premium" + tagline
                   "Disponibile nei piani Pro/Premium" + primary CTA
                   "Esplora piani" → /pricing (route esistente ma senza
                   mockup canonico — vedi audit gap; link sarà dead-end
                   visivo) + secondary "Torna indietro".

Offline: banner top persistent (📡 + "Offline — sto usando dati salvati")
         + cache-stale data sotto a 80% opacity + retry inline nel banner.

LIGHT/DARK TOGGLE: button top destro inline (no 2 file paralleli).

ILLUSTRATION ECONOMY: ~5 illustration uniche riusabili (tema):
- "book": translate+play
- "speak": chat+notifications
- "calendar": game-nights+sessions-live
- "compass": shared-games+discover

REUSE TRACE: commento HTML head per ogni pattern che linka all'origine
(es. `<!-- pattern: see nanolith-runthrough-translate-viewer.html -->`).
```

- [ ] **Step 6.3: User executes Claude Design Web**

Engineer pause. Output sarà denso (40 cell phone-frame).

- [ ] **Step 6.4: Integrate HTML output**

Write tool per creare `admin-mockups/design_files/state-matrix.html`.

- [ ] **Step 6.5: Validate AC via grep**

Run:
```bash
grep -c "phone-frame" admin-mockups/design_files/state-matrix.html
```

Expected: `≥40` (1 per cell).

```bash
grep -c "Empty\|Error\|Loading\|Permission\|Offline" admin-mockups/design_files/state-matrix.html
```

Expected: ≥40 (5 stati × 8 route con `—` skip = 35 effettivi + headers).

```bash
grep "translate\|play\|chat\|game-nights\|sessions/live\|shared-games\|discover\|notifications" admin-mockups/design_files/state-matrix.html | wc -l
```

Expected: ≥8 occorrenze (1 per route).

```bash
grep "prefers-reduced-motion" admin-mockups/design_files/state-matrix.html
```

Expected: ≥1 match.

- [ ] **Step 6.6: Visual review**

Browser apre file. Verifica:
- Grid 8×5 visibile (alcune cell `—`)
- Light + dark toggle funziona
- Tutte le tagline sono IT route-specific
- Permission-denied appare solo su 3 route (translate, chat, sessions-live)

- [ ] **Step 6.7: Commit**

```bash
git add admin-mockups/design_files/state-matrix.html
git commit -m "feat(mockup): sezione 3 (B18) — state-matrix cross-route dev-fixture (8x5 grid)"
```

---

## Task 7: Aggiornare `MOCKUPS_INDEX.md`

**Files:**
- Modify: `admin-mockups/MOCKUPS_INDEX.md`

- [ ] **Step 7.1: Read MOCKUPS_INDEX.md attuale**

Run via Read tool: `admin-mockups/MOCKUPS_INDEX.md`

Verificare summary count attuale (riga ~134): page-mock 48, component-mock 16, dev-fixture 10, Total 74.

- [ ] **Step 7.2: Aggiornare summary count**

Edit tool per cambiare la tabella summary:
- page-mock: 48 → **49** (+ `chat-fullscreen.html`)
- component-mock: 16 → **16** (invariato — `error-states`, `glossary-editor`, `translate-viewer`, `photo-upload` sono *modifiche*, non nuovi file)
- dev-fixture: 10 → **11** (+ `state-matrix.html`)
- Total: 74 → **76**

- [ ] **Step 7.3: Aggiungere entry SP6 / Auth & onboarding / dev-fixture sections**

Edit tool su sezioni rilevanti:

**Auth & onboarding** (aggiungere nuova riga se non esiste sezione Chat):
```markdown
| `chat-fullscreen.html` | page-mock | `/chat/[threadId]`, `/chat/new` (empty state) |
```

(in alternativa: creare nuova sezione "Chat" se la struttura lo permette).

**Dev fixtures** (aggiungere riga):
```markdown
| `state-matrix.html` | dev-fixture | State matrix cross-route (8 route × 5 stati) — riusabile per Phase 2/3 |
```

- [ ] **Step 7.4: Aggiornare data "Last updated"**

Edit tool: cambia "Last updated: 2026-05-18" → "Last updated: 2026-05-23"
(riga ~9).

- [ ] **Step 7.5: Validate AC via grep**

Run:
```bash
grep "chat-fullscreen\|state-matrix" admin-mockups/MOCKUPS_INDEX.md
```

Expected: ≥2 occorrenze (1 per file).

```bash
grep "\*\*76\*\*" admin-mockups/MOCKUPS_INDEX.md
```

Expected: 1 match (total aggiornato).

- [ ] **Step 7.6: Commit**

```bash
git add admin-mockups/MOCKUPS_INDEX.md
git commit -m "docs(mockup): aggiorna MOCKUPS_INDEX — +chat-fullscreen +state-matrix (76 total)"
```

---

## Task 8: Aggiornare `v2-migration-matrix.md`

**Files:**
- Modify: `docs/for-developers/frontend/v2-migration-matrix.md`

- [ ] **Step 8.1: Read sezione Route → Mockup index**

Run via Grep:
```bash
grep -n "Route.*Mockup index\|chat/\[threadId\]\|/chat/" docs/for-developers/frontend/v2-migration-matrix.md
```

Identificare sezione e righe da modificare.

- [ ] **Step 8.2: Aggiornare route mapping per /chat/[threadId]**

Edit tool: aggiornare riga esistente o aggiungere riga per
`chat-fullscreen.html` → `/chat/[threadId]`, `/chat/new`.

Pattern proposto (sezione "Chat" o "Communication"):
```markdown
| `chat-fullscreen.html` | page-mock | `/chat/[threadId]`, `/chat/new` | done — PR <questa> | T A M V |
```

- [ ] **Step 8.3: Aggiornare entry state-matrix come dev-fixture**

Edit tool: aggiungere sezione/riga per `state-matrix.html` come dev-fixture
riferimento, route cross-cutting.

- [ ] **Step 8.4: Aggiornare il banner update notes (top del file)**

Edit tool: aggiungere paragrafo "Updated 2026-05-23" simile al pattern
esistente:

```markdown
> **Updated 2026-05-23** (Mockup refinement Aaron core + cross-cutting PR <N>):
> added `chat-fullscreen.html` (page-mock, /chat/[threadId]+/chat/new),
> `state-matrix.html` (dev-fixture, cross-route states), e estese 4 mockup
> esistenti per cluster translate (error-states +6 stati, translate-viewer
> +loading+reader-mode+multi-lang, glossary-editor +context-aware,
> photo-upload +manual-mode entry). Spec:
> `docs/superpowers/specs/2026-05-23-mockup-refinement-aaron-core-design.md`.
```

- [ ] **Step 8.5: Validate AC via grep**

Run:
```bash
grep -c "chat-fullscreen\|state-matrix\|2026-05-23" docs/for-developers/frontend/v2-migration-matrix.md
```

Expected: ≥3 occorrenze.

- [ ] **Step 8.6: Commit**

```bash
git add docs/for-developers/frontend/v2-migration-matrix.md
git commit -m "docs(matrix): v2-migration-matrix +chat-fullscreen +state-matrix routes"
```

---

## Task 9: Aggiornare audit `2026-05-22-mockup-gaps.md`

**Files:**
- Modify: `docs/for-developers/audits/2026-05-22-mockup-gaps.md`

- [ ] **Step 9.1: Read sezione corrente**

Run via Read tool su file completo. Identificare sezioni P0 (#491, #492)
che vanno marcate come "Phase 1 in implementation".

- [ ] **Step 9.2: Aggiungere status banner top del file**

Edit tool: dopo la riga "Total gaps confermati:..." aggiungere:

```markdown
## Status (2026-05-23 update)

**Phase 1 in implementation** (PR <questa>):
- ✅ Closure parziale #491 chat full-screen → mockup `chat-fullscreen.html` creato (Sezione 2)
- ⚠️ Closure false-positive #492 community follow-up → ancora da riaprire
  (sezione B11a non in scope Approccio B di questa Phase 1)
- ✅ Cluster translate Aaron CORE → 4 sezioni 1a/1b/1c/1d implementate
- ✅ B18 state-matrix cross-route → mockup `state-matrix.html` creato (Sezione 3)

**Spec di riferimento**:
`docs/superpowers/specs/2026-05-23-mockup-refinement-aaron-core-design.md`

**Out of scope Phase 1** (deferiti a Phase 2/3):
B11 play-records · B11a community follow-up · B14 editor agent-proposals
(parcheggiato) · B15 toolkit sub-pages · B16 wishlist · B17 sessions
sub-pages · nodi architetturali N4/N6/N7.
```

- [ ] **Step 9.3: Update sezione "Sommario azioni per maintainer"**

Edit tool: marcare con ✅ le voci consegnate, lasciare ⚠️/⏸️ per le
deferite.

Esempio:
```markdown
1. ⚠️ **Riaprire #492** con scope ridotto (4 screen community) → o aprire B11a — STILL TODO
2. ✅ **Riaprire #491** con scope ridotto (3 screen chat full-screen) →  consegnato via `chat-fullscreen.html` (PR <questa>)
...
```

- [ ] **Step 9.4: Validate AC via grep**

Run:
```bash
grep "Phase 1 in implementation\|2026-05-23" docs/for-developers/audits/2026-05-22-mockup-gaps.md
```

Expected: ≥2 occorrenze.

- [ ] **Step 9.5: Commit**

```bash
git add docs/for-developers/audits/2026-05-22-mockup-gaps.md
git commit -m "docs(audit): mockup-gaps Phase 1 in implementation (Aaron core consegnato)"
```

---

## Task 10: Push + Pull Request

**Files:**
- No file changes

- [ ] **Step 10.1: Push tutti i commit**

Run:
```bash
git push origin feature/mockup-refinement-aaron-design
```

- [ ] **Step 10.2: Verifica diff completo prima di PR**

Run:
```bash
git diff main-dev...HEAD --stat
git log main-dev..HEAD --oneline
```

Expected: ~9 commit visibili, ~10 file modificati/creati totali.

- [ ] **Step 10.3: Crea PR target main-dev**

Run:
```bash
gh pr create --base main-dev --title "feat(mockup): Aaron core refinement + chat-fullscreen + state-matrix (Phase 1)" --body "$(cat <<'EOF'
## Summary

Implementazione Phase 1 del piano di raffinamento mockup secondo l'Approccio B (Aaron core + cross-cutting foundation):

- **Sezione 1a-1d** — cluster translate (Aaron JTBD #1): 4 mockup estesi con failure modes (6 nuovi stati), loading multi-step + reader mode + wake-lock, glossary context-aware, multi-language source + manual fallback
- **Sezione 2 (B11b)** — nuovo `chat-fullscreen.html` (3 screen: desktop 3-col + mobile + empty) — chiude parzialmente #491
- **Sezione 3 (B18)** — nuovo `state-matrix.html` (8 route × 5 stati = 40 cell, foundation per Phase 2/3)
- **Documentazione** — MOCKUPS_INDEX +2 entry, v2-migration-matrix aggiornata, audit `2026-05-22-mockup-gaps` marcato Phase 1 in implementation

## Spec di riferimento

`docs/superpowers/specs/2026-05-23-mockup-refinement-aaron-core-design.md`

## Out of scope

B11 play-records · B11a community follow-up · B14 editor agent-proposals · B15 toolkit sub-pages · B16 wishlist · B17 sessions sub-pages — deferiti a Phase 2/3.

## Test plan

- [ ] Visual review mockup nel browser: light + dark + mobile 375 + desktop 1280
- [ ] Verifica `prefers-reduced-motion` rispettato (skeleton shimmer fallback)
- [ ] Verifica reader mode toggle persistito in localStorage
- [ ] Verifica wake-lock indicator graceful degradation Safari <16.4
- [ ] Verifica contrasto AAA (≥7:1) sui paragrafi tradotti (axe / DevTools)
- [ ] Verifica reference cross-linking nei commenti HTML head

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 10.4: Verifica CI checks**

Aspetta che GitHub Actions completi. Se fail, investigare e fixare prima del merge.

- [ ] **Step 10.5: Stop here**

Plan termina. Merge della PR e cleanup branch sono al di fuori dello scope (decisione user).

---

## Self-Review checklist (post-plan)

Da eseguire dopo la scrittura del plan:

- [ ] **Spec coverage**: ogni sezione spec (1a/1b/1c/1d/2/3/8/9/10) ha task corrispondente?
  - Sezione 1a → Task 1 ✓
  - Sezione 1b → Task 2 ✓
  - Sezione 1c → Task 3 ✓
  - Sezione 1d → Task 4 ✓
  - Sezione 2 (B11b) → Task 5 ✓
  - Sezione 3 (B18) → Task 6 ✓
  - Sezione 9 (AC globali) → Task 7+8+9 ✓
  - Sezione 10 (decisioni archi) → coperte come commenti HTML nei brief
- [ ] **Placeholder scan**: nessun TBD/TODO/"add appropriate" nei step?
- [ ] **Type consistency**: nomi file, route, label coerenti cross-task?
- [ ] **Branching**: Task 0 setup + Task 10 push/PR coerenti con CLAUDE.md project rules (feature branch → main-dev)
