# Prompt di kickoff — risolvere le issue dell'happy-path testing (sessione nuova)

> Copia il blocco qui sotto in una sessione fresca per iniziare a risolvere le 7 issue aperte dal testing (2026-07-12b). Le issue sono #2845–#2851. I prompt per-issue con i punti d'attacco precisi sono in [`_FIX-PROMPTS.md`](./_FIX-PROMPTS.md); i dettagli dei finding in [`RESULTS.md`](./RESULTS.md).

---

Devi risolvere i bug trovati dall'happy-path testing program di MeepleAI. Sono 7 issue GitHub aperte (repo `meepleAi-app/meepleai-monorepo`), branch di lavoro da `main-dev`.

## Leggi prima (in ordine)
1. `docs/for-developers/testing/happy-path/_FIX-PROMPTS.md` — prompt per-issue con punti d'attacco file/endpoint precisi.
2. `docs/for-developers/testing/happy-path/RESULTS.md` § "Issue aperte" — dettaglio dei finding #FF..#Q con ripro.
3. Le issue GitHub: #2845 (admin CRUD), #2846 (proxy), #2847 (#BB), #2848 (#Z), #2849 (#T), #2850 (#M), #2851 (#Q). `gh issue view <n>`.

## Ipotesi trasversale da verificare PER PRIMA (spike, ~30min)
Molti bug hanno la **stessa impronta: mismatch schema-validation FE↔BE** — la risposta del BE non è conforme allo schema Zod del client, quindi il FE mostra "Schema validation failed" / "Failed to load" pur avendo il BE risposto 2xx. Colpisce **#HH** (admin shared-game create+detail), **#Z** (sessions/history), e va escluso su **#BB** (play-record).
→ **Spike iniziale**: cerca in `apps/web/src/lib/api/schemas/**` gli schemi Zod di risposta per shared-games (admin), sessions/history, play-records; confrontali con i DTO/response BE effettivi (`apps/api/src/Api/.../DTOs` + le route). Se un refactor recente ha desincronizzato schema↔DTO (campo aggiunto/nullable/rinominato/rimosso), potresti sistemare più issue con un fix mirato al layer schema invece che uno per uno. Documenta cosa trovi prima di procedere.

## Ambiente
- `make dev` (full-stack AI) + `make seed-sp4` (già eseguibili; vedi CLAUDE.md). Verifica con `docker ps` che api/web siano healthy.
- Login: **admin** `admin@meepleai.app` (role superadmin, per gli scenari admin — l'utente digita la password, non tu) e **marco** `marco@meepleai.test` / `Sp4-Seed-Pwd!2026` (per gli scenari utente — comunica le cred, non digitarle nei form).
- Verifica browser via l'estensione Claude-in-Chrome (skill `claude-in-chrome`). Se il CDP freeza su pagine pesanti → crea un **tab fresco** (`tabs_create_mcp`) per resettare il renderer.

## Workflow per ogni issue/cluster
1. `git checkout main-dev && git pull --ff-only` → `git checkout -b fix/issue-<n>-<desc>`.
2. **systematic-debugging** + **TDD** dove possibile (unit/integration; per il BE .NET usa Testcontainers come da backend-testing-patterns.md).
3. **Verifica E2E reale** nel browser: esegui l'operazione (create/edit/delete/toggle) e conferma via **reload/GET** che il dato è persistito — il `2xx` NON è prova (regola verifica scritture dell'happy-path). Per gli scenari rotti, riproduci prima il bug, poi conferma il fix.
4. `git commit` (usa `git commit -F <file>` se il messaggio ha `#`/`{}`; termina con il trailer Co-Authored-By di CLAUDE.md). **Il pre-push hook fa build FE+BE (~5min) ma completa**; push in background se serve.
5. PR verso **main-dev** con `Closes #<n>`; merge (se l'utente conferma "no ci", `gh pr merge --admin --squash --delete-branch`); cleanup branch/worktree.

## Ordine raccomandato (quick wins → cluster)
1. **#2845 / #GG** (editor nega superadmin) — 1-riga sul guard `RequireRole` (includi 'superadmin' o gerarchia). Quick win.
2. **#2850 / #M** (gamebooks 500) — fix EF `ApplySetOperation` nel query handler. Quick win, alto rumore console.
3. **#2849 / #T** (notif prefs PUT 204 no-op) — command handler update + invalidazione HybridCache.
4. **#2851 / #Q** (toolkit private 422) — decisione BE-accetta-privateGameId vs FE-nasconde-azione.
5. **#2846 / proxy** (#G/#DD/#EE) — match boundary-aware in `apps/web/src/proxy.ts` + whitelist route pubbliche; verifica interazione con la (eventuale) PR #2812.
6. **Cluster schema-validation** dopo lo spike: **#2848 / #Z** (sessions/history), poi **#2845 / #HH** (admin shared-game create+detail 405+delete 202), poi **#2847 / #BB** (play-record create draft-sync).
7. **#2845 / #FF** (edit categoria → duplicato) — wira il dialog edit al `PUT /admin/categories/{id}` con l'id.

Fai una issue/PR alla volta (o raggruppa #FF/#GG/#HH sotto #2845 se preferisci un'unica PR per l'umbrella admin). Aggiorna lo stato delle issue su GitHub man mano. Alla fine, se vuoi, ri-esegui gli scenari happy-path corrispondenti per confermare che passano (RESULTS.md ha gli ID: A3-18/19 + U4-14, U6-13/21/22/28, U8-09, U3-02/09, giro ospite).
