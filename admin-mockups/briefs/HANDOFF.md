# Handoff Pacchetto Claude Design — MeepleAI redesign v2

## Cos'è questo pacchetto

Questo archivio contiene tutto il necessario per aprire una sessione Claude Design (o qualsiasi altro tool di design AI) e produrre i mockup mancanti per completare il redesign v2 di MeepleAI.

## Contenuto

```
admin-mockups/
├── README.md                          # Handoff convention, data model, architecture
├── briefs/
│   ├── HANDOFF.md                     # Questo file
│   ├── _common.md                     # Preambolo obbligatorio (token, pattern, DoD)
│   ├── SP3-public-secondary.md        # 9 pagine pubbliche secondarie
│   ├── SP4-entity-desktop.md          # 16 pagine desktop entity-driven
│   └── SP5-admin-tools.md             # 14 pagine admin + power-user
└── design_files/
    ├── tokens.css                     # Design tokens (OBBLIGATORIO leggere)
    ├── components.css                 # Componenti base styled
    ├── data.js                        # Dati finti realistici (games, players, etc.)
    ├── 00-hub.html                    # Index navigabile dei mockup
    ├── 01-screens.html                # Screen catalog mobile
    ├── 02-desktop-patterns.html       # 4 pattern desktop (split-view, sidebar-detail, tabs, hero+body)
    ├── 03-drawer-variants.html        # 6 varianti drawer (tabbed bottom-sheet canonica)
    ├── 04-design-system.html          # Design system reference
    ├── 05-dark-mode.html              # Dark mode showcase
    ├── auth-flow.{html,jsx}           # Mockup già consegnato (SP1)
    ├── mobile-app.jsx                 # Mockup già consegnato (SP2)
    ├── public.{html,jsx}              # Mockup già consegnato (marketing base)
    ├── settings.{html,jsx}            # Mockup già consegnato
    ├── onboarding.{html,jsx}          # Mockup già consegnato
    └── notifications.{html,jsx}       # Mockup già consegnato
```

## Come usare questo pacchetto

### 1. Setup sessione Claude Design

Apri una nuova conversazione con Claude (o con il tool AI di design scelto) e incolla in ordine:

1. **Prima**: contenuto integrale di `briefs/_common.md` (preambolo obbligatorio)
2. **Poi**: contenuto integrale del brief SP scelto (`SP3`, `SP4`, o `SP5`)
3. **Infine**: istruzioni operative — "Produci le schermate in ordine di priorità. Una risposta = una schermata."

### 2. Una sessione per sub-project

**Non mescolare SP in una sola sessione.** Crea 3 sessioni separate:

| Sessione | Brief | Output atteso |
|----------|-------|---------------|
| A | `SP3-public-secondary.md` | 9 file `sp3-*.{html,jsx}` |
| B | `SP4-entity-desktop.md` | ~16 file `sp4-*.{html,jsx}` |
| C | `SP5-admin-tools.md` | ~14 file `sp5-*.{html,jsx}` |

Le 3 sessioni possono girare in parallelo (coprono audience diverse, zero sovrapposizione).

### 3. File di riferimento da mostrare a Claude Design

Se la sessione lo supporta, allega o referenzia questi file perché Claude Design abbia contesto visivo/strutturale:

**Obbligatori**:
- `design_files/tokens.css` → tutti i token di design (colori, spacing, typography, motion)
- `design_files/02-desktop-patterns.html` → 4 pattern desktop canonici
- `design_files/03-drawer-variants.html` → drawer tabbed bottom-sheet canonica

**Consigliati per coerenza**:
- `design_files/auth-flow.jsx` o `mobile-app.jsx` → esempi reali di come il design system è applicato
- `design_files/data.js` → nomi gioco/giocatore italiani realistici da riusare
- `design_files/components.css` → classi componenti base

### 4. Consegna mockup prodotti

Claude Design salva ogni nuovo file in `design_files/` con prefisso `sp3-*`, `sp4-*`, `sp5-*`:

```
design_files/
├── sp3-faq.html
├── sp3-faq.jsx
├── sp3-how-it-works.html
├── ...
├── sp4-game-detail.jsx
├── ...
└── sp5-admin-overview.jsx
```

## Definition of Done per ogni mockup

Questi criteri (estratti da `_common.md`) sono **bloccanti**:

- [ ] Usa solo token da `tokens.css` (nessun colore/spacing hardcoded)
- [ ] Light + dark mode entrambi funzionanti (`data-theme="dark"` sul `<html>`)
- [ ] Mobile 375px + desktop 1440px entrambi presenti (responsive contract)
- [ ] Stati default/empty/loading/error tutti disegnati
- [ ] `EntityChip`/`EntityPip` per ogni riferimento cross-entity (mai testo semplice)
- [ ] ARIA essenziale: `role="dialog"` su drawer, `role="tablist"` su tab, `aria-label` su icon-only
- [ ] Focus visibile keyboard-only (`outline: 2px solid hsl(var(--c-game))` o `--e-ring`)
- [ ] Nessun TODO o placeholder visibile
- [ ] Testo UI in italiano, dati realistici da `data.js`
- [ ] Commento di apertura nel file con route target + descrizione 1-riga

## Constraint non-negoziabili

❌ **NON fare mai**:
- Aggiungere nuovi entity type (restano 9: game, player, session, agent, kb, chat, event, toolkit, tool)
- Usare palette grey (solo warm neutrals)
- Importare font fuori da Quicksand/Nunito/JetBrains Mono
- Creare border-radius arbitrari (usa scala `--r-*`)
- Aggiungere shadow blu/grigi (solo warm `rgba(90,60,20,...)` light / pure black dark)
- Usare immagini esterne (gradient placeholder o emoji)
- Scrivere UI in inglese (app è in italiano)

## Ordine di priorità

Ogni brief SP contiene una sezione "Priorità produzione" con l'ordine alto→basso impatto. Segui quell'ordine per consegnare prima le schermate più critiche.

**Overall program priority**:
1. SP3 #4 + #5 (Accept invite + Join) — blocca onboarding nuovi utenti
2. SP4 Game detail + Agent character sheet — top-traffic authenticated
3. SP4 Session live desktop — core live play
4. SP5 Admin Overview + Users + AI/RAG Quality — operations quotidiane
5. Resto secondo priorità nei brief

## Handoff finale (cosa consegnare al team dev)

Quando una sessione SP è completa:
1. Lista tutti i file `sp{n}-*.{html,jsx}` prodotti
2. Lista tutti i nuovi componenti v2 introdotti (andranno in `apps/web/src/components/ui/v2/`)
3. Eventuali dubbi / deviazioni dai token (con motivazione)
4. Screenshot mentali light/dark/mobile/desktop per ogni schermata

Il team dev riceve il pacchetto aggiornato + handoff note e scrive le spec implementative (`docs/superpowers/specs/2026-04-2X-<sp>-impl.md`) usando i mockup come contratto.

## Riferimenti programma

- Program overview: `docs/superpowers/specs/2026-04-21-redesign-v2-program.md` (sequencing 5 SP + dipendenze + rischi)
- Gap analysis: `docs/superpowers/specs/2026-04-21-full-redesign-gap-analysis.md` (sorgente decomposizione)
- SP1 spec (completata): `docs/superpowers/specs/2026-04-21-auth-flow-v2-design.md`

## Supporto

Se un brief ha ambiguità o manca un caso:
- **Non inventare**. Ferma la sessione e apri issue con label `redesign-v2 mockup-gap`.
- Il brief va aggiornato prima di procedere.
- Non fare guess sui token — tutti devono venire da `tokens.css`.
