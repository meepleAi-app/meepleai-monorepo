# Claude Design — TODO pagine mancanti per completare il Design System v1

> **Data creazione**: 2026-04-20
> **Stato attuale**: Gruppo 1 ✅ (5/5 mockup in `admin-mockups/design_files/`). Rimanenti 11 aree.
> **Branch app**: `redesign/v2`

---

## 🔁 Comandi per riprendere il lavoro (quando Claude Design torna disponibile)

Quando potrai ritirare i mockup da Claude Design e rientrare in questa repo, dammi uno di questi comandi:

### Ingest + pianificazione
```
/sc:spec-panel leggi docs/superpowers/claude_design_todo.md e i nuovi mockup che Claude Design ha prodotto in admin-mockups/design_files/. Identifica cosa è stato consegnato, aggiorna MEMORY.md, e proponimi il prossimo plan M7/M8/M9 in base alle priorità.
```

### Avvio migrazione M7 (Gruppo 2)
```
ultrathink Inizia M7 — Gruppo 2 migrazione. Segui il pattern del plan 2026-04-20-m6-gruppo1-migration.md: preflight primitivi, TDD per ogni task, subagent-driven-development, PR su redesign/v2. Inizia dalla pagina con priorità più alta secondo il brief.
```

### Avvio migrazione M8 (Gruppo 3 — opzionale)
```
Procedi con M8 — Gruppo 3 (Editor/Pipeline + Calendar). Plan minimal, solo se i mockup esistono.
```

### Avvio migrazione M9 (Gruppo 4 esteso)
```
ultrathink M9 — Gruppo 4 esteso (Discovery, Threads avanzato, Toolbox builder, Shared games, Admin redesign, Error states). Leggi il brief 2026-04-20-claude-design-group4-extended-brief.md. Subagent-driven-development.
```

### Riprendere M6 se non completo
```
Controlla stato M6 Gruppo 1 sul branch redesign/v2 (git log + docs/superpowers/plans/2026-04-20-m6-gruppo1-migration.md) e riprendi dai task pending. Usa subagent-driven-development.
```

### Dopo merge di M6
```
M6 mergiato su redesign/v2. Aggiorna MEMORY.md, chiudi le issue correlate (se presenti), e proponimi M7.
```

---

## 📋 Pagine da produrre con Claude Design

Per ogni area: priorità, brief di riferimento, prompt pronto da incollare a Claude Design, output atteso.

### Regole universali da ricordare a Claude Design (da mettere sempre in testa al prompt)

> Mantieni la coerenza con `admin-mockups/design_files/` v1:
> - tokens.css HSL entity colors (9: game/player/session/agent/kb/chat/event/toolkit/tool)
> - Warm palette: `--bg: #f7f3ee` light, `--bg: #14100a` dark
> - Typography: Quicksand (display/titoli), Nunito (body), JetBrains Mono (mono/kicker)
> - 4px spacing grid, radius xs→2xl→pill
> - Phone frame 380×780, bottom sheet (vaul-style) come primary disclosure mobile
> - Desktop pattern: Split/Sidebar+Drawer/HeroTabs a scelta per pagina
> - Connection bar con pip per relazioni tra entity
> - Dark mode completa con entity colors più chiari (vedi `tokens.css:150-158`)
> - Per ogni pagina produci: HTML standalone + JSX + eventuali estensioni a components.css + dati in data.js + aggiorna `00-hub.html` + variante dark

---

## Gruppo 2 🟡 Importanti (feature app-specific)

### G2.1 — Game Night creation + edit

**Brief**: `docs/superpowers/specs/2026-04-20-claude-design-missing-pages-brief.md` sezione 2.1
**Output**: `admin-mockups/design_files/game-night.html` + `game-night.jsx`
**Entità primaria**: `event` (color `--c-event`)

**Prompt per Claude Design**:

````
[regole universali del design system]

Crea la pagina "Game Night creation+edit" come `game-night.html` + `game-night.jsx` in `admin-mockups/design_files/`.

