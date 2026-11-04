# Happy-Path — Prompt di fix per i finding (2026-07-12b)

> Prompt pronti da incollare in una sessione fresca per triagliare/fixare i cluster di bug trovati dall'happy-path testing program. Ogni prompt referenzia la sua issue GitHub + la riga di dettaglio in [`RESULTS.md`](./RESULTS.md).
>
> **Regola trasversale**: molti di questi bug hanno la stessa impronta — **mismatch schema-validation FE↔BE** (la risposta BE non è conforme allo schema Zod del client) o **endpoint mancanti/errati**. Vale la pena, prima di fixare uno per uno, verificare se un refactor recente ha desincronizzato gli schemi client dai DTO BE.

---

## (a) Cluster CRUD admin — issue [#2845](https://github.com/meepleAi-app/meepleai-monorepo/issues/2845)

```
Triaglia e fixa il cluster di bug CRUD admin descritto nella issue #2845 (finding #FF/#GG/#HH dell'happy-path testing).
Ambiente: make dev (full-stack) + seed-sp4 + login admin@meepleai.app (superadmin).

Usa systematic-debugging. I 3 bug e i loro punti d'attacco:

1. #FF — /admin/shared-games/categories: il dialog "Edit" categoria usa la semantica create ("Add category" → POST) invece di update (PUT /api/v1/admin/categories/{id}). Cerca il componente CategoriesTable + il dialog di edit; il submit deve chiamare PUT con l'id quando in modalità edit (probabile prop mode/isEdit non passata o handler sempre-POST). Verifica: rinominare una categoria NON deve creare una seconda riga (controlla via GET /api/v1/admin/categories).

2. #GG — /editor nega il superadmin. L'EditorAuthGuard/RequireRole={['Admin','Editor']} è una lista piatta che non include 'superadmin'. Includi 'superadmin' (o implementa una gerarchia ruoli superadmin ≥ admin ≥ editor). Cerca `RequireRole` / `EditorAuthGuard` in apps/web. Verifica: admin@meepleai.app (superadmin) deve poter aprire /editor.

3. #HH — CRUD admin shared-game rotto su 3 gambe:
   - CREATE (/admin/shared-games/new): POST crea il gioco lato BE ma la RISPOSTA fallisce la schema-validation FE ("Schema validation failed: Response validation fail") → no redirect + duplicati. Confronta lo schema Zod della risposta create con il DTO BE effettivo (campo mancante/nullable/tipo).
   - DETAIL (/admin/shared-games/{id}): GET /api/v1/admin/shared-games/{id} → 405 Method Not Allowed → "Failed to load game details". O manca la route GET admin (aggiungerla) o il FE deve usare l'endpoint corretto (es. /api/v1/shared-games/{id}).
   - DELETE: DELETE /api/v1/admin/shared-games/{id} → 202 ma il gioco resta nella lista. Verifica se è async (job) non riflesso o un no-op; la UI/lista deve rispecchiare la cancellazione (soft-delete IsDeleted + query filter).

Per ogni fix: TDD dove possibile, verifica E2E nel browser (crea/edita/elimina una entità HP-TEST e conferma via reload/GET), niente 2xx-come-prova. Apri PR verso main-dev, chiudi #2845.
```

---

## (b) Finding utente

### Proxy prefix-collision — issue [#2846](https://github.com/meepleAi-app/meepleai-monorepo/issues/2846) (#G/#DD/#EE)

```
Fixa la collisione di prefisso nel proxy descritta in #2846. In apps/web/src/proxy.ts, PROTECTED_ROUTES.some(r => pathname.startsWith(r)) tratta come protette le route pubbliche /library-public, /library/shared/{token}, e /games/{id} (perché startsWith('/library') / startsWith('/games')). Implementa un match boundary-aware (pathname === route || pathname.startsWith(route + '/')) + whitelist esplicita delle route pubbliche (/library-public, /library/shared/*). Chiarisci con il team se /games/{id} deve essere pubblico. Verifica da ospite (logout): /library-public e /library/shared/{token valido} NON devono redirigere a /login. Attenzione: potrebbe esistere già una PR #2812 su questo — verifica lo stato. Verifica anche via test proxy (apps/web/src/__tests__/proxy.test.ts).
```

### Play-record create data-loss — issue [#2847](https://github.com/meepleAi-app/meepleai-monorepo/issues/2847) (#BB)

