# Happy-Path Testing — Prompt di ripresa (Fase B)

> Copia il blocco sotto in una sessione fresca per riprendere l'esecuzione browser-reale. Aggiornato: **2026-07-12b** (fine sessione: #S/#W merged in main-dev, #R fixato+shipped PR #2838, U4-05 + U6 + U7 parziale eseguiti). Branch `feature/happy-path-testing-program`.

---

Riprendi la **FASE B** del programma happy-path testing di MeepleAI (esecuzione browser reale). NON ripartire da zero.

## Leggi per il contesto (in ordine)
1. Memoria `programma-happy-path-testing` (topic file; auto-caricata ma rileggila — ha lo stato dettagliato + le lezioni tooling).
2. `docs/for-developers/testing/happy-path/RESULTS.md` — riepilogo + finding #A..#W.
3. Il catalogo dell'area che eseguirai (`U4-chat-rag.md`/`U5-game-night.md`/`U6-sessioni-scoring.md`/`U7-toolkit-gamebook.md`/`A1..A5-*.md`).

## Stato attuale (checkpoint 2026-07-12b — fine)
- **Totale programma: 91✅ / 26❌ / 15⚠️ / 136⏭️** (268 scenari; i conteggi per-area quadrano a 268).
- **Giro admin AVVIATO** (login admin@meepleai.app **superadmin**): landing A1-A5 smoke-OK, ma 🔴 **CRUD admin SISTEMICAMENTE ROTTO**: A3-19 ❌ #FF (edit categoria → duplicato), U4-14 ❌ #GG (editor nega superadmin), A3-02/03/18 ❌ #HH (shared-game create schema-fail+duplicati / detail GET 405 / delete 202-no-op). Solo A3-01/09 (liste/smoke) passano. ⚠️ 2 giochi HP-TEST (`1f7f5446`+`3b31290d`) non eliminati (delete 202 no-op) → cleanup manuale.
- 🔴 **Admin RISTRUTTURATO** (`/admin/shared-games`→`/admin/content`) → catalogo A1-A5 da **ri-mappare** (mapping route reali nella memoria `programma-happy-path-testing`). Il deep pass admin va fatto **sapendo che il CRUD è rotto** — probabile che molti Flow admin falliscano con lo stesso pattern schema-validation/endpoint.
- Completi: U1, U2, U3(Giro2), **U8**, **U4** (10✅, U4-14..17 editor=admin ⏭️), **U5** (4✅), **U7** (15✅/3⚠️ — gamebook CRUD completo).
- **U6** ampio (14✅/6❌/6⚠️/3⏭️): list/create-wizard/live-shell G1/play-records/players/note OK; scoring blocked-env; player sessions/stats rotti (#Z); **play-record CRUD ❌** (#BB create data-loss + #CC edit i18n); launcher blocked #X.
- **Giro ospite fatto**: U6-23 /join ✅; ma cluster **proxy prefix-collision** (#G `/library-public`, #DD `/library/shared/{token}`, #EE `/games/{id}`) → route "pubbliche" redirigono ospiti a /login. **marco è SLOGGATO**.
- Branch testing: `feature/happy-path-testing-program` (NON mergeato). Ha il **merge di origin/main-dev** + i commit doc (fino a `8d53439f1`).

## 🔧 Fix — TUTTI SHIPPED (merged in main-dev)
- **#V + #W** (citazioni chat RAG + PDF): PR **#2833** MERGED (squash `2c54205d`).
- **#S** (detail GN Published-vuota crash): PR **#2834** MERGED (squash `b4553489`). **✅ VERIFICATO E2E 2026-07-12b** (rebuild web da testing-merged): detail "Serata da Marco" Published-vuota → empty-state, no error boundary, no `TypeError`.
- **#R** (publish GN 500 con pre-invitato): **root cause = RSVP DUPLICATI in `Publish()`** (non ciclo FK cross-tabella come ipotizzato prima; due `GameNightRsvpEntity` con stesso unique index → `Multigraph.ThrowCycle`). Fix = dedup guard in `GameNightEvent.Publish()` (mirror di `PreInvite()`). PR **#2838** MERGED (`--admin` squash, no CI), issue **#2835 CLOSED**. TDD: 3 test dominio + 1 integration Testcontainers (pre-fix repro confermato). ⚠️ **NON ancora nel build api in esecuzione** (che ha il vecchio codice) → verifica E2E di U5-04 (publish con pre-invitato → 204) richiede rebuild api.

## Bug residui da triagliare (finding, non fixati)
- **U6 nuovi**: **#X** (sessione live id-duality GameSession↔LiveGameSession: detail `/sessions/{id}` usa endpoint legacy → 404 su live-session valida; + `/live-sessions/{id}/start` → **503** chat agent "Impossibile avviare l'assistente"); **#Y** (nessuna CTA "Termina sessione" nella live shell G1 → complete→play-record irraggiungibile); **#Z** (`/api/v1/sessions/history` fallisce schema-validation FE → `/players/{id}/sessions` errore + `/players/{id}/stats` vuoto; probabile impatto anche U7-03 toolkit history); **#AA** (id-non-risolti a nome: card sessione = gameId, player = userId).
- **Da sessioni precedenti**: #T (pref-notifiche PUT 204 no-persist), #U (riattiva-agente 404), #M (gamebooks list 500), #Q (toolkit-private 422), cluster #L/#N/#P (CRUD MeepleCard scollegate), #O (wishlist titolo), #H1/#H2/#J/#K (batch FE).

## Ambiente — attualmente ATTIVO (full-stack AI)
`make dev` completo (AI) up + `seed-sp4` **completo**. Web rebuiltato dal branch testing-merged (ha #S/#W/#V). **api in esecuzione = pre-#R** (per U5-04 E2E serve rebuild api dal main-dev current). 13 agenti attivi, 4 GN Published-vuote, Azul KB `513e4041` Ready (46 chunk), gioco Azul `62066e49`. marco loggato. Sessione HP-TEST `7cda5c11` + agente HP-TEST `b883b2af` creati (da pulire). ⚠️ **CDP freeze intermittenti** su pagine pesanti (achievements, play-records/new, gamebook play) — recuperano via navigazione ma rallentano; valutare **restart browser/estensione**.

## Da dove ripartire (raccomandazione) — il grosso rimasto è l'ADMIN DEEP PASS
1. **Admin deep pass A1-A5 + A3-02..20** (il chunk più grande): il catalogo A usa route stale → **ri-mappare alle route reali** (mapping completo nella memoria `programma-happy-path-testing` § "Admin route mapping REALE"). Admin già loggato (superadmin). Per U4-14..17 editor serve un utente role **Admin/Editor puro** (superadmin è negato da #GG). Priorità: cicli CRUD (A3-18 shared-game, A3-20 phases) + Flow (import/wizard/rag-setup/agent-test con AI) + i sub-Smoke non ancora fatti.
2. **U1 email-gated** (mailpit `localhost:8025`): request-access → approva → invito → setup e2e; conferma reset password. Re-login qualsiasi utente.
3. (Opz.) Rebuild api dal main-dev current → **U5-04 E2E** (publish GN con pre-invitato → 204) + U5-05/06/08/15.
4. **U6-24..27 token-guest** (marco genera token: session join-code, guest-join token, GN invite, play-record share → poi logout → guest li usa). U6-03 (serve sara), U6-09/10.
5. Cluster proxy (#G/#DD/#EE) + batch-fix FE (#H1/#H2/#J/#K) + triage BE (#X/#Y/#Z/#BB/#CC/#M/#Q/#T/#U).
6. Cleanup HP-TEST: sessione live `7cda5c11`, agente `b883b2af`, campagna gamebook `625dc808` (Azul), token libreria `96642a25…`.
- ⚠️ **Se CDP freeza** su pagine pesanti: crea un **tab fresco** (`tabs_create_mcp`) → resetta il renderer. Dopo ~20 nav anche il tab fresco degrada → altro tab fresco.

## 🔴 REGOLA DI VERIFICA SCRITTURE (applicare SEMPRE)
Per OGNI operazione che modifica un dato (create/edit/delete/toggle/publish/RSVP/salva-pref) NON fermarsi al 2xx: verificare (1) UI produce l'effetto atteso + (2) dato **realmente persistito** via **reload/GET**. Cfr. #T (PUT 204 no-op), #B/#D (emailSent falso positivo).

## Lezioni tooling (critiche)
- **Click React**: `element.click()` via `javascript_tool` (il click CDP `computer` non ingaggia gli handler). **Input React**: native value-setter + `dispatchEvent(new Event('input',{bubbles:true}))` (il `type` CDP non scrive).
- `javascript_tool`: NO IIFE async; usa top-level await + espressione finale `JSON.stringify(out)`. Screenshot CDP glitcha su animazioni → DOM via `javascript_tool` è autorevole.
- `read_network`: tracking parte alla 1a chiamata; la richiesta va fatta DOPO. Le richieste vecchie restano in cache (filtra per id/URL). Console: pattern obbligatorio; rumore baseline "message channel closed" = estensioni.
- **Browser fetch bloccato** su `/download` con credentials ("Cookie/query string data") → verifica via DB o read_network, non fetch.
- **Git push HTTPS si APPENDE** (non-interattivo) → killarlo lascia lock git: cleanup `Get-Process git,ssh | Stop-Process -Force` + rimuovi `.git/index.lock`; poi push MANUALE utente (`! git push`).
- **Commit sul testing branch** falliscono col pre-commit typecheck FE per `.next/types` **stale** (route main-dev vs testing più vecchio) → `Remove-Item apps/web/.next/types -Recurse -Force` PRIMA di ogni commit.
- **`git commit -m @'…'@`** in PowerShell con `/`, `{}`, backtick nel messaggio si ROMPE → usare **`git commit -F <file>`** (scrivi il messaggio con Write in scratchpad).
- **Rebuild `web` o `api`** via `docker compose up -d --build <svc>` ricrea ANCHE l'altro (image build) → api unhealthy-transitoria da PdfSeeder (~140s), attendi healthy.
- API GN: `POST /game-nights` (create) accetta `{title, scheduledAt, location, gameIds[], invitedUserIds[]}`; ritorna l'id come **stringa JSON nel body + header `Location`** (NON `{id}`). `POST /game-nights/{id}/invite` richiede GN **Published** (409 su Draft). `/api/v1/game-nights` (lista) ritorna solo Published. `DELETE /game-nights/{id}` → 204.
- Azul: gioco `62066e49-8d0e-4265-a463-1e2c85af80f6`, doc reale `513e4041-…` (vector_documents.Id=`853f6dcc-…`, PdfDocumentId=`513e4041-…`). sara userId `ae1b7195-d947-4442-9072-b375dcdd0d14`.
- **LOGIN**: non digitare password. Credenziali seed marco `marco@meepleai.test` / `Sp4-Seed-Pwd!2026` (common.sh:115) — comunicale, non digitarle. Admin da `infra/secrets/admin.secret`.
- DATI TEST: marcatore `HP-TEST-<data>`; delete solo su HP-TEST propri.

## Comandi ambiente (PowerShell per docker/git; Bash per POSIX)
- DB query: `docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -c "…"` (colonne PascalCase quotate).
- Up AI completo: `make -C infra dev` (⚠️ al 1° up api unhealthy-transitoria PdfSeeder → web non parte → **ri-esegui `make -C infra dev`** dopo che api è healthy). Seed: `make -C infra seed-sp4` (lento, embedding CPU-bound).
- Wipe: `docker compose -p meepleai --project-directory infra -f infra/docker-compose.yml -f infra/compose.dev.yml --profile ai --profile monitoring --profile automation --profile proxy --profile storage down -v --remove-orphans`.
- Attendi healthy: `for ($i=1;$i -le 48;$i++){ $s=docker inspect -f "{{.State.Health.Status}}" meepleai-api; if($s -eq 'healthy'){break}; Start-Sleep 5 }`.
- Rebuild solo un servizio: `docker compose -p meepleai --project-directory infra -f infra/docker-compose.yml -f infra/compose.dev.yml up -d --build <web|api>`.
- Logs: `docker logs meepleai-api --tail 80`. Mailpit UI: http://localhost:8025 (profilo automation, già up).

## Stato aperto (non rifare)
- Fix #V/#W/#S/#R tutti **MERGED** (PR #2833/#2834/#2838). Branch locali eliminati, worktree rimosso, issue #2835 chiusa.
- PR aperte da sessioni precedenti (verificare stato): #2805 (invito), #2809 (reset), #2812 (proxy), #2818 (wishlist redirect).
- Ripristinare a fine programma intero: `email.secret`→Gmail, flag `web.env.dev`, config originale.

Comincia leggendo memoria + RESULTS + il catalogo dell'area scelta, verifica l'ambiente (`docker ps`, `/auth/me` nel browser) e proponi il primo scenario. Buon punto d'attacco: push branch fix → verifica E2E #S → fix #R → U6/U7/U4-05.