Screen richiesti (5):
1. **Game Night list** — calendario mensile + lista prossime + past (toggle view)
2. **Create wizard (3 step)** — data+luogo → giocatori invitati → giochi pianificati. Progress bar top come pattern onboarding.
3. **Edit** — stessi campi con stato corrente, action bar bottom (save/cancel/delete)
4. **Detail** — partecipanti (avatar stack), giochi (card list), diary cross-game timeline, foto gallery (stub 4 placeholder)
5. **Finalize** — summary card riassuntiva con stats (durata totale, MVP, giochi completati)

Design hint:
- Entità primaria: event (--c-event)
- Multi-step progress bar identica a onboarding
- Drag-drop visual per ordinare giochi pianificati (anche solo indicatori visivi)
- Avatar stack partecipanti (fino a 5 + "+N" per overflow)
- Connection bar: event → [player, game, session] con pip count

Entità collegate (connection bar): event primario, player + game + session secondarie.

Consegna: HTML standalone + JSX + variante dark + update 00-hub.html con sezione "Gruppo 2 Importanti" contenente link a questa pagina.
````

---

### G2.2 — Play records / history

**Brief**: sezione 2.2
**Output**: `admin-mockups/design_files/play-records.html` + `play-records.jsx`
**Entità primaria**: `session`

**Prompt per Claude Design**:

````
[regole universali]

Crea "Play records / history" come `play-records.html` + `play-records.jsx`.

Screen richiesti (4):
1. **Records list** — filtri topbar (game / player / date range) + lista card responsive (table desktop, card mobile)
2. **Record detail** — punteggi finali (leaderboard style), durata, MVP con badge, note testuali
3. **Stats dashboard** — win rate per giocatore (barre orizzontali), giochi più giocati (grid), trend temporale (sparkline inline)
4. **Export** — modal con CSV/PDF selector + date range + "Scarica"

Design hint:
- Entità primaria: session (--c-session)
- Desktop: table con sticky header
- Mobile: card list con swipe-to-action (dettaglio)
- Chart mini-inline style sparkline (no libreria, solo SVG inline)
- Stats con barre colorate entity-aware (color del gioco/giocatore)

Entità collegate: session primario, game + player secondarie.

Consegna: HTML + JSX + dark + update 00-hub.html.
````

---

### G2.3 — Session creation wizard

**Brief**: sezione 2.3
**Output**: `admin-mockups/design_files/session-wizard.html` + `session-wizard.jsx`
**Entità primaria**: `session`

**Prompt per Claude Design**:

````
[regole universali]

Crea "Session creation wizard" come `session-wizard.html` + `session-wizard.jsx`.

Screen richiesti (5 step + summary):
1. **Step 1 — Game select** — grid da library con search top. Se query `?game=<id>` → salta a step 2 (mostra indicatore "prefilled from Detail")
2. **Step 2 — Players** — add from contacts (checkbox list) + guest players (input name-only, add button)
3. **Step 3 — Turn order** — drag-drop reorder (indicatori visivi), "Random shuffle" button in alto
4. **Step 4 — Variant / house rules** — opzionale, select da KB (dropdown cercabile), skip link
5. **Step 5 — Ready** — summary + "Start session" CTA primario

Design hint:
- Entità primaria: session
- Mobile: stack verticale, step scroll
- Desktop: split-view sx = form step / dx = preview live aggiornata
- Progress bar top (5 step)
- Prefill detection visuale: banner top "Preselezionato da [Game detail]" dismissibile

Entità collegate per step: game (1), player (2-3), kb (4).

Consegna: HTML + JSX + dark + update 00-hub.html.
````

---

## Gruppo 3 🟢 Opzionali (power user)

### G3.1 — Editor / Pipeline / n8n

**Brief**: sezione 3.1
**Output**: `admin-mockups/design_files/editor-pipeline.html` + `editor-pipeline.jsx`
**Entità**: `tool` (`--c-tool`)

**Prompt per Claude Design**:

````
[regole universali]

