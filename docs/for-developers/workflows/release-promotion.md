# Giro di rilascio: `main-dev` → `main-staging` → `main`

Come si promuove il codice di `main-dev` fino a `main`, scritto durante il giro del 2026-11-10 (#4972, #4977) e confrontato con il giro del 2026-11-04 (#4892, #4897). La strategia dei rami è in [ADR-054](../../for-claude/architecture/adr/adr-054-devops-multi-branch-strategy.md). Questa pagina descrive come si fa oggi, e nei punti in cui ADR-054 descrive un'infrastruttura che non c'è più (un deploy automatico su staging) vale questa.

> Convenzione: i numeri citati qui sono esiti di un giro preciso, con la data accanto. Per sapere quanti commit, test o rossi ci sono **adesso** usa il comando accanto, non il numero.

## In breve

1. Ramo `release/<data>-main-dev-promote` alla punta di `main-dev`, PR verso `main-staging`, merge commit.
2. Si legge la CI della PR **per nomi**, contro un run di controllo di `main-dev` sullo stesso SHA.
3. Ramo `release/<data>-prod-promote` alla punta di `main-staging` dopo il merge, PR verso `main`, merge commit con l'account che ha i permessi.
4. Dopo il merge su `main`, si sbloccano le issue di *contract* che aspettavano un rilascio (per esempio #4960).

Nessuno dei due merge avvia un deploy: non c'è un host di staging né di produzione (#4428).

## 1. Il primo salto: `main-dev` → `main-staging`

### Il ramo

Il ramo di rilascio coincide con la punta di `main-dev` e non ha commit propri. Si crea **sul remoto, via API**, senza toccare il checkout (i container dello stack locale montano file da lì, e non passano gli hook di push):

```bash
git fetch origin
SHA=$(git rev-parse origin/main-dev)
gh api repos/meepleAi-app/meepleai-monorepo/git/refs \
  -f ref=refs/heads/release/$(date -u +%Y-%m-%d)-main-dev-promote -f sha=$SHA
```

In alternativa, un ramo locale fast-forward: `git checkout -b release/<data>-main-dev-promote origin/main-dev && git push -u origin HEAD`.

Prima di aprire la PR, misura cosa entra e simula il merge:

```bash
git log --first-parent --oneline origin/main-staging..origin/main-dev   # cosa entra
git log --oneline origin/main-dev..origin/main-staging                  # cosa ha main-staging in più
git merge-tree --write-tree origin/main-staging origin/main-dev         # exit 0 = nessun conflitto
```

La seconda riga dovrebbe elencare solo i merge delle promozioni precedenti (più gli hotfix fatti direttamente su `main-staging`, se ce ne sono stati).

### La PR

```bash
gh pr create --base main-staging --head release/<data>-main-dev-promote \
  --title "release: main-dev → main-staging (<data>)" --body-file <file>
```

Il corpo dice: lo SHA di `main-dev` a cui punta il ramo, i comandi per misurare cosa entra, perché si promuove adesso (le issue che aspettano il rilascio), e lo stato della verifica. Modello: #4972.

**Merge commit, non squash.** `main-staging` porta i merge delle promozioni precedenti e gli eventuali hotfix fatti lì: solo un merge commit li preserva. Uno squash creerebbe un commit che `main-dev` non ha, e il salto successivo vedrebbe una storia divergente.

### Se `main-dev` avanza durante il giro

La CI della PR prova lo SHA del ramo, non la punta di `main-dev` di quel momento. Se nel frattempo su `main-dev` arrivano merge che vuoi includere, porta avanti il ramo (`gh api -X PATCH repos/meepleAi-app/meepleai-monorepo/git/refs/heads/release/<data>-main-dev-promote -f sha=<nuovo SHA>`) e rileggi la CI da capo. Altrimenti lasciali al giro successivo: è più semplice e il controllo resta valido.

## 2. Leggere la CI di una PR di rilascio

Sulla PR verso `main-staging` gira `ci.yml` completo, più gli E2E di backend (`backend-e2e-tests.yml`, che sulle PR verso `main-dev` **non** gira: #4032). Ci vuole più di un'ora. La PR non ha controlli obbligatori: lo stato `UNSTABLE` con dei rossi non blocca il merge, quindi il giudizio va dato a mano.

### Il criterio: nomi, non conteggi

Per ogni job rosso, confronta l'**insieme dei nomi** dei test falliti con quello di un run di controllo su `main-dev` **allo stesso SHA**. Un insieme uguale vuol dire nessuna regressione misurata; un nome solo dalla parte della PR è da indagare. Due conteggi uguali possono nascondere due test diversi.

Run di controllo degli E2E, da lanciare appena aperta la PR, così i due girano in parallelo:

```bash
gh workflow run backend-e2e-tests.yml --ref main-dev
```

Confronto (stessa forma per qualunque job che stampa i `Failed <nome> [`):

```bash
for r in <run della PR> <run di controllo>; do
  gh run view $r --log > e2e-$r.log
  grep -a -o -E "Failed [A-Za-z0-9_.]+ \[" e2e-$r.log | sed 's/ \[$//' | sort -u > e2e-$r.failed
  grep -a -A4 "Total tests:" e2e-$r.log | sed -E 's/^.*Z //'
done
comm -23 e2e-<PR>.failed e2e-<controllo>.failed   # solo nella PR: da indagare
comm -13 e2e-<PR>.failed e2e-<controllo>.failed   # solo nel controllo
```

Confronta anche **superati e saltati**, non solo i falliti: un `Passed` che scende sono test che non girano più. Giro del 2026-11-10: PR e controllo con gli stessi totali (222 test, 194 superati, 27 falliti, 1 saltato) e nessun nome da una parte sola. Gli E2E di `main-dev` sono rossi da settimane (#4032): senza il controllo, la PR sembrerebbe la causa di tutto.

Per le integrazioni di backend il controllo è il **Dev Async** di `main-dev` sullo stesso SHA. Gli shard hanno nomi quasi uguali (`Backend - Integration (Core)` in `ci.yml`, `Backend Integration (Core)` nel Dev Async), ma i due workflow trattano un test fallito in modo diverso: `ci.yml` fa fallire il job, il Dev Async lo lascia `success`. Quindi i nomi si leggono dal log, mai dall'esito del job. Giro del 2026-11-10: lo shard Core era rosso sulla PR e verde nel Dev Async, con gli stessi due test falliti e gli stessi totali da entrambe le parti (1122 test, 1074 superati, 2 falliti, 46 saltati). Nessuna regressione.

Da #4329 gli shard **si corrispondono uno a uno**. `ci.yml` e `dev-async.yml` hanno la stessa matrice: stessi nomi, stesso ordine, stessi `filter_extra`, e lo pretende `infra/scripts/tests/integration-shard-matrix.bats`. Quindi lo shard X della PR si confronta con lo shard X del controllo. Prima di #4329 non era così. Lo shard `KnowledgeBase` di `ci.yml` conteneva KnowledgeBase, DocumentProcessing e Authentication, che nel Dev Async erano già tre shard (`KnowledgeBase`, `DocProcessing`, `AuthIntegration`), e si confrontava con l'**unione** dei tre. Giro del 2026-11-10, con la matrice di allora: cinque falliti sulla PR, gli stessi cinque nello shard DocProcessing del controllo, e la somma dei tre shard del controllo uguale ai totali della PR (1434 test, 1416 superati, 5 falliti, 13 saltati).

Finché il run è in corso, `gh run view --job <id> --log` restituisce un log **vuoto**, non un errore. Il log di un job già finito, dentro un run ancora in corso, si legge dall'API:

```bash
gh run view <run> --json jobs --jq '.jobs[] | select(.name=="Backend - Integration (Core)") | .databaseId'
gh api repos/meepleAi-app/meepleai-monorepo/actions/jobs/<job>/logs > core.log
grep -a -E "^\S+ +Failed [A-Za-z0-9_.]+ \[|Failed! +-|Passed! +-" core.log
```

### Il commento del bot

Il bot dei gate ([`release-gate-bot.md`](../operations/release-gate-bot.md), #4937) commenta la PR con una tabella dei check rossi e la loro classificazione secondo [`.github/release-gates.yml`](../../../.github/release-gates.yml). Si aggiorna quando finisce un workflow sulla testa del ramo, e finché qualcosa gira scrive «⏳ N checks still running — no verdict until they complete» invece del verdetto. È il **primo indice** dei rossi, non il verdetto: classifica per nome di check, non sa se un rosso è nuovo. Il confronto dei nomi resta da fare.

Sul primo salto la testa del ramo è **lo stesso commit** della punta di `main-dev`, e i check-run appartengono al commit, non al ramo: il bot legge anche quelli dei run di `main-dev` su quel commit (il Dev Async, il run di controllo degli E2E). Per questo nella sua tabella possono comparire righe doppie (due `Backend E2E Tests`) e job che non sono della PR (`Backend E2E Test Results`, del Dev Async). Fino a #4974 (2026-11-10) su questo salto il bot non commentava affatto: cercava la PR dal commit, e per un commit di `main-dev` GitHub restituisce solo la PR già unita che lo ha introdotto.

### I rossi per costruzione

Questi sono rossi qualunque cosa contenga la PR. Verificali uno per uno con il link accanto, perché l'elenco invecchia:

| check | perché | finché |
|---|---|---|
| `validate-source-branch` (Branch Policy) | pretende `main-dev` come unico source di `main-staging`, e il source è `release/*` | finché il workflow non ammette `release/*`; è advisory |
| `Backend - Integration (DocProcessing)` di `ci.yml` | porta i test di DocumentProcessing rossi anche su `main-dev`: sono i falliti di base, che fino a #4329 stavano nello shard `KnowledgeBase` | #4996. **Confronta i NOMI** dei falliti con lo shard `DocProcessing` del controllo |
| `A11y unit smoke` (Staging Gate) | lo step viene annullato dopo pochi minuti (timeout noto) | — |
| `Frontend - Lint` | gate DS-17-1, copertura 0/3, rosso dichiarato dal commit #4048 | #4047 |
| `CI Success`, `Backend E2E Quality Gate`, `E2E Quality Gate` | aggregati: rossi quando lo è un job a monte | — |

Fino a #4329 la tabella aveva anche `Backend - Integration (Games)` di `ci.yml` (riferimento storico). Lo shard superava spesso il limite di 75 minuti del run (`Aborting test run: test run timeout`), con zero falliti ed esito `Aborted`, e la parte non eseguita la provava solo il controllo. #4329 l'ha diviso in `Catalog` e `GamesAdmin`, come nel Dev Async. Da allora ogni shard di `ci.yml` stampa nel log la quota del `TestSessionTimeout` che ha usato (riga `[<shard>] sessione: …`) e avvisa dall'80%.

## 3. Il secondo salto: `main-staging` → `main`

Dopo il merge della prima PR, crea il ramo alla nuova punta di `main-staging`, sempre via API:

```bash
git fetch origin
SHA=$(git rev-parse origin/main-staging)
gh api repos/meepleAi-app/meepleai-monorepo/git/refs \
  -f ref=refs/heads/release/<data>-prod-promote -f sha=$SHA
git merge-tree --write-tree origin/main origin/main-staging   # exit 0 = nessun conflitto
gh pr create --base main --head release/<data>-prod-promote \
  --title "release: main-staging → main (<data>)" --body-file <file>
```

Il contenuto è lo stesso della prima PR. Sulla PR verso `main` girano di nuovo `ci.yml` e gli E2E di backend: per quelli il confronto dei nomi si fa con la prima PR, che a sua volta era confrontata con il controllo su `main-dev`. Giro del 2026-11-10: stessi nomi e stessi totali per Backend E2E, Core e KnowledgeBase. Anche qui **merge commit**. Il titolo non lo dice: #4897 ha il titolo della PR, #4977 quello predefinito di GitHub, e sono entrambi merge commit. Quello che conta sono i due genitori:

```bash
git log -1 --format='%h parents=%p %s' origin/main
```

### I workflow che girano solo sulle PR verso `main`

`api-smoke.yml`, `test-performance.yml` (Lighthouse, K6), `test-e2e.yml` (Playwright) e `security-pentest.yml` hanno `pull_request` verso `main`. Su `main-dev` ne girano due, e solo per una parte:

- K6 (`test-performance.yml`) con un cron notturno, mentre Lighthouse lì viene saltato. Da #4978 K6 gira anche sulle PR verso `main-dev` che toccano `test-performance.yml` o `tests/k6/`;
- `security-pentest.yml` con un cron settimanale.

Smoke e Playwright non hanno un controllo su `main-dev`. Il confronto si fa con **la PR verso `main` del giro precedente**, e dove c'è con il run notturno, sulla causa del rosso (la riga di errore del log), non sul solo esito. Per K6 si legge comunque il job, non il run: fino a #4978 il run notturno risultava `success` anche con il job K6 rosso.

```bash
gh run list --workflow test-performance.yml --event schedule --limit 3
gh run view <run> --json jobs --jq '.jobs[] | "\(.conclusion)\t\(.name)"'
```

Giro del 2026-11-10, contro #4897:

| check | causa, uguale nei due giri salvo dove detto | issue |
|---|---|---|
| `smoke (ubuntu-latest)` | `make dev-core`: `infra/env/web.env.dev not found` | #4978 |
| `smoke (windows-latest)` | Bruno: `You can run only at the root of a collection` | #4978 |
| `Lighthouse CI` | l'artefatto di build è vuoto (`No files were found with the provided path: apps/web/.next/`), quindi `Artifact not found` | #4978 |
| `K6 Performance Tests` | **diversa**: su #4977 l'installazione di k6 si ferma su `NO_PUBKEY C780D0BDB1A69C86`; su #4897 lo step dei test era scaduto (`The action 'Run k6 performance tests' has timed out after 30 minutes`). Nello stesso run di #4897 ogni login dell'utente di carico rispondeva 400, un difetto concomitante che `\|\| true` nascondeva. Lo stesso `NO_PUBKEY` compare nel run notturno su `main-dev` dello stesso giorno, quindi non dipende dal contenuto | #4978 |
| `E2E Tests (Shard N)` | uno shard cade sulla build con `Can't resolve '@vercel/turbopack-next/internal/font/google/font'`: non dipende dal diff, e che la causa sia il download dei font non è dimostrato (#4997 §1). Gli altri girano con un worker e non arrivano in fondo: su #4897 GitHub li annulla a 360 minuti, il run di #4977 (42774776452) è stato annullato dopo circa 3 h 25 min. Quanti test esegue uno shard si legge nel suo log: `gh run view --job <job_id> --log \| grep -m1 'Running .* tests using'` | #4123, #4997 |
| `2FA Security Penetration Tests (OWASP)` | verde | — |

Un rosso intermittente può comparire anche su un workflow che gira su ogni PR. `cover-r2-strict` era verde su #4972, è caduto su #4977 sullo stesso errore della build dei font ed è tornato verde al rilancio sullo stesso SHA (#4997, voce del registro da #4978).

Non aspettare la fine degli shard di `test-e2e.yml`: non arrivano in fondo. Classificati gli altri check, unisci e annulla quel run (`gh run cancel <run>`), che altrimenti tiene occupati i runner degli shard fino alle 6 ore. Un rosso con una causa diversa da quella del giro precedente si legge fino in fondo, come K6 qui. Se la nuova causa sta prima del codice (installazione, rete, artefatti), non è una regressione del rilascio, ma va scritta nella issue che segue quel workflow.

### L'account

`main` e `main-staging` hanno una branch protection che vieta force push e cancellazione, e nient'altro: nessun check obbligatorio, nessuna review richiesta, nessuna restrizione su chi fa push, nessun ruleset (verificato il 2026-11-10; il 2026-11-08 la protezione di `main` rispondeva ancora 404). Per rileggerla:

```bash
gh api repos/meepleAi-app/meepleai-monorepo/branches/main/protection \
  --jq '{checks: .required_status_checks, reviews: .required_pull_request_reviews, restrictions: .restrictions, force: .allow_force_pushes.enabled, deletions: .allow_deletions.enabled}'
gh api repos/meepleAi-app/meepleai-monorepo/rules/branches/main --jq length
```

Il merge su `main` è comunque riuscito solo con l'account che ha i permessi di scrittura sul repository. Con l'altro, il 2026-11-08, la risposta è stata «does not have the correct permissions to execute MergePullRequest». Prima del merge:

```bash
gh auth status                    # quale account è attivo
gh auth switch -u meepleAi-app    # se serve
gh pr merge <n> --merge --match-head-commit <SHA del ramo>
```

Mai `--admin`: non serve, e scavalcherebbe una protezione che un giorno ci sarà.

## 4. Cosa parte dopo i merge

Prima di scrivere in una PR che il merge «avvierà un deploy», leggi lo stato dei workflow: alcuni file di deploy risultano attivi ma non hanno nulla da fare.

```bash
# stato di ogni workflow di deploy: dall'API, non da `gh workflow list --all --json`,
# che i workflow disattivati non li restituisce (provato il 2026-11-10)
gh api repos/meepleAi-app/meepleai-monorepo/actions/workflows --paginate \
  --jq '.workflows[] | select(.path|test("deploy|staging")) | "\(.state)\t\(.path)"'
gh run list --branch main-staging --limit 10    # cosa è partito davvero
gh run list --branch main --limit 10
```

Stato al 2026-11-10:

- `deploy-staging.yml` è `disabled_manually` (#4428): il push su `main-staging` non deploya.
- `deploy-production.yml` risulta `active` per GitHub, ma nei rami esiste solo `deploy-production.yml.disabled`: è un residuo, e il merge su `main` non deploya.
- Dopo il merge su `main-staging` partono `CI`, `Security Scan` e `Seed Snapshot Bake (CI smoke — ci.yml manifest)`, sul commit del merge. Lo stesso nei giri del 2026-11-04 e del 2026-11-10.
- Dopo il merge su `main` partono solo `Security Scan` e `Validate Workflow Cache Patterns`, sul commit del merge. Lo stesso il 2026-11-08 (#4897) e il 2026-11-10 (#4977).

## 5. Dopo il rilascio

- Cancella i due rami `release/*` (l'auto-delete li toglie al merge; controlla con `git ls-remote origin 'refs/heads/release/*'`).
- Le issue di *contract* che aspettavano un rilascio ora possono procedere: un `DROP TABLE` o `DROP COLUMN` è sicuro solo quando la versione su `main` non legge più quella tabella o colonna. Il [Migration Safety Gate](../operations/rollback-runbook.md) lo chiede con una riga `-- safe:` che cita il fatto. Esempio: #4960 (`DROP TABLE rule_disputes`), unita su `main-dev` subito dopo #4977, che aveva portato #4936 su `main`.
- Controlla che il contract tolga **solo** ciò che la versione su `main` non usa più. Il piano di #4960 toglieva anche la colonna `game_memories.last_processed_event_id`, che la versione rilasciata mappava nell'entità. Una colonna mappata entra nella SELECT di ogni lettura, quindi un rollback dopo il `DROP COLUMN` fallirebbe su tutte le letture di quella tabella, non solo sul codice ritirato.
- **Vale anche per una proprietà ombra.** Resta nel modello EF, ed EF la mette nella SELECT con e senza `AsNoTracking()`: in #4973 è stato misurato con `ToQueryString()`. Con un ambiente deployato, il `DROP COLUMN` arriva quindi solo dopo un rilascio in cui la colonna non è più nel modello, nemmeno come proprietà ombra. Senza un ambiente deployato (#4428) non c'è una versione vecchia da far girare sullo schema nuovo, e #4973 ha tolto proprietà ombra e colonna nella stessa consegna.
