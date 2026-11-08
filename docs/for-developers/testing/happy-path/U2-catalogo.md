# Happy Path — U2 · Catalogo & Discover

> Catalogo scenari area **U2** (Catalogo & Discover). Solo **happy path**. Formato GWT
> (keyword in inglese), testo italiano, osservabili strutturali.
> Template + legenda: [`_TEMPLATE.md`](./_TEMPLATE.md) · Mappa globale: [`_coverage-map.md`](./_coverage-map.md) (§U2).
> Strategia: [`2026-07-10-happy-path-testing-program-design.md`](../../../superpowers/specs/2026-07-10-happy-path-testing-program-design.md).

## Intestazione

- **Area**: U2 — Catalogo & Discover (11 route).
- **Prerequisiti dati (seed `make seed-sp4`)**: catalogo con 14 giochi condivisi (`data.json:games[]` — Azul, Catan, Wingspan, Brass: Birmingham, Gloomhaven, Ark Nova, Spirit Island, 7 Wonders Duel, Codenames, Carcassonne, Ticket to Ride, Pandemic, Terraforming Mars), ognuno con PDF regole indicizzato (`kbDocs[]`) + agenti (`agents[]`) + toolkit (`toolkits[]`); library di `marco` (12 giochi Owned, Gloomhaven Wishlist).
- **Utenti**: `marco@meepleai.test` (premium, verificato) per le route `(authenticated)`; le route `(public)/shared-games*` e `(public)/library-public` sono **anonime** (nessun login richiesto) — verificabili in sessione ospite.
- **Vincolo BGG (freeze 2026-06-10)**: le cover NON usano asset BGG. L'osservabile della card è il **placeholder deterministico** (emoji `🎲` / `🧰` nelle card Discover; emoji `🎲` in `MeepleCardGame` quando `coverUrl` è assente), non un'immagine remota. Nessuna richiesta a `cf.geekdo-images.com` / `*.boardgamegeek.com` in Network.
- **Note strutturali riscontrate all'esplorazione**:
  - Il tab **Trending** di `/games` è **live** (`TrendingTabContent` → endpoint `/api/v1/catalog/trending`), NON un ComingSoon — solo **Catalogo** e **Community** sono placeholder ComingSoon.
  - `/hub` e `/hub/games/[id]` sono **redirect runtime** (rispettivamente → `/games?tab=discover` e → `/games/{id}`): l'osservabile è l'atterraggio sulla destinazione, non un contenuto proprio.
  - Le sotto-pagine `games/[id]/{faqs,rules,sessions}` sono **viste standalone** (link "indietro" → `/library/{gameId}`), non tab interni di `/games/[id]`. Il detail `/games/[id]` (`GameDetailView`) ha il proprio tab-set interno a 7 voci (info/rules/faqs/sessions/stats/agents/documents) con anteprime inline che linkano alle sotto-pagine.
  - `(public)/library-public` monta fixture inline (`LibraryPublicHome`), non un fetch backend: è quindi robusta a servizi giù (smoke sempre eseguibile).

## Matrice di copertura

| Route | Liv. atteso | Scenario/i | Note |
|-------|-------------|-----------|------|
| `(authenticated)/games` | Flow | **U2-01** (browse Discover default tab), **U2-02** (switch tab → Trending live), **U2-08** (smoke Catalogo ComingSoon), **U2-09** (smoke fallback tab invalido → Discover) | Hub multi-tab (`parseTab`). Catalogo/Community = ComingSoon; Trending = live |
| `(authenticated)/games/[id]` | Flow | **U2-03** | `GameDetailView`, 7 tab interni |
| `(authenticated)/games/[id]/faqs` | Smoke | **U2-04** | Lista FAQ accordion |
| `(authenticated)/games/[id]/rules` | Smoke | **U2-05** | Versioni regolamento accordion |
| `(authenticated)/games/[id]/sessions` | Smoke | **U2-06** | Lista sessioni del gioco |
| `(authenticated)/discover` | Flow | **U2-07** | Route standalone (backward-compat) — stesso `DiscoverHub` |
| `(authenticated)/hub` | Smoke | **U2-10** | Redirect → `/games?tab=discover` |
| `(authenticated)/hub/games/[id]` | Smoke | **U2-11** | Redirect → `/games/{id}` |
| `(public)/shared-games` | Smoke | **U2-12** | Catalogo community pubblico (griglia `MeepleCardGame`). Vista read-only con filtro come azione primaria → Smoke |
| `(public)/shared-games/[id]` | Flow | **U2-13** | Dettaglio pubblico gioco community (5 tab) |
| `(public)/library-public` | Smoke | **U2-14** | Landing community (hero + featured + stats, fixture inline) |