Crea "Editor / Pipeline / n8n" (area power user) come `editor-pipeline.html` + `editor-pipeline.jsx`.

Screen indicativi (3-4):
1. **Workflow canvas** — node-based editor (stub visuale: card nodes + linee connessione SVG)
2. **Node detail drawer** — config node (input/output, params)
3. **Pipeline status + logs** — lista esecuzioni con badge status, tail logs (mono font)
4. **n8n webhook config** — form endpoint + auth + test button

Design hint:
- Entità: tool (--c-tool)
- Desktop-first (area power user), mobile solo read-only
- Canvas: griglia puntinata, node card con entity border-left
- Logs: JetBrains Mono, color coded per livello (info/warn/err)

Consegna: HTML + JSX + dark + update 00-hub.html sezione "Gruppo 3 Opzionali".
````

---

### G3.2 — Calendar view

**Brief**: sezione 3.2
**Output**: `admin-mockups/design_files/calendar.html` + `calendar.jsx`
**Entità**: `event`

**Prompt per Claude Design**:

````
[regole universali]

Crea "Calendar view" come `calendar.html` + `calendar.jsx`.

Screen richiesti (3):
1. **Month grid** — 7×6 griglia, event pill per giorno, color coded per tipo (game night, session programmata, deadline)
2. **Week view** — time slots verticale (8h-24h), event cards con durata
3. **Day detail** — timeline minute-level, event detail con link a game night / session

Design hint:
- Entità primaria: event
- Mobile: week view default, tap giorno → day detail full-screen
- Desktop: month default, hover event → tooltip

Consegna: HTML + JSX + dark + update 00-hub.html.
````

---

## Gruppo 4 🔵 Esteso (discovery, admin, error)

**Brief completo**: `docs/superpowers/specs/2026-04-20-claude-design-group4-extended-brief.md`

Per ogni sotto-gruppo trovi nel brief: scope dettagliato, screen list, design hints, entità collegate. Qui sotto il prompt quick-start.

### G4.1 — Discovery feed

**Output**: `discovery.html` + `discovery.jsx`

**Prompt**:
````
[regole universali]
Leggi il brief `docs/superpowers/specs/2026-04-20-claude-design-group4-extended-brief.md` sezione "Discovery feed" e produci `admin-mockups/design_files/discovery.html` + `discovery.jsx` + variante dark + update 00-hub.html.
````

### G4.2 — Threads multi-agent avanzato

**Output**: `threads-advanced.html` + `threads-advanced.jsx`

**Prompt**:
````
[regole universali]
Leggi il brief sezione "Threads multi-agent avanzato" e produci `threads-advanced.html` + `.jsx` + dark + hub update.
Focus: split view conversazioni + detail, agent selector entity-colored, citation chips KB, thread branching visualizer.
````

### G4.3 — Toolbox builder (power user)

**Output**: `toolbox-builder.html` + `toolbox-builder.jsx`

**Prompt**:
````
[regole universali]
Leggi il brief sezione "Toolbox builder" e produci `toolbox-builder.html` + `.jsx` + dark + hub update.
Focus: card deck editor drag-drop, phase builder, session tool template library.
````

### G4.4 — Shared games catalog (community)

**Output**: `shared-games.html` + `shared-games.jsx`

**Prompt**:
````
[regole universali]
Leggi il brief sezione "Shared games catalog" e produci `shared-games.html` + `.jsx` + dark + hub update.
Focus: browse community, fork/clone, rating aggregato, filters, submit new game flow.
````

### G4.5 — Admin dashboard redesign

**Output**: `admin-dashboard.html` + `admin-dashboard.jsx`

**Prompt**:
````
[regole universali]
Leggi il brief sezione "Admin dashboard redesign" e produci `admin-dashboard.html` + `.jsx` + dark + hub update.
Focus: overview metrics, user management table, content moderation queue, audit log viewer.
````

### G4.6 — Error / 404 / 403 / 500 states

**Output**: `error-states.html` + `error-states.jsx`