```
Fixa il data-loss del create play-record (#2847). /play-records/new: dopo "Salva partita" il record persiste come "Planned" con players:[] e location:null nonostante i dati inseriti. Sospetto: l'auto-save "BOZZA SALVATA" salva un draft precoce e "Salva partita" lo promuove senza risincronizzare players/scores/location (o il payload del submit non include l'array players). Cerca SessionCreateForm + il flusso submit/draft. Verifica E2E: crea un play-record con 1 giocatore + score + location → GET /api/v1/play-records/{id} deve avere players + location + status corretto (non "Planned"/vuoto).
```

### sessions/history schema fail — issue [#2848](https://github.com/meepleAi-app/meepleai-monorepo/issues/2848) (#Z)

```
Fixa il mismatch schema di GET /api/v1/sessions/history (#2848). /players/{id}/sessions mostra "Schema validation failed: Response validation failed for /api/v1/sessions/history?limit=50" e /players/{id}/stats resta vuoto. Confronta lo schema Zod client di sessions/history con il DTO BE (campo nullable/tipo/shape). /toolkit/history (percorso diverso) funziona → il difetto è nel consumer sessions/history. Verifica E2E: /players/{id}/sessions rende la lista (o empty-state) + /players/{id}/stats rende le KPI.
```

### Notification preferences no-persist — issue [#2849](https://github.com/meepleAi-app/meepleai-monorepo/issues/2849) (#T)

```
Fixa la persistenza delle preferenze notifiche (#2849). PUT /api/v1/notifications/preferences → 204 ma il GET successivo ritorna il valore originale (no-op). Bug BE: mancata scrittura o HybridCache non invalidata. Cerca il command handler di update notification preferences + la cache. Verifica: PUT con emailOnDocumentReady:false → GET (e post-reload) deve ritornare false. Query DB notification_preferences per conferma.
```

### Gamebooks list 500 — issue [#2850](https://github.com/meepleAi-app/meepleai-monorepo/issues/2850) (#M)

```
Fixa GET /api/v1/gamebooks → 500 (#2850). Errore EF Core SelectExpression.ApplySetOperation (query con set-operation malformata). Chiamato dal dettaglio di ogni gioco → [API Error] in console. Cerca il query handler di gamebooks list + la set-operation (UNION/EXCEPT/Concat su IQueryable non traducibile). Verifica: GET /api/v1/gamebooks → 200 + il dettaglio gioco non logga [API Error].
```

### Toolkit private game 422 — issue [#2851](https://github.com/meepleAi-app/meepleai-monorepo/issues/2851) (#Q)

```
Fixa la creazione toolkit per private game (#2851). usePrivateToolkitEditor.createToolkit invia {privateGameId, name, overrides} a POST /api/v1/game-toolkits/ ma il BE esige GameId (422); aggiungendo gameId → 400 (ctor esige uno shared-game reale). Decidi: (opz A) il BE game-toolkits accetta privateGameId per i toolkit di private game; (opz B) il FE non offre la creazione toolkit per private game finché non supportato. Verifica E2E: creare un toolkit da un private game deve riuscire (opz A) o l'azione non deve essere offerta (opz B).
```

---

## (c) Cleanup HP-TEST — FATTO 2026-07-12b

Tutti gli artefatti HP-TEST creati durante la sessione sono stati eliminati:
- 2 shared-game admin (`1f7f5446`, `3b31290d`) — via DB (`DELETE FROM shared_games`, perché `DELETE /api/v1/admin/shared-games/{id}` → 202 no-op, vedi #HH/#2845).
- Agente `b883b2af` (HP-TEST Azul Tutor) — via DB (`knowledge_base.agent_definitions`).
- Campagna gamebook `625dc808` (Azul) — via DB (`session_tracking.gamebook_campaign_sessions`).
- Sessione live `7cda5c11` — via DB (`live_game_sessions`).
- Token condivisione libreria `96642a25…` — via DB (`library_share_links`).
- Play-record `b0835ae3`, categorie HP-TEST, game-night `62da6425` — già eliminati via API durante l'esecuzione.

Verifica finale: 0 righe `HP-TEST%` in `shared_games` e `knowledge_base.agent_definitions`.

---

## Batch fix FE ancora pendente (non issue-izzato)

Finding minori i18n/UI raggruppabili in una mini-PR: **#H1/#H2** (i18n game-detail houseRules/FAQ), **#J** (KB chunk limit 200>100 → 400), **#K** (/kb/[id] force-static redirect 404), **#CC** (edit play-record i18n leak `playRecords.edit.*`), **#O** (wishlist card titolo non risolto), **#AA** (card sessione/player id-non-risolto). Cluster CRUD-UI-scollegate **#L/#N/#P** (MeepleCard actions) = feature #1856 DEC-4 pending.