**Copertura**: 11/11 route U2 mappate ad ≥1 scenario. Nessuna route `smoke-aggregato` o `skip`.
Totale scenari U2: **14** (Flow: U2-01, U2-02, U2-03, U2-07, U2-13 = 5 · Smoke: U2-04, U2-05, U2-06, U2-08, U2-09, U2-10, U2-11, U2-12, U2-14 = 9).

---

## Scenari

```gherkin
Scenario U2-01 [Flow]: Sfoglio Discover (tab default del hub /games)
  Given sono loggato come marco@meepleai.test (premium, verificato)
    And il catalogo seed contiene ≥10 giochi con PDF regole indicizzato
  When apro /games senza query string
  Then il hub carica con la tab Discover attiva (default)
    And vedo l'hero Discover con la barra di ricerca e la pill-bar dei filtri entità
    And la Row 1 "Trending" mostra card gioco con cover placeholder (emoji, non asset BGG)
    And scorrendo compaiono le row below-the-fold (chunk lazy) con contenuto reale o empty-state legittimo
  Osservabile ✅: elemento con data-testid="games-hub" e data-active-tab="discover"
    + data-slot="entity-filter-pill-bar" con 7 pill (data-filter-id: all/games/agents/toolkits/kbs/people/events)
    + ≥1 data-slot="horizontal-row" con data-row-id="trending" contenente ≥1 data-slot="row-card"
    + NESSUNA richiesta Network a cf.geekdo-images.com / *.boardgamegeek.com
  Route: /games (?tab=discover default), componente DiscoverHub
  Utente: marco

Scenario U2-02 [Flow]: Cambio tab del hub Games verso Trending (contenuto live)
  Given sono su /games con la tab Discover attiva (U2-01)
    And la mini-nav mostra le 4 tab Discover/Catalogo/Trending/Community
  When clicco la tab "Trending" nella mini-nav (o navigo a /games?tab=trending)
  Then il corpo del hub passa alla vista Trending
    And vedo una griglia (variant grid) di card gioco trending con cover placeholder
    And skeleton → contenuto reale (o empty-state se il backend non ha trending)
  Osservabile ✅: data-testid="games-hub" con data-active-tab="trending"
    + data-testid="games-tab-trending" presente
    + al suo interno data-slot="horizontal-row" con card data-slot="row-card" data-variant="grid" (o data-slot="row-empty")
    + il pannello Discover precedente non è più montato
  Route: /games?tab=trending, componente TrendingTabContent
  Utente: marco

Scenario U2-03 [Flow]: Apro il dettaglio di un gioco dal catalogo e navigo i suoi tab
  Given sono loggato come marco@meepleai.test
    And "Azul" è nella library di marco (Owned) con PDF regole indicizzato
  When apro /games/{azulId} (variante "own" perché posseduto)
  Then vedo l'hero del gioco con titolo "Azul" e i KPI (rating/complessità/giocatori/tempo)
    And è presente la tab-bar a 7 voci (info/rules/faqs/sessions/stats/agents/documents) con "info" attiva
  When clicco la tab "Sessions"
  Then il pannello Sessions diventa visibile con il rail delle sessioni recenti (o empty-state) e la CTA "Nuova sessione"
  Osservabile ✅: data-slot="game-detail-view" + hero con testo "Azul"
    + tablist (GameDetailTabsAnimated) con 7 tab
    + al click su Sessions: data-slot="game-detail-panel-sessions" con hidden=false e gli altri pannelli hidden=true
  Route: /games/[id]
  Utente: marco

Scenario U2-04 [Smoke]: Pagina FAQ di un gioco
  Given sono loggato come marco@meepleai.test
    And esiste il gioco "Azul" con id {azulId}
  When apro /games/{azulId}/faqs
  Then la pagina carica (skeleton → contenuto): titolo "FAQ" + link "Gioco" verso /library/{azulId}
    And vedo la card con il conteggio "N Questions" e la lista FAQ, oppure l'empty-state "No FAQs available for this game yet."
  When clicco la prima domanda (se presente)
  Then l'accordion si espande mostrando la risposta
  Osservabile ✅: heading "FAQ" visibile + (card "N Question(s)" con ≥1 voce lista con aria-expanded, oppure empty-state)
    + nessun errore 4xx/5xx non atteso (Network) né errore JS (Console)
  Route: /games/[id]/faqs (api.games.getFAQs)
  Utente: marco

Scenario U2-05 [Smoke]: Pagina Regolamento di un gioco
  Given sono loggato come marco@meepleai.test
    And esiste il gioco "Azul" con id {azulId}
  When apro /games/{azulId}/rules
  Then la pagina carica: titolo "Regolamento" + link "Gioco" verso /library/{azulId}
    And vedo ≥1 card "Version N" con badge conteggio regole, oppure l'empty-state "No rules have been published for this game yet."
  When espando una versione (se presente)
  Then compaiono gli atomi/regole (sezione · pagina · testo)
  Osservabile ✅: heading "Regolamento" visibile + (≥1 card "Version N" espandibile, oppure empty-state)
    + nessun errore Console/Network non atteso
  Route: /games/[id]/rules (api.games.getRules)
  Utente: marco

Scenario U2-06 [Smoke]: Pagina Sessioni di un gioco
  Given sono loggato come marco@meepleai.test
    And "Azul" ha una sessione live nel seed (s-azul-live, InProgress, giocatori marco/sara/luca/giulia)
  When apro /games/{azulId}/sessions
  Then la pagina carica: heading "Sessions" + back link "Back to Game" verso /library/{azulId}
    And vedo la card "N Session(s)" con righe (data · N players · durata · winner), oppure l'empty-state "No sessions recorded for this game yet."
  Osservabile ✅: heading "Sessions" visibile + (card "N Session(s)" con ≥1 riga sessione con badge stato, oppure empty-state)
    + nessun errore Console/Network non atteso
  Route: /games/[id]/sessions (api.games.getSessions)
  Utente: marco

Scenario U2-07 [Flow]: Discover come route standalone (backward-compat)
  Given sono loggato come marco@meepleai.test
    And /discover è preservata per bookmark/link legacy e monta lo stesso DiscoverHub di /games?tab=discover
  When apro /discover
  Then vedo la stessa superficie Discover: hero + search + pill-bar filtri + Row Trending + row lazy
    And NON è presente la mini-nav a 4 tab (single-tab noise rimosso: la mini-nav vive su /games)
  When clicco la pill "Games" nella pill-bar
  Then il filtro entità passa a "games" (aria-pressed sulla pill) e le row si aggiornano di conseguenza
  Osservabile ✅: hero Discover + data-slot="entity-filter-pill-bar"
    + al click: pill data-filter-id="games" con aria-pressed="true"
    + ≥1 data-slot="horizontal-row" con data-row-id="trending"
  Route: /discover, componente DiscoverHub
  Utente: marco

Scenario U2-08 [Smoke]: Tab Catalogo del hub Games mostra ComingSoon con CTA di fallback
  Given sono loggato come marco@meepleai.test
  When apro /games?tab=catalogo
  Then il corpo del hub mostra il placeholder ComingSoon "Catalogo"
    And vedo il messaggio "Funzionalità in arrivo" e le CTA di fallback ("Esplora i giochi con Discover" → /games?tab=discover, "Vai alla tua libreria" → /library)
  Osservabile ✅: data-testid="games-hub" con data-active-tab="catalogo"
    + data-testid="games-tab-catalogo-coming-soon" presente con heading "Catalogo"
    + link di fallback verso /games?tab=discover e /library visibili
  Route: /games?tab=catalogo (ComingSoonTab)
  Utente: marco

Scenario U2-09 [Smoke]: Tab invalido del hub Games ricade su Discover (invariante #20)
  Given sono loggato come marco@meepleai.test
    And l'invariante #20 impone Discover come tab di default
  When apro /games?tab=inesistente
  Then parseTab non riconosce il valore e attiva la tab Discover di default
    And vedo la superficie Discover (hero + pill-bar + Row Trending), NON un ComingSoon
  Osservabile ✅: data-testid="games-hub" con data-active-tab="discover"
    + data-slot="entity-filter-pill-bar" presente
    + NESSUN elemento data-testid="games-tab-*-coming-soon"
  Route: /games?tab=<invalido> (parseTab fallback)
  Utente: marco

Scenario U2-10 [Smoke]: /hub reindirizza al hub Games (Discover)
  Given sono loggato come marco@meepleai.test
    And /hub è un redirect runtime verso /games?tab=discover (Issue #2190)
  When apro /hub
  Then vengo reindirizzato e atterro su /games con la tab Discover attiva
  Osservabile ✅: URL finale = /games?tab=discover
    + data-testid="games-hub" con data-active-tab="discover"
    + nessun errore Console/Network non atteso durante il redirect
  Route: /hub (redirect) → /games?tab=discover
  Utente: marco

Scenario U2-11 [Smoke]: /hub/games/{id} reindirizza al dettaglio gioco canonico
  Given sono loggato come marco@meepleai.test
    And /hub/games/[id] è un redirect runtime verso /games/{id} (Issue #2153)
    And esiste il gioco "Azul" con id {azulId}
  When apro /hub/games/{azulId}
  Then vengo reindirizzato e atterro sul dettaglio canonico /games/{azulId}
  Osservabile ✅: URL finale = /games/{azulId}
    + data-slot="game-detail-view" con hero del gioco
    + nessun errore Console/Network non atteso durante il redirect
  Route: /hub/games/[id] (redirect) → /games/[id]
  Utente: marco

Scenario U2-12 [Smoke]: Catalogo community pubblico (anonimo) con filtro
  Given NON sono loggato (sessione ospite)
    And il catalogo seed è pubblicato con ≥10 giochi community
  When apro /shared-games
  Then la pagina carica (SSR + React Query): hero catalogo community + barra filtri (ricerca/genere/sort/chip)
    And la griglia mostra le card gioco (una per titolo) con cover placeholder emoji (nessun asset BGG) e contatori toolkit/agent/kb
    And la sidebar "Top contributors" è presente (desktop)
  When digito un termine nella ricerca (es. "Azul") — azione primaria
  Then la griglia si aggiorna (debounce 300ms) e il contatore risultati riflette il filtro
  Osservabile ✅: data-testid="shared-games-page"
    + data-slot="shared-games-grid" con data-state="default" contenente ≥1 data-slot="shared-games-card" (data-game-id)
    + dopo la ricerca la griglia si restringe (data-state resta "default" con meno card, o "empty-search" se nessun match — entrambi legittimi)
    + NESSUNA richiesta Network a cf.geekdo-images.com / *.boardgamegeek.com
  Route: (public)/shared-games (searchSharedGames + getTopContributors + getCategories)
  Utente: anonimo (ospite)

Scenario U2-13 [Flow]: Dettaglio pubblico di un gioco community (anonimo) e navigazione tab
  Given NON sono loggato (sessione ospite)
    And apro il catalogo /shared-games e individuo la card di "Azul"
  When clicco la card "Azul" (Link → /shared-games/{sharedGameId})
  Then atterro sul dettaglio pubblico: hero con titolo "Azul", rating, meta (giocatori/tempo/complessità) e contatori toolkit/agent/kb
    And è presente la tab-bar a 5 voci (overview/toolkits/agents/knowledge/community) con "overview" attiva e la descrizione del gioco
  When clicco la tab "Toolkits"
  Then il pannello Toolkits diventa visibile con la lista dei toolkit pubblicati (o empty-state "no-toolkits")
    And in fondo è presente la StickyCta "Sign in" (perché ospite)
  Osservabile ✅: data-testid="shared-game-detail-page" con data-active-tab che passa a "toolkits"
    + hero con testo "Azul" + tablist a 5 voci
    + al click: sezione role="tabpanel" toolkits con hidden=false (lista ToolkitListItem o EmptyState)
    + StickyCta con link /login visibile
  Route: (public)/shared-games/[id]
  Utente: anonimo (ospite)

Scenario U2-14 [Smoke]: Landing community pubblica (library-public)
  Given NON sono loggato (sessione ospite)
    And /library-public monta fixture inline (hero + 4 giochi featured + stats community)
  When apro /library-public
  Then la pagina carica: hero "Scopri la community board game di MeepleAI" con CTA "Inizia gratis" (→ /join) e "Come funziona" (→ /how-it-works)
    And vedo la sezione "Giochi in evidenza" con il carosello di card featured (cover placeholder)
    And vedo la riga statistiche community (4 metriche) e la sezione "Cosa puoi fare" a 3 bullet
  Osservabile ✅: heading "Scopri la community board game di MeepleAI" visibile
    + sezione "Giochi in evidenza" con ≥1 card gioco featured
    + riga statistiche con 4 valori + CTA footer "Crea account gratis" (→ /join)
    + nessun errore Console/Network non atteso
  Route: (public)/library-public (LibraryPublicHome, fixture inline)
  Utente: anonimo (ospite)
```

