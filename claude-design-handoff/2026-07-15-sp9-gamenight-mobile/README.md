# Claude Design handoff — SP9 Mobile GameNight Social (2026-07-15)

Snapshot della sessione di **generazione** dei 3 mockup mobile-first del flow GameNight/Session social (issue [#2989](https://github.com/meepleAi-app/meepleai-monorepo/issues/2989), follow-up del gap headline SP8 #1890).

- **Run**: 2026-07-15 · progetto "MeepleAI Mobile GameNight Social" · 3 turni di generazione (A→B→C).
- **Brief**: [`admin-mockups/briefs/SP9-mobile-gamenight-social.md`](../../admin-mockups/briefs/SP9-mobile-gamenight-social.md)
- **System prompt**: [`claude-design-demo-prompts.md`](../../docs/for-developers/workflows/claude-design-demo-prompts.md) § SP9
- **PR**: [#2983 → follow-up branch `feature/issue-2989-mobile-gamenight-social`]

## Nota — formato `.dc.html`

Come la run [2026-06-30-sp6](../2026-06-30-sp6/), Claude Design ha esportato **Design Component** (`.dc.html` + `support.js` runtime), non `.html`/`.jsx` standalone. Il brief chiedeva `.{html,jsx}`; l'export reale è `.dc.html`. Per portarli in `admin-mockups/design_files/` come sorgenti standalone servirebbe estrarre il markup (o un turno di export dedicato). La cartella `uploads/` dell'export originale (bundle seed) **non** è inclusa — ridondante con `admin-mockups/design_files/` + `claude-design-bundle/sp9-gamenight-mobile/`.

## Contenuto

| File | Cosa |
|---|---|
| `sp9-dashboard-game-night-mobile.dc.html` | A — dashboard GN mobile: Prossimi (con card pending-RSVP #17) + Recenti (#4) |
| `sp9-game-nights-index-mobile.dc.html` | B — /game-nights index: list/calendar toggle + filter + FAB nuova serata |
| `sp9-game-night-detail-rsvp-mobile.dc.html` | C — detail+RSVP: hero + roster + RSVP bar 3-button + Invia inviti (host) |
| `support.js` | runtime dell'export Claude Design (referenziato dai `.dc.html`) |
| `thumbnail.webp` | preview |

## Come navigare

```powershell
cd claude-design-handoff/2026-07-15-sp9-gamenight-mobile
python -m http.server 8765
# Apri http://localhost:8765/sp9-dashboard-game-night-mobile.dc.html
```

## Validazione (workflow 3-agenti, 2026-07-15)

**Verdetto: tutti e 3 `minor-issues`** — nessun blocker. Stati chiave e invarianti coperti; **le CTA RSVP critiche sono a 52px** (il gap SP8 B-02 a 24px NON si ripete).

| Mockup | Stati | Invarianti rese |
|---|---|---|
| A dashboard | 10 ✅ | `[INV-4]` `[INV-17]` |
| B index | 10 ✅ | `[INV-15]` |
| C detail-RSVP | 12 ✅ | `[INV-15]` `[INV-16]` `[INV-17]` |

### Fix applicati e verificati (turn correttivo 2026-07-15)

Workflow di re-validazione (3-agenti): **B verified (3/3)** · **C verified** · **zero regressioni**. A: 3/4 (marker parziale, nota sotto).

1. ✅ **768px tablet** aggiunto a tutti e 3 (frame A-07 / B-09 / C-10 — stessa UI centrata in colonna più larga, mobile-first resta canonical).
2. ✅ **B scrim tokenizzato** — introdotto token `--scrim` (light + dark), rimosso `rgba(20,12,4,.42)` dal `dayDrawer` (0 occorrenze residue).
3. ✅ **B touch-target chrome ≥44px** — filter chip, toggle Lista|Calendario, nav mese ‹ ›, close × drawer.
4. ⚠️ **A marker** — presente **un** `[GAP-STATE]` "Tap Conferma → card normale" a livello card (l'interazione core #17 è documentata); **non** un marker separato su Declina. Granularità minore, accettata.

**CTA RSVP critiche invariate (52px)** · zero hex/scrim di prodotto (resta solo il bezel del device-frame decorativo, accettabile).

### Accettabile (non-fix)
- Device-frame bezel `#1a1a1a` + `rgba(0,0,0,.28)` shadow: chrome del telefono nel mockup, non UI di prodotto (tutti i colori prodotto usano `hsl(var(--c-*))`).
- Nav chrome (back/Altro/Menu/Notifiche) a 38-40px: non sono le CTA C-critical del DoD (pattern SP8 A-11 MINORE).
