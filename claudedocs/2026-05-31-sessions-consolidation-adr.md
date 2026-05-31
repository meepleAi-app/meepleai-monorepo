# ADR — Consolidamento sub-route Sessions in RightColumnTabs

- **Data:** 2026-05-31
- **Stato:** Accettato
- **Scope:** SP4 wave 2 · Sessions cluster
- **Decisione:** Option B+ (hybrid consolidation) — già approvata nel brief
- **Autore:** Claude Design (mockup) → owner FE per implementazione

---

## Contesto

Il cluster Sessions esponeva **8 sub-route** dedicate, ciascuna con il proprio
mockup/route, molte delle quali condividono lo stesso contesto di sessione (stesso
header, stesso socket SSE, stesso roster). Mantenerle separate significa:

- 8 entry-point da navigare, con perdita di contesto a ogni hop;
- riconnessione del socket SSE a ogni cambio route nella partita live;
- duplicazione di header / connection-bar / chrome;
- 8 mockup da mantenere allineati al design system.

## Decisione

Consolidare le route in **tab** dentro il pattern `RightColumnTabs` già presente
in `sp4-session-live.jsx`, **estendendolo** (non reinventandolo), e replicandolo in
forma analoga in `sp4-session-summary.jsx`.

### Mappatura route → tab

**Cluster Live** — `sp4-session-live.jsx`, `RightColumnTabs` esteso
(esistenti `tools · chat · notes` **+** 4 nuove):

| Route eliminata | Nuova tab | Entity color | Contenuto |
|---|---|---|---|
| `/sessions/[id]/scores`  | `scores`  | `session` | Live scoring realtime, leader gap, barre di progresso |
| `/sessions/[id]/photos`  | `photos`  | `kb`      | Galleria in-session, lazy load, label per turno |
| `/sessions/[id]/agent`   | `agent`   | `agent`   | Pannello agente in-sessione (header KB + chat embedded) |
| `/sessions/[id]/players` | `players` | `player`  | Gestione live: mute / score adj / espelli + stato connessione |

**Cluster Post-game review** — `sp4-session-summary.jsx`, nuovo `RightColumnTabs`:

| Route eliminata | Nuova tab | Entity color | Contenuto |
|---|---|---|---|
| `/sessions/[id]/scoreboard` | `scoreboard` | `session` | Podio standalone + classifica con totali, export |
| `/sessions/[id]/notes`      | `notes`      | `kb`      | Editor note post-partita (markdown + preview) |
| `/sessions/[id]/players`    | `players`    | `player`  | Lista giocatori con statistiche storiche |

**Riuso (zero design lavoro):**

| Route | Decisione |
|---|---|
| `/sessions/[id]/join` | **Reuse** `sp3-join.html`. Nessun nuovo mockup. |

### Route da eliminare (definitivo)

```
DELETE /sessions/[id]/scores       → live  ?tab=scores
DELETE /sessions/[id]/photos       → live  ?tab=photos
DELETE /sessions/[id]/agent        → live  ?tab=agent
DELETE /sessions/[id]/players      → live  ?tab=players
DELETE /sessions/[id]/scoreboard   → summary ?tab=scoreboard
DELETE /sessions/[id]/notes        → summary ?tab=notes
DELETE /sessions/[id]/players (pg) → summary ?tab=players
KEEP   /sessions/[id]/join         → reuse sp3-join (invariato)
```

Aggiungere redirect 301/308 dalle vecchie route ai canonici `?tab=` per non
rompere link salvati/condivisi.

## Query param schema (`?tab=`)

Le tab sono **deep-linkabili** e bookmarkabili via query param sul route base:

```
/sessions/[id]/live?tab=scores|photos|agent|players|chat|tools|notes
/sessions/[id]?tab=scoreboard|notes|players
```

Regole:

- **Canonico:** `?tab=<id>`. Valore mancante/ignoto → tab di default
  (`scores` per live, `scoreboard` per summary). Nessun 404.
- Il cambio tab fa `history.replaceState` (non `push`) per non inquinare la
  cronologia con ogni click; il deep-link iniziale resta condivisibile.
- I vecchi path segment-based (`/scores`) ridirigono al param equivalente.
- I param sconosciuti vengono ignorati con fallback al default (forward-compat
  per tab future senza rotture).

## Rationale — preservazione SSE

Il motivo forte della consolidation. Con route separate, ogni navigazione
smontava la pagina live e **riapriva il socket SSE** (riconnessione, refetch dello
stato partita, flash di loading, rischio di eventi persi nella finestra di
riconnessione).

Con le tab:

- Il **socket SSE vive sopra il layer delle tab**, nel contenitore di sessione.
  Cambiare tab è puro switch di stato React — **nessun teardown, nessuna
  riconnessione**, lo stream resta caldo.
