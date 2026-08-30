# Happy Path — U8 · Profilo & Notifiche

> Catalogo scenari happy-path per l'area **U8 (Profilo & Notifiche)**. Solo percorso di successo. Formato Given/When/Then per `_TEMPLATE.md`. Osservabili basati su **struttura** (elemento presente, navigazione avvenuta, chiamata rete, empty-state legittimo), mai su testo generato da LLM.

## Intestazione

- **Area**: U8 — Profilo & Notifiche
- **Route roots**: `(authenticated)/{profile/**, notifications/**, versions, dashboard, n8n}`
- **Prerequisiti dati (seed `seed-sp4`)**: utente `marco@meepleai.test` (premium, verificato) con library popolata (12 giochi, di cui Gloomhaven in Wishlist), play-record aggregati (89 sessioni), 1 sessione InProgress (`s-azul-live`) + varie completate. **Nota dati mancanti**: il seed NON crea notifiche, achievement sbloccati, né uno storico `RuleSpec` versioni per l'utente. Di conseguenza gli osservabili di `/notifications`, `/profile/achievements` e `/versions` accettano l'**empty-state legittimo** come pass happy-path (il criterio è la struttura pagina + azione primaria, non la presenza di N righe).
- **Utente/i**: `marco` (User standard) per tutti gli scenari user-facing. `admin` per lo scenario `/n8n` (superficie admin-scoped).
- **Account**: admin da `infra/secrets/admin.secret`; marco password default `seed_password()` (`infra/scripts/seed-sp4/lib/common.sh`).

### Note di superficie rilevate in esplorazione