**Prompt**:
````
[regole universali]
Leggi il brief sezione "Error states" e produci `error-states.html` + `.jsx` + dark + hub update.
Focus: 404, 403 forbidden, 500 server error, offline/network error, maintenance mode. Ognuno con illustrazione stub + CTA recovery.
````

---

## 🎯 Priorità consigliata di consegna Claude Design

1. **G2.3 Session wizard** (più critica per flow gameplay)
2. **G2.1 Game Night** (alta visibilità, feature di punta)
3. **G4.6 Error states** (production-ready)
4. **G2.2 Play records** (storia, engagement retention)
5. **G4.1 Discovery feed** (acquisition)
6. **G4.5 Admin dashboard** (operations)
7. **G4.2 Threads advanced** (AI differentiator)
8. **G4.3 Toolbox builder** (power user)
9. **G4.4 Shared games** (community long-term)
10. **G3.2 Calendar** (nice-to-have)
11. **G3.1 Editor pipeline** (ultima, power user avanzato)

---

## 📦 Checklist di consegna per ogni area (Claude Design)

Per ogni pagina, Claude Design deve produrre:
- [ ] File HTML demo standalone (pattern come `01-screens.html`)
- [ ] File JSX con tutti gli screen
- [ ] Eventuali estensioni a `components.css` (NO modifiche breaking — solo nuove classi)
- [ ] Eventuali nuovi dati in `data.js`
- [ ] Aggiornamento `00-hub.html` con link e sezione appropriata (Gruppo 2/3/4)
- [ ] Variante dark mode completa (entity colors più chiari per contrast)
- [ ] Screenshot light + dark per showcase (opzionale ma utile)

---

## 🧭 Riferimenti rapidi

| Cosa | Path |
|------|------|
| Design tokens | `admin-mockups/design_files/tokens.css` |
| Components base | `admin-mockups/design_files/components.css` |
| Dataset demo | `admin-mockups/design_files/data.js` |
| Hub dei mockup | `admin-mockups/design_files/00-hub.html` |
| Brief Gruppo 1+2+3 | `docs/superpowers/specs/2026-04-20-claude-design-missing-pages-brief.md` |
| Brief Gruppo 4 esteso | `docs/superpowers/specs/2026-04-20-claude-design-group4-extended-brief.md` |
| Plan M6 (in corso) | `docs/superpowers/plans/2026-04-20-m6-gruppo1-migration.md` |
| Plan pilot library | `docs/superpowers/plans/2026-04-20-redesign-v2-library-pilot-plan.md` |
| Plan M1-M5 core | `docs/superpowers/plans/2026-04-20-redesign-v2-full-app-migration-m1-m5.md` |

---

## 📝 Status tracker (aggiorna manualmente dopo ogni consegna)

### Gruppo 2 🟡
- [ ] G2.1 Game Night — brief ✅, mockup ⏳, migrazione ⏳
- [ ] G2.2 Play records — brief ✅, mockup ⏳, migrazione ⏳
- [ ] G2.3 Session wizard — brief ✅, mockup ⏳, migrazione ⏳

### Gruppo 3 🟢
- [ ] G3.1 Editor pipeline — brief ✅, mockup ⏳, migrazione ⏳
- [ ] G3.2 Calendar — brief ✅, mockup ⏳, migrazione ⏳

### Gruppo 4 🔵
- [ ] G4.1 Discovery feed — brief ✅, mockup ⏳, migrazione ⏳
- [ ] G4.2 Threads advanced — brief ✅, mockup ⏳, migrazione ⏳
- [ ] G4.3 Toolbox builder — brief ✅, mockup ⏳, migrazione ⏳
- [ ] G4.4 Shared games — brief ✅, mockup ⏳, migrazione ⏳
- [ ] G4.5 Admin redesign — brief ✅, mockup ⏳, migrazione ⏳
- [ ] G4.6 Error states — brief ✅, mockup ⏳, migrazione ⏳

**Legenda**: ✅ fatto · ⏳ pending · 🔄 in corso