---

## Auto-verifica

- **Copertura route**: tutte e 11 le route U2 di `_coverage-map.md` compaiono nella matrice, ognuna con ≥1 scenario (nessun `smoke-aggregato`, nessun `skip`).
  - `games` → U2-01/02/08/09 · `games/[id]` → U2-03 · `games/[id]/faqs` → U2-04 · `games/[id]/rules` → U2-05 · `games/[id]/sessions` → U2-06 · `discover` → U2-07 · `hub` → U2-10 · `hub/games/[id]` → U2-11 · `shared-games` → U2-12 · `shared-games/[id]` → U2-13 · `library-public` → U2-14.
- **Osservabili**: ogni scenario dichiara ≥1 osservabile strutturale concreto (data-testid / data-slot / heading / URL / aria-*), verificabile a schermo senza dipendere da testo generato da LLM.
- **Solo happy path**: nessuno scenario negativo/errore/edge; gli empty-state sono trattati come esito legittimo delle viste Smoke (criterio §5).
- **Vincolo BGG**: gli scenari con card gioco (U2-01, U2-02, U2-07, U2-12, U2-13, U2-14) verificano il placeholder deterministico e l'assenza di richieste ad host BGG.
- **Dati concreti dal seed**: giochi (Azul + catalogo 14 titoli), sessione `s-azul-live`, utente `marco`; le route pubbliche usano sessione anonima. Nessuna entità creata da questi scenari (tutti browse/read) → nessun marcatore `HP-TEST-<data>` necessario in U2.
```