- **`/profile`** è una single-page a 4 tab guidati da `?tab=` (`overview` default · `achievements` · `activity` · `settings`); il tab `settings` ha un secondo asse `?section=`. La modifica nome profilo avviene in un **Sheet** (bottone "Modifica" → campo "Nome visualizzato" → "Salva") → `PUT /api/v1/users/profile`. L'avatar upload è `POST` multipart su `/api/v1/users/avatar` (optimistic blob preview).
- **`/notifications`** usa lo store Zustand `useNotificationStore.fetchNotifications` → `GET /api/v1/notifications`. Aprire una card apre un Drawer e marca letta (`POST /api/v1/notifications/{id}/mark-read`). "Segna tutte come lette" → `POST /api/v1/notifications/mark-all-read`. Il toggle "N non lette" filtra unread; le pill entity-colorate (Tutte/Sessioni/Agenti/Serate/Sistema) filtrano per categoria.
- **`/notifications/preferences`** (`force-dynamic`): `GET /api/v1/notifications/preferences` → 5 categorie di Switch (Documento pronto / Elaborazione fallita / Inviti serata / Promemoria serata / Retry). "Salva preferenze" → `PUT /api/v1/notifications/preferences` → toast "Salvato".
- **Contatore notifiche SSE**: `useNotificationsCounter` (Asse B #1897 WP6) apre `GET /api/v1/notifications/stream` (SSE) con seed da `GET /api/v1/notifications/unread-count`. Il **badge visibile** in topbar è il `NotificationBell` (in `UnifiedHeader`), che legge lo store Zustand; l'hook SSE è l'infrastruttura di push parallela. L'osservabile del contatore è quindi il badge campanella + (a livello Network) la connessione allo stream.
- **`/versions`** (`?gameId=…`): storico `RuleSpec` di un gioco (`GET /api/v1/games/{id}/rulespec/history` + diff). **Senza `gameId`** mostra il prompt "Specifica un gameId…". Il pulsante "Ripristina" è **role-gated** (admin/editor/superadmin) — per `marco` (User) la vista è read-only.
- **`/dashboard`** = orchestratore priority-driven Asse C (`DashboardClient`, #1898): Hero + KPI grid, poi 4 sezioni in ordine fisso **Prossimi / Recenti / Suggeriti / FriendsActivity** + un blocco inline "Giochi con agente pronto" (Block C). Ogni sezione deriva `loading|error|empty|default` dalla propria query; empty è silent-fallback legittimo.
- **`/n8n`** = pagina di gestione config n8n che fa `fetch('/admin/n8n')` — **superficie admin**. Per un utente standard è tipicamente 401/403 → smoke da eseguire come `admin` (o `⚠️ blocked-env` se il ruolo utente non ha accesso).

---

## Matrice di copertura

Le 7 route mappate a U8 nel `_coverage-map.md`, ognuna con ≥1 scenario.

| Route | Liv. | Scenario/i |
|-------|------|-----------|
| `(authenticated)/dashboard` | Smoke | U8-01 |
| `(authenticated)/profile` | Flow | U8-02 (overview smoke), U8-03 (edit displayName), U8-04 (tab activity), U8-08 (tab settings) |
| `(authenticated)/profile/achievements` | Smoke | U8-05 |
| `(authenticated)/notifications` | Flow | U8-06 (mark-read), U8-07 (mark-all + filtri), U8-11 (SSE badge) |
| `(authenticated)/notifications/preferences` | Flow | U8-09 |
| `(authenticated)/versions` | Smoke | U8-10 |
| `(authenticated)/n8n` | — | skip: feature n8n in rimozione |
| `/offline` (top-level, PWA fallback) | Smoke | U8-13 |

**Copertura**: 8 route — 7 coperte + `/offline` + 1 `skip` (`/n8n`, feature n8n in rimozione). Nessun `smoke-aggregato`.

> Il tab `achievements` di `/profile` (interno a U8-02/U8-03) e la route standalone `/profile/achievements` condividono lo stesso componente `AchievementsGrid`; U8-05 verifica la route standalone, coerente con la mappa.

---

## Scenari

```gherkin
Scenario U8-01 [Smoke]: Dashboard priority-driven carica le 4 sezioni Asse C
  Given sono loggato come marco@meepleai.test (premium, verificato)
    And il seed ha popolato library (12 giochi) e sessioni per marco
  When apro /dashboard
  Then vedo l'Hero con il saluto e la KPI grid (Giochi / Sessioni / Ore / Win rate)
    And sotto l'Hero compaiono le 4 sezioni nell'ordine fisso: "Prossimi", "Recenti", "Suggeriti", "FriendsActivity"
    And ogni sezione mostra il suo contenuto reale oppure un empty-state legittimo (nessuno skeleton residuo)
    And la KPI "Giochi" mostra un numero coerente con la library (≥1); Ore/Win rate mostrano "—" (non esposti dal backend)
  Osservabile ✅: DashboardHero visibile + KPI "Giochi" con valore numerico + 4 heading di sezione nell'ordine atteso + nessun errore 4xx/5xx (Network) né errore JS (Console)
  Route: /dashboard
  Utente: marco

Scenario U8-02 [Smoke]: Profilo overview con stats library e ultime partite
  Given sono loggato come marco@meepleai.test
    And marco ha una library popolata e play-record dal seed
  When apro /profile (tab overview di default)
  Then vedo l'header profilo con avatar, displayName (es. "Marco R.") ed email
    And la card "Library Stats" mostra le 6 tile (Giochi/Preferiti/Posseduti/Wishlist/PDF caricati/In prestito) con valori numerici
    And la card "Ultime partite" elenca partite recenti oppure il messaggio "Nessuna partita ancora"
    And la card "Quick Actions" mostra i link Achievements / My Library / Storia di gioco
  Osservabile ✅: displayName in header + 6 tile stats con valori + card "Ultime partite" e "Quick Actions" presenti + nessun errore Console/Network
  Route: /profile
  Utente: marco

Scenario U8-03 [Flow]: Modifica nome visualizzato dal profilo
  Given sono loggato come marco@meepleai.test e sono su /profile
    And il displayName corrente è mostrato nell'header
  When clicco il bottone "Modifica" (apre lo Sheet "Modifica profilo")
    And nel campo "Nome visualizzato" imposto "HP-TEST-2026-07-10 Marco"
    And clicco "Salva"
  Then parte una PUT /api/v1/users/profile con il nuovo displayName
    And lo Sheet si chiude
    And l'header profilo si aggiorna al nuovo nome (via invalidazione query utente)
    And dopo un reload della pagina l'header mostra ancora "HP-TEST-2026-07-10 Marco" (persistito nel backend)
  Osservabile ✅: PUT /api/v1/users/profile 2xx (Network) + Sheet chiuso + header mostra "HP-TEST-2026-07-10 Marco" + **post-reload il nuovo nome persiste**
  Route: /profile
  Utente: marco

Scenario U8-04 [Flow]: Cambio tab a "Attività" e feed carica
  Given sono loggato come marco@meepleai.test e sono su /profile
  When clicco il tab "Attività" nella TabBar
  Then l'URL riflette ?tab=activity (router.replace)
    And il pannello mostra il sottotitolo "Le tue ultime partite, achievement e aggiornamenti"
    And il componente ActivityFeed carica il suo contenuto reale o un empty-state legittimo (nessuno skeleton residuo)
  Osservabile ✅: query string ?tab=activity + ActivityFeed montato con contenuto/empty-state + nessun errore Console/Network
  Route: /profile
  Utente: marco

Scenario U8-05 [Smoke]: Pagina Achievements standalone con filtri
  Given sono loggato come marco@meepleai.test
  When apro /profile/achievements
  Then vedo l'heading "Achievements" e il sottotitolo "Tieni traccia dei tuoi traguardi di gioco"
    And la barra filtri mostra i pulsanti "Tutti / Ottenuti / In Corso / Bloccati"
    And la griglia mostra le card achievement reali oppure l'empty-state "Nessun achievement disponibile" (seed senza achievement = empty legittimo)
    And clicco il filtro "Ottenuti" e la griglia si aggiorna coerentemente (subset o empty filtrato)
  Osservabile ✅: heading "Achievements" + 4 pulsanti filtro + griglia o empty-state + il click su "Ottenuti" produce un cambiamento visibile a schermo + nessun errore Console/Network
  Route: /profile/achievements
  Utente: marco

Scenario U8-06 [Flow]: Apri una notifica e marcala come letta
  Given sono loggato come marco@meepleai.test
    And esiste ≥1 notifica non letta (creata via azione precedente o seed; se assente vedi U8-07 nota)
  When apro /notifications
    And la lista raggruppata per giorno (Oggi/Ieri/…) mostra le NotificationCard
    And clicco una card non letta
  Then si apre il Drawer di dettaglio con titolo e timestamp della notifica
    And parte una POST /api/v1/notifications/{id}/mark-read
    And la card perde lo stato "unread" e il contatore "N non lette" si decrementa
  Osservabile ✅: Drawer dettaglio aperto con titolo notifica + POST .../mark-read 2xx (Network) + contatore non-lette decrementato
  Route: /notifications
  Utente: marco

Scenario U8-07 [Flow]: Filtri categoria + "Segna tutte come lette"
  Given sono loggato come marco@meepleai.test e sono su /notifications
    And la lista è caricata (GET /api/v1/notifications) con contenuto reale o empty-state legittimo
  When clicco la pill categoria "Serate" (entity event)
    And la lista si filtra ai soli tipi game_night_*
    And clicco "Segna tutte come lette"
  Then parte una POST /api/v1/notifications/mark-all-read
    And il contatore in header passa a "Nessuna notifica non letta" e il bottone si disabilita
  Osservabile ✅: filtro "Serate" attivo (aria-pressed) + lista coerente/empty + POST .../mark-all-read 2xx + contatore azzerato + "Segna tutte come lette" disabilitato
  Route: /notifications
  Utente: marco
  Nota dati: se il seed non produce notifiche, la lista è empty-state ("Nessuna notifica per ora" + CTA "Configura preferenze") — resta pass happy-path (struttura pagina + azione primaria). Il ramo mark-all è verificabile solo con ≥1 non letta.

Scenario U8-08 [Flow]: Tab Impostazioni con navigazione a sezione
  Given sono loggato come marco@meepleai.test e sono su /profile
  When clicco il tab "Impostazioni"
  Then l'URL riflette ?tab=settings&section=<default> (router.replace)
    And viene montato il SettingsTab con la sezione di default selezionata
    And navigando a un'altra sezione l'URL aggiorna ?section=… e il pannello destro cambia contenuto
  Osservabile ✅: query string ?tab=settings&section=… + SettingsTab montato + il cambio sezione produce un contenuto diverso a schermo + nessun errore Console/Network
  Route: /profile
  Utente: marco

Scenario U8-09 [Flow]: Modifica e salva preferenze notifiche
  Given sono loggato come marco@meepleai.test
  When apro /notifications/preferences
    And attendo il caricamento (GET /api/v1/notifications/preferences) delle 5 categorie di Switch
    And nella categoria "Documento pronto" cambio lo stato dello Switch "Email"
    And clicco "Salva preferenze"
  Then parte una PUT /api/v1/notifications/preferences con il payload aggiornato
    And compare un toast "Salvato" ("Preferenze di notifica aggiornate")
    And dopo un reload della pagina lo Switch "Email" mantiene il nuovo stato (persistito)
  Osservabile ✅: 5 card categoria con Switch caricati + lo Switch "Email" riflette il nuovo stato + PUT .../preferences 2xx (Network) + toast "Salvato" visibile + **post-reload lo stato dello Switch persiste**
  Route: /notifications/preferences
  Utente: marco

Scenario U8-10 [Smoke]: Storico versioni RuleSpec (vista read-only utente)
  Given sono loggato come marco@meepleai.test
  When apro /versions?gameId={azulId} (azulId = id del gioco Azul dal seed)
  Then la pagina carica GET dello storico versioni del gioco
    And se esistono ≥1 versione vedo la lista "Versioni (N)" a sinistra e il pannello "Confronta Versioni" a destra
    And se NON esiste storico per il gioco vedo la pagina "Storico Versioni RuleSpec" con lista vuota (empty legittimo)
    And come utente User NON vedo il pulsante "Ripristina" (azione role-gated admin/editor)
  Osservabile ✅: heading "Storico Versioni RuleSpec" + toggle List/Timeline view + assenza del bottone "Ripristina" per ruolo User + nessun errore Console/Network
  Route: /versions
  Utente: marco
  Nota dati: senza ?gameId la pagina mostra il prompt "Specifica un gameId…" (anch'esso stato legittimo, ma lo scenario passa gameId per esercitare il caricamento storico).

Scenario U8-11 [Smoke]: Badge contatore notifiche (SSE) in topbar
  Given sono loggato come marco@meepleai.test
  When carico una qualsiasi pagina autenticata con la topbar (es. /dashboard)
  Then in topbar è presente il NotificationBell (icona campanella)
    And a livello Network parte GET /api/v1/notifications/unread-count (seed contatore)
    And si apre la connessione SSE GET /api/v1/notifications/stream (EventSource)
    And il badge mostra il conteggio non-letto corrente (o nessun badge se 0)
  Osservabile ✅: icona campanella visibile in topbar + GET .../unread-count 2xx + connessione a .../stream aperta (Network EventSource) + badge coerente col conteggio
  Route: /dashboard (topbar shell; hook useNotificationsCounter)
  Utente: marco

Scenario U8-13 [Smoke]: Pagina offline PWA (fallback)
  Given l'app è servita come PWA
    And navigo direttamente a /offline (o il service worker mi ci porta quando la rete è giù)
  When la pagina /offline si monta in stato offline
  Then vedo l'HeroGradient con icona WifiOff, titolo e sottotitolo "offline"
    And vedo le CTA "Riprova" (reload) e "Home" (→ /)
    And se ci sono dati PWA in cache vedo le 3 stat (Sessioni / Giochi in cache / Azioni in sospeso)
  Osservabile ✅: heading offline + icona WifiOff + CTA "Riprova"/"Home" presenti + (se cache) 3 stat + nessun errore JS (Console)
  Route: /offline
  Utente: qualsiasi (pagina di sistema)
  Nota: la pagina fa history.back() automaticamente se isOnline=true. In un browser ONLINE reindirizza subito indietro → per osservare lo stato serve la modalità offline (DevTools → Network → Offline). Se non producibile → ⚠️ blocked-env. Aggiunto in A-FINAL (route top-level rilevata dalla guardia di copertura).
```

---

## Auto-verifica

- **Copertura route**: tutte le 8 route U8 (7 del `_coverage-map.md` § U8 + `/offline` top-level aggiunta in A-FINAL) compaiono nella matrice con ≥1 scenario (dashboard→U8-01; profile→U8-02/03/04/08; profile/achievements→U8-05; notifications→U8-06/07/11; notifications/preferences→U8-09; versions→U8-10; offline→U8-13; `/n8n` skip (feature n8n in rimozione)). Nessun buco.
- **Osservabili**: ogni scenario dichiara un blocco `Osservabile ✅` con ≥1 marcatore verificabile a schermo o via Network (nessuna asserzione su testo LLM).
- **Happy path only**: nessuno scenario negativo/errore/edge; gli empty-state (notifiche, achievement, versioni) sono trattati come esiti legittimi del percorso di successo, coerentemente con lo stato del seed.
- **Marcatori dati**: l'unico scenario Flow che crea/muta dato persistente è U8-03 (rinomina profilo), che usa il marcatore `HP-TEST-2026-07-10`. U8-06/07/09 mutano stato di notifiche/preferenze dell'utente seed (read-state e flag preferenze) senza creare entità con titolo — reversibili ri-eseguendo il seed.
- **Conteggio**: 12 scenari (Flow: U8-03, U8-04, U8-06, U8-07, U8-08, U8-09 = 6 · Smoke: U8-01, U8-02, U8-05, U8-10, U8-11, U8-13 = 6). Route `/n8n` in skip (scenario U8-12 rimosso — feature n8n in rimozione).
