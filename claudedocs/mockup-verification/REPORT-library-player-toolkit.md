# Verifica visiva mock vs app — library / player / toolkit

**Data analisi**: 2026-05-27 · **Metodo**: confronto degli screenshot in `admin-mockups/check/` (coppie `*_app.png` / `*_mock.png` catturate 2026-05-26 in sessione precedente; **non** ri-catturati live in questa sessione — nessun dev server attivo).
**Ambito**: 3 superfici hub/detail dell'Epic #1475 — `/library`, player detail, `/toolkits` (hub).
**Caveat**: `library_app.png` è a bassa risoluzione/compresso → confronto library a grana grossa. player/toolkit leggibili.

## Verdetto sintetico

| Superficie | Layout vs mockup | Esito | Natura della differenza |
|---|---|---|---|
| **library** | ❌ divergenza strutturale maggiore | gap **atteso** | app = vista Wave B.3 **3-tab game-only**; mock = **hybrid hub 6-tab** (#1585). Phase 2/3 non ancora implementate. |
| **player** | ✅ struttura combacia | conforme | differenze solo di **dati** (utente reale vuoto vs demo) |
| **toolkit** | ✅ struttura combacia (parziale) | conforme con **drift di copy/KPI** | hero copy diverso, 3 KPI vs 4, label filtri diverse |

Nessuna regressione user-visible bloccante. Il solo "scostamento" grosso (library) è lavoro #1585 in corso, non un difetto.

## library — gap atteso (#1585 WIP)

**Mock** (`library_mock.png` = `sp4-library-desktop`): hybrid hub completo —
- Hero "La tua libreria" + sottotitolo "Tutti i tuoi giochi, agenti e documenti in un posto."
- Stat chips giochi/agenti/documenti + badge chat + CTA "Aggiungi gioco" (arancione) + "Importa BGG"
- **6 tab**: Tutti / Giochi / Agenti / KB / Sessioni / Chat
- Filtri STATO / STATS / ORD + "Più filtri" + search
- Griglia **multi-entità** (giochi + agenti + KB docs + chat mescolati)
- Sidebar destra "Ultime modifiche" (activity rail) + "Shortcuts"

**App** (`library_app.png`): vista corrente —
- Hero "La mia libreria"
- Griglia **game-only** con box-art reali (Catan, Azul, Wingspan, …)
- Tab ridotti (Wave B.3: all/kb/loaned)

**Lettura**: combacia con lo stato noto di #1585 — Phase 1 (#1591, merged) ha consegnato solo le fondamenta type-level (union/mappers/`deriveHybridItems`); l'orchestrazione UI a 6 tab è **Phase 2 (#1592/#1605)** e il rail è **Phase 3b (#1593)**, non ancora implementate. **Non è una regressione.** La copy hero diverge ("La mia" vs "La tua libreria") — da uniformare quando si implementa l'hero hybrid (AC3, Phase 2a #1605).

## player — conforme (differenze solo di dati)

**Mock** (`player_mock.png`): hero viola (entity=player), avatar "MR", badge PLAYER + PRO, "Marco R.", "@marco.r · Milano · Membro da Gen 2025", KPI PARTITE 89 / VITTORIE 47 / WIN RATE 53% / TOP Azul, CTA Confronta + Modifica, mini-nav chips (Top giochi/Partite/Game Nights/Agenti usati/Toolkit/Chat), tab Overview/Partite/Giochi/Toolkit/Achievement, empty "Nessuna partita ancora".

**App** (`player_app.png`): **stessa struttura** — hero viola, avatar, KPI a 4 card, stessi mini-nav chips, stessi tab, empty state "Nessuna partita registrata" + "Sessioni recenti".

**Differenze** (tutte spiegabili dallo stato dati, non difetti di migrazione):
- Nome "me" + badge "EVENT" (utente reale loggato senza display name/ruolo demo) vs "Marco R." PLAYER/PRO
- KPI a 0/0/0%/0 (utente senza partite) vs valori demo
- Badge "EVENT" sull'app: da verificare se è il ruolo reale o un fallback — **unico punto da controllare nel codice**

**Lettura**: migrazione player-detail strutturalmente riuscita. Le card KPI, i chip mini-nav e la tab bar combaciano col mock.

## toolkit — conforme con drift di copy/struttura

**Mock** (`toolkit_mock.png` = `hub/toolkits`): dark theme, badge "HUB · /HUB/TOOLKITS", titolo **"Catalogo toolkit community"**, sottotitolo "Bundle pronti all'uso di strumenti (timer, dadi, counter, deck)…", **4 KPI** (TOOLKIT / INSTALLAZIONI / FEATURED / **STRUMENTI TOTALI**), filtri **Tutti / Featured / Nuovi / Top 100**, CTA "+ Crea toolkit" in top-nav, empty "Nessun toolkit trovato" + "Azzera filtri" (verde).

**App** (`toolkit_app.png`, #1480 già mergeato): dark theme ✓, badge "HUB · /TOOLKITS", titolo **"Toolkit community"**, sottotitolo "Toolkit di strumenti condivisi dalla community per arricchire le tue sessioni.", **3 KPI** (TOOLKIT / INSTALLAZIONI / FEATURED), filtri **Tutti / In evidenza / Nuovi / Top**, empty "Nessun risultato" + "Azzera filtri" (verde).

**Drift rilevato** (divergenze reali, da confermare nel codice prima di agire — potrebbero essere scelte intenzionali post-#1480):
1. **Copy hero**: "Toolkit community" vs "Catalogo toolkit community"; sottotitolo riformulato.
2. **KPI mancante**: l'app ha 3 KPI, il mock 4 (manca **"Strumenti totali"**).
3. **Label filtri**: "In evidenza" vs "Featured", "Top" vs "Top 100".
4. **Empty-state copy**: "Nessun risultato / Prova a modificare i filtri o la ricerca" vs "Nessun toolkit trovato / Nessun toolkit corrisponde ai filtri attuali".
5. **Header**: app usa l'app-shell standard (hamburger + search + theme + avatar); il mock mostra una top-nav dedicata (Home/Libreria/Hub/Sessioni/Toolkit) + CTA "+ Crea toolkit" — differenza di chrome, probabilmente fuori scope #1480.
6. Dati a 0 (DB vuoto in ambiente di test) — **non** difetto.

## Follow-up consigliati

| Pri | Superficie | Azione |
|---|---|---|
| — | library | Nessuna azione: gap atteso, coperto da #1585 Phase 2a #1605 (hero+6 tab) e Phase 3b #1593 (rail). Allineare copy hero "La tua libreria" in Phase 2a. |
| P3 | player | Verificare nel codice il badge "EVENT" (ruolo reale vs fallback) — unico punto ambiguo. |
| P3 | toolkit | Decidere se il drift copy/KPI (#1480) è intenzionale; se no, aprire issue per: 4° KPI "Strumenti totali", label filtri "Featured/Top 100", copy hero/empty allineati al mock. |
| — | metodo | Gli screenshot app sono del 2026-05-26. Per un check rigoroso pre-release, ri-catturare live con dati seed (SP4 dataset, #1579) così i KPI non sono a 0 e il confronto è dato-completo. |

## Note di metodo

- Verifica **statica** su screenshot esistenti, non live. I verdetti "conforme" riguardano **struttura/layout**, non pixel-precision né stato runtime (i18n, errori API) — per quello servirebbe il dev server come nel REPORT.md game-detail (2026-05-25).
- Le differenze di **dati** (player vuoto, toolkit 0) derivano dall'ambiente di cattura, non dalla migrazione.
- Per la verifica i18n-runtime (gap pattern P71) di queste 3 superfici servirebbe una cattura live: non desumibile dagli screenshot statici.
