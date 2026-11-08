# Prompt di ripresa — cluster NoTracking (#3866 → #3888) e code residuo

> Incolla il contenuto di questo file come primo messaggio in una sessione nuova.

---

Riprendi il cluster iniziato da #3866. **Quattro issue sono chiuse e mergiate in `main-dev`**;
restano due code, entrambe già pushate o aperte. Sotto trovi lo stato esatto, cosa fare, e — più
importante — **il metodo e le trappole che hanno fatto perdere ore**, perché sono tutte ripetibili.

## Stato: cosa è già in `main-dev`

| commit | issue | contenuto |
|---|---|---|
| `0dc727f34` | #3866 | parità NoTracking strutturale (in `OnConfiguring`, **non** nel costruttore) + triage di 117 integration → 21 difetti di produzione |
| `bcaf941f7` | #3882 | 22 write-path senza `.AsTracking()` — fra cui **il reset password che non resettava niente** |
| `680f9682b` | #3883 | 6 rossi di Games, fra cui `GameCoreData` che rifiutava il conteggio giocatori `(0,0)` benedetto dallo schema |
| `65aea8d9b` | #3886 | le due pipeline cover R2 di `SharedGameCatalog` lanciavano alla **risoluzione DI** → 500 su ogni richiesta senza S3 |

## Cosa resta da fare

### 1. `feature/issue-3886-cover-null-branches` — pushato, gate lanciato, PR da aprire

Commit `56694a870`. Fissa i due rami di degradazione introdotti da #3888, che **nessun test
esercitava**: la modifica ha passato il gate perché il gate non li raggiunge (vivono solo in un
ambiente local-storage).

- `EnrichCatalogCoverCommandHandler` senza pipeline: non lancia, fallisce con `FailReasonR2Upload`,
  non persiste stato a metà. L'harness prende `withR2Pipeline: false`.
- `BggCoverDownloader` senza pipeline: torna null **e non spende una richiesta HTTP** verso BGG
  (mock `Strict`).

Verificato in locale: `EnrichCatalogCover` 19/19, `BggCoverDownloader` 8/8, `Category=Unit`
22418/22418.

**Da fare**: attendere il gate `33259244323`, confronto per nome, aprire PR verso `main-dev`,
mergiare, cleanup.

### 2. Verificare il gate post-merge di `65aea8d9b` (run `33257975647`)

Era in corso a fine sessione. Confronto per nome contro la baseline sotto.

### 3. #3887 — aperta, non lavorata

Due classi producono **rossi che si spostano**, e mi hanno dato due falsi positivi in due run
consecutive:

- `AdminProviderEndpointsIntegrationTests` muta `Environment.SetEnvironmentVariable("RateLimiting__Enabled")`
  dentro un `[Fact]` con `finally` di ripristino, in una collection **parallela**: la finestra è
  globale al processo e chi ci passa dentro prende 429. Cambia bersaglio a ogni run.
- `TwoFactorSecurityPenetrationTests.BruteForce_RapidFireAttack_ShouldBeRateLimited` ha
  `[Fact(Timeout = 10000)]` (`:314`): 10s non bastano per una fixture Testcontainers fredda.
  **Fallisce identico su `main-dev`** — controllo già eseguito.

Contano perché **tutto questo cluster si regge sul confronto per nome**, e quel confronto vale solo
finché i rossi stanno fermi.

## La baseline, e come si usa

Il gate `dev-async` è **rosso in partenza**: il colore del job non dice nulla. Si confrontano i
**nomi** dei falliti.

Baseline corrente — `main-dev` `680f9682b`, run **33245379236**:

| shard | eseguiti | rossi |
|---|---|---|
| Core | 903 | 4 |
| Games | 778 | 5 (uno è il rotante di #3887) |
| KnowledgeBase | 923 | 4 |

Ricetta usata tutta la sessione (gli artifact `.trx` sono scaricabili):

```bash
gh run download <run_id> -n "integration-results-<shard>" -D <dir>
# poi: parse del .trx, insiemi di nomi, e tre categorie —
#   REGRESSIONE      = rosso ora, verde in baseline
#   mai eseguito     = rosso ora, assente in baseline (il gate ha raggiunto più in là)
#   ora verde        = era rosso in baseline
```

`gh workflow run dev-async.yml --ref <branch>` per lanciarlo; ~60-80 min. **Non mergiare prima che
risponda** — è l'errore che fece revertare #3879.

## Le trappole che sono costate di più

1. **Un calo del conteggio eseguiti può essere un CRASH, non un troncamento.** Su #3866 lo shard KB
   eseguiva 758 test contro 918 e l'ho letto come troncamento: era un **ciclo infinito** che aveva
   ucciso il test host dopo 39 minuti. Il `.trx` non li distingue — solo il log:
   `grep -E "Test host process crashed|Aborting test run" integration-<shard>.log`.

2. **`dotnet test` gira sul DLL vecchio se la build fallisce.** Su #3886 la mia diagnostica non è
   mai esistita nel binario: gli analyzer Sonar emettono `error S…`, e il mio `grep "error CS"` non
   li vedeva. **Verifica sempre `dotnet build` prima di credere a un risultato negativo.** Questa
   trappola ha invalidato l'affermazione portante di un'intera issue.

3. **Il `= null` sui parametri opzionali non è cosmetico.** Il container non onora le annotazioni
   nullable: senza valore di default un servizio non registrato resta obbligatorio e la risoluzione
   lancia comunque.

4. **Un commento che descrive il comportamento può mentire.** Ne ho trovati quattro:
   *"we deliberately re-query without AsNoTracking so EF can attach the row"* (il default **è**
   NoTracking), *"load the tracked entity so EF preserves xmin"* (non tracciata), *"UploadAsync will
   throw at first call"* (lancia alla risoluzione). Se un commento dice «tracked», verifica che ci
   sia `.AsTracking()`.

5. **Un'asserzione che accetta quattro esiti non è un'asserzione.**
   `BeOneOf(201, 400, 422, 404)` ha coperto un 500 per mesi.

6. **Verifica i test di regressione per contrasto.** Su #3882 ho tolto le tre `AsTracking` e
   controllato che i quattro test diventassero rossi. Un test che passa sul codice difettoso non
   serve a niente — è come #3858 era sfuggito.

## Vincoli operativi

- **Push/merge solo con l'account `meepleAi-app`**: `DegrassiAaron` dà 403. Ricetta:
  ```bash
  export GH_PUSH_TOKEN=$(gh auth token --user meepleAi-app)
  git -c credential.helper= -c credential.helper='!f() { echo username=meepleAi-app; echo "password=$GH_PUSH_TOKEN"; }; f' push -u origin <branch>
  ```
  Per `gh`: `GH_TOKEN="$(gh auth token -u meepleAi-app)" gh pr create ...`
- Il pre-commit hook ricompila tutto il FE (~3 min): **backgrounda `git commit` e `git push`**.
- Subject dei commit: **max 72 caratteri**, altrimenti commitlint rifiuta.
- `Backend Fast` può risultare `cancelled` per un `actions/checkout` da 10-15 min su un budget di
  18: **non è il diff**. Rerun. E prima di dire «i test sono verdi», controlla il riepilogo vstest
  nel log, non la conclusione del job.

## Il principio, in una riga

Un gate che non esamina niente e un gate che esamina e passa danno **lo stesso segnale**. Ogni volta
che questo cluster è arrivato più lontano — 234 casi in più su Games, 160 su KnowledgeBase — ha
trovato altri difetti. Chiedi sempre il **conteggio**, non solo il colore.