- Lo stato live (scores, presenze, eventi) è condiviso: la tab `scores` e la card
  roster nella colonna centrale leggono la **stessa** sorgente realtime.
- Lo switch tab mostra subito gli ultimi dati noti (niente spinner) perché il dato
  è già in memoria.
- Stato **SSE-disconnect** disegnato per ogni tab live: banner `entity-agent` non
  bloccante + auto-retry 5s + retry manuale; il contenuto resta visibile in stato
  *stale* (dim 60%) invece di scomparire.

Post-game (summary) **non ha stream live**: lì lo stato analogo è **offline /
cache** (es. bozza note non sincronizzata, scoreboard da cache locale). Modellato
come 5° stato `offline` per parità di copertura con i requisiti, ma semanticamente
distinto dall'SSE-disconnect della live.

## Rationale — Mobile UX (375 / 768 / 1280)

- **1280 (desktop):** `RightColumnTabs` come rail laterale (380 live / 360 summary),
  strip tab in cima; su live la strip è **scrollabile** perché 7 tab non entrano in
  larghezza fissa. Roster + scoring restano sempre visibili.
- **768 (tablet):** stesso rail, può collassare; le tab restano in strip.
- **375 (mobile):** il rail non sta a fianco. Le tab vivono in un **bottom-sheet**
  drawer — il pattern mobile canonico MeepleAI (vedi README §Drawer). Una strip di
  chip orizzontale scrollabile in basso (live) o un bottone **Review** (summary) apre
  il sheet con handle, header `entity-color`, selettore stato e contenuto.
  - Niente bottom-bar a N icone: 7 voci non ci stanno e l'icon-crowding è anti-pattern.
  - Il sheet preserva il contesto (la surface live sotto resta montata → SSE intatto).
  - Chiusura per tap su backdrop / handle drag / `Esc`.

Rispettato `prefers-reduced-motion` (spring/confetti/shimmer disabilitati).

## Stati coperti per tab

Ogni tab disegna i suoi stati (gallery nei mockup):

| Cluster | Stati |
|---|---|
| Live (`scores/photos/agent/players`) | `default · empty · loading · error · sse` |
| Summary (`scoreboard/notes/players`) | `default · empty · loading · error · offline` |

## Componenti riusati

Da `sp4-sessions-index.jsx` (re-derivati in `sp4-parts-common.jsx` per i mockup):
`OutcomeBadge` · `ScoringInline` · `ConnectionChipStripFooter` · `MeepleCard`.
Pattern esteso, non duplicato: il `RightColumnTabs` live è la fonte; il summary ne
replica la struttura (registry tab + `StateSwitch` + tabpanel).

## Conseguenze

**Positive**
- −7 route, −7 mockup da mantenere; un solo contenitore di sessione per cluster.
- SSE stabile per tutta la partita; UX più fluida, niente flash di riconnessione.
- Deep-link e condivisione preservati via `?tab=`.
- Contesto mai perso: roster/scoring sempre a vista su desktop.

**Negative / rischi**
- `RightColumnTabs` live arriva a 7 tab → serve strip scrollabile e, a regime,
  valutare overflow "•••" se se ne aggiungono altre.
- Tab nascoste non sono indicizzabili da crawler come route distinte (accettabile:
  contenuto post-auth).
- Un solo livello di drawer su mobile (no stacking) — coerente col contratto README.

## Out of scope

- Implementazione FE (issue separata) e modifiche BE.
- Variante A/B sull'ordine delle tab.
- `/sessions/[id]/join` (reuse `sp3-join.html`).

## File prodotti (mockup)

```
design_files/
  sp4-session-live.html / .jsx          (RightColumnTabs esteso: +4 tab)
  sp4-session-live-parts.jsx            (window.LiveSessionParts1)
  sp4-session-live-tabs.jsx             (window.LiveTabs — 4 tab × 5 stati)
  sp4-session-summary.html / .jsx       (nuovo RightColumnTabs: 3 tab)
  sp4-session-summary-parts.jsx         (window.SummaryParts)
  sp4-session-summary-sections.jsx      (corpo celebrativo, invariato)
  sp4-session-summary-tabs.jsx          (window.SummaryReviewTabs — 3 tab × 5 stati)
  sp4-parts-common.jsx                  (entityHsl + data + reuse components)
```

> Nota: i runtime helper originali (`data.js`, `*-parts.jsx`, `sp4-sessions-index.jsx`)
> non erano nel pacchetto fornito; sono stati **re-derivati fedelmente** da
> `tokens.css` / `components.css` e dai file canonici per rendere i mockup eseguibili.
> In integrazione, sostituire i re-derivati con i moduli reali del codebase.
