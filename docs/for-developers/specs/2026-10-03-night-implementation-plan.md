# Piano di implementazione — livelli di dipendenza e salti contabili

**Data**: 2026-10-03 · **Rev. 2, dopo panel** (25 rilievi critici, 17 sopravvissuti alla refutazione)
**Spec**: [2026-10-02-test-dependency-tiers-and-observable-skips.md](./2026-10-02-test-dependency-tiers-and-observable-skips.md)

> Misure datate del 2026-10-03, con il comando accanto. Rimisurale prima di fidarti.

## L'errore della rev. 1, perché non si ripeta

La prima stesura ordinava i task per **dipendenza concettuale** e ignorava il **costo di
osservazione**. Il panel l'ha demolita su tre punti, tutti misurati:

1. **T1 era inosservabile in una notte.** Una run `dev-async` dura 61–110 minuti
   (`timeout-minutes: 110`), parte solo su push a `main-dev` o dispatch, ha
   `cancel-in-progress: false` — e i tre job `Backend Integration` sono **`failure` in ogni run che
   li esegue**: le run `success` sono quelle in cui il job è `skipped` dal path filter. Quindi la
   prova prescritta — «aggiungi uno skip e vedi il rosso» — era **già soddisfatta a codice
   invariato**.
2. **La dipendenza T1 → T3 era retorica.** T3 è un test `Category=Unit`: gira in `dev-fast` su ogni
   PR verso `main-dev`, quindi la sua conseguenza è immediata — e *più forte* di quella di T1, perché
   nota ogni sito non classificato, compresi quelli che si sostituiscono a vicenda a conteggio
   invariato. La dipendenza vera corre **nel verso opposto**: T2 e T4 cambiano i conteggi, quindi è
   la baseline di T1 a dipendere da loro. La spec lo dice (R2.3b), il diagramma della rev. 1 no.
3. **T2 citava come modello un helper rotto e morto.** `E2ETestPrerequisites` ha **zero chiamanti**,
   sonda `localhost:8080` e Qdrant `:6333` (un servizio che questo repo non ha più) — che i test
   degli 8 file non usano, girando in-processo su `E2EWebApplicationFactory` — e il suo
   `IsApiAvailableAsync` ritorna `false` su **qualunque** non-2xx, cioè *deduce l'assenza da una
   risposta*: esattamente il difetto che T2 deve correggere.

## T0 — Sonde di eseguibilità · 15 minuti, prima di qualunque riga di codice

Nessun task comincia prima di aver risposto a queste domande con un comando. Un task che nessun gate
raggiungibile può osservare va **dichiarato tale**, non eseguito sperando.

| domanda | comando | risposta del 2026-10-03 |
|---|---|---|
| Quale gate esegue i test che T2 modifica? | `awk '/^on:/{f=1;next} /^[^[:space:]#]/{f=0} f' .github/workflows/backend-e2e-tests.yml` | `pull_request: branches: [main, main-staging]` — **NON `main-dev`** |
| Quale gate esegue un test `Category=Unit`? | `grep -n "Category!=" .github/workflows/dev-fast.yml` | `dev-fast`, su ogni PR verso `main-dev` |
| Quale gate esegue le suite bats? | `grep -n "bats" .github/workflows/dev-fast.yml` | `dev-fast`, job *Infra Scripts* (#3665) |
| Ollama ha un modello di chat? | `docker exec meepleai-ollama ollama list` | `nomic-embed-text:latest`, **`qwen2.5:3b`** — `llama3:8b` assente |

**Conseguenza di T0 su T2, da accettare prima di iniziare**: le PR di T2 verso `main-dev` **non
eseguono** i test che modificano. La prova richiede
`gh workflow run backend-e2e-tests.yml --ref <branch>` per ogni PR. Senza quella run, una PR di T2
verde non significa niente.

## Ordine

```
T0 ──► T3 ──► T4 ──► T2 ──► T1a ──► T1b/T1c
                                      │
T5 solo se la sonda T0 lo consente ───┘
```

- **T3 primo** perché è l'unico osservabile in minuti e la sua conseguenza è la più forte.
- **T4 prima di T2** perché T4 decide *cosa può fare* un prerequisito L1 non sano, e i 39 siti di T2
  sono chiamate che ne dipendono.
- **T1 ultimo** e spezzato, perché la sua baseline dipende dai conteggi che T2 e T4 cambiano.

🔴 **Decisione da prendere ora, non alle 3 di notte**: T2 e T3 lavorano sulla **stessa popolazione**
(39 dei 93 `Assert.Skip` sono i siti «service likely unavailable»). Scelta: **T3 nasce con i 39
dentro `Exempt`**, con la motivazione «in conversione in T2 — #<issue di T2>», e T2 li rimuove
dall'elenco nella propria PR. L'alternativa (T2 prima) rifà la classificazione due volte.

---

## T3 — Le quattro classi di salto, col gate che le verifica · R1, A1

```
grep -rhoE 'Assert\.(Skip|SkipUnless)\(' --include=*.cs apps/api/tests/ | wc -l   → 93
grep -rhoE '\[(Fact|Theory)\(Skip' --include=*.cs apps/api/tests/ | wc -l          → 45
grep -rhoE '\[(Fact|Theory)\(Skip = "[^"]*InMemory' … | wc -l                      → 17  (classe LIMITE)
```

**Cosa fare**
1. Il gate **scansiona i sorgenti**, sul modello di `EgressHttpClientPinArchitectureTests`
   (`Directory.EnumerateFiles` + `ReadStatement`) e del precedente scritto ieri,
   `PaidAiGuardCoverageArchitectureTests`. **Non** sul modello di
   `TestCategoryGateArchitectureTests`, che legge solo `GetCustomAttributesData()` e per riflessione
   non vede l'argomento di un `Assert.Skip` dentro un corpo di metodo.
2. Classi: `PREVISTO:` · `GUASTO:` · `DIFETTO:` · `LIMITE:`.
3. Elenco `Exempt` con motivazione **per voce**, che nasce con i 39 siti di T2 dentro.
4. Il gate legge l'**istruzione**, non la riga: un motivo può essere interpolato o spezzato.

**Fatto quando**
- [ ] Il gate fallisce togliendo un prefisso a un sito. **Prova eseguita e incollata nella PR.**
- [ ] Porta una contro-prova di se stesso: se il marcatore di scansione smette di combaciare, un
      secondo test lo rileva invece di passare vacuamente.
- [ ] `dotnet test --filter "FullyQualifiedName~<gate>"` verde, in secondi, sulla macchina locale.

**Punto di stop sicuro**: il gate può nascere con `Exempt` pieno e svuotarsi dopo. Nascere *senza*
gate e coi siti classificati è l'ordine sbagliato: la classificazione senza applicatore invecchia
come la riga 296 di `dev-async.yml`.

---

## T4 — L1 non si salta · R3, A3, A4

**Cosa fare**
1. Lista L1 in **un posto eseguibile** (Q5 della spec): postgres, redis, minio, mailpit.
2. Prerequisito L1 non sano ⇒ **fallimento**, con messaggio che distingue «ambiente non pronto» da
   «asserzione violata».
3. 🔴 Mitigazione **nella stessa PR**: chi lavora senza Docker deve poter **non selezionare** quei
   test, con un target `make` documentato e provato. Non è opzionale: senza, questo task trasforma
   «Docker spento» in una giornata rossa.

**Fatto quando**
- [ ] Con l'immagine MinIO puntata a un tag inesistente, le due suite di #3978 diventano **rosse**.
      Da eseguire su un **progetto compose isolato** (`-p probe`, volumi nuovi), mai sullo stack del
      proprietario — che ora è su MinIO e funzionante.
- [ ] Esiste un test che fallisce se un L1 viene saltato.
- [ ] Il target `make` di esclusione gira e il suo output è nella PR.

---

## T2 — I 39 siti che deducono l'assenza da una risposta · R1.6, A7

```
grep -rhoE 'Assert\.Skip\([^;]*service likely unavailable' --include=*.cs apps/api/tests/ | wc -l → 39  (8 file)
```

🔴 **Non usare `E2ETestPrerequisites` come modello.** Zero chiamanti, sonda `localhost:8080` e
Qdrant `:6333` che quei test non usano, e tratta ogni non-2xx come assenza. Nella stessa PR va
**chiuso**: corretto (distinguere rifiuto-di-connessione da status di errore) oppure `[Obsolete]`
con il rimpiazzo nel messaggio — perché finché esiste qualcuno lo ricopia, come ho fatto io nella
rev. 1 di questo piano.

**Cosa fare**
1. Il bersaglio della sonda è **ciò che il test usa davvero**: l'app in-processo di
   `E2ESharedInfrastructure.Factory` più i container di `SharedTestcontainersFixture`. Sonda e
   chiamata sotto test devono puntare allo **stesso** `HttpClient`.
2. Un'eccezione dentro la logica del test resta un **fallimento**, non un salto.
3. Prima di convertire un sito in fallimento, verificare se quel servizio esiste nei gate che
   eseguono quella suite: se non c'è, il sito è PREVISTO, non GUASTO.

**Fatto quando**
- [ ] **Criterio comportamentale, non un grep**: per almeno una suite, con il servizio **degradato**
      (risponde 500, non rifiuta la connessione) il test **fallisce**; con il servizio **assente**
      (connessione rifiutata) **salta**. Due esecuzioni, entrambe nella PR.
      *Il grep a zero è un criterio che si soddisfa barando: basta rinominare la stringa.*
- [ ] `gh workflow run backend-e2e-tests.yml --ref <branch>` eseguito per la PR, con l'esito nel
      corpo — perché le PR verso `main-dev` **non** eseguono questi test.
- [ ] Conteggio `Passed` della suite prima e dopo, dalla stessa fonte, uguale o maggiore.

**Punto di stop sicuro**: una suite per PR. Prima suite consigliata: la più piccola fra le otto, per
stabilire il pattern su poco codice.

---

## T1 — La baseline dei salti, spezzata in tre · R2, A2

**Spezzata perché il suo costo di osservazione non sta in una notte** (vedi §errore 1).

### T1a — il comparatore come script, osservabile in `dev-fast`

**Il precedente esiste due volte, ed è completo** — non va inventato niente:

| | `rag-smoke-assert.sh` | `title-health-assert.sh` |
|---|---|---|
| baseline | `infra/fixtures/*-baseline.json` | `infra/fixtures/title-health-baseline.json` |
| aggiornamento | `--update-baseline` | `--update-baseline` |
| invocazione | una riga da `rag-smoke-dispatch.yml:289,293` | una riga da `title-health-staging.yml:109,112` |
| test | suite bats in `infra/scripts/tests/` | idem |

Le suite bats girano nel job *Infra Scripts* di `dev-fast`, su ogni PR verso `main-dev` — **lì il
rosso è visibile in minuti e deterministico**. Il job è nato da #3665, dove
`snapshot-verify.bats` è rimasto rosso due mesi senza che nessuno lo notasse: vale la pena leggere
quel commento prima di aggiungere una suite.

🔴 **La fonte del conteggio, e la trappola che contiene.** Il `.trx` ha **due** rappresentazioni dei
salti e una è falsa:

| | valore sui tre shard del run 36997819474 |
|---|---|
| `Counters/@notExecuted` | **0 · 0 · 0** ← falso |
| numero di `<UnitTestResult outcome="NotExecuted">` | **45 · 11 · 8** ← vero |
| trailer del log `Skipped: N` | 45 · 11 · 8 |

Un gate scritto col campo apparentemente ovvio leggerebbe **sempre zero**: sarebbe il gate verde e
vuoto costruito dentro la PR che dovrebbe chiudere quella famiglia di difetti. Il trailer si legge
come **controprova**: se le due fonti divergono, il gate fallisce con «fonti discordanti» invece di
scegliere.

**Baseline, misurata e con il suo run** — non «l'ultimo run», che è un riferimento mobile:

```
run 36997819474 (2026-10-02; i tre job con conclusion=failure, quindi hanno ESEGUITO)
  Core            failed=4   skipped=45
  KnowledgeBase   failed=23  skipped=11
  Games           failed=2   skipped=8
```

Il file porta `runId` e `capturedAt`. Non aggiornare la baseline a un run più recente senza prima
verificare che i tre job non siano `skipped`.

**Fatto quando**
- [ ] Il comparatore, sui tre `.trx` del run 36997819474, esce 0. Su una copia con un
      `<UnitTestResult outcome="NotExecuted">` aggiunto a mano, esce ≠0 e **nomina lo shard**.
      Entrambe le esecuzioni nella PR.
- [ ] Un test bats copre: log normale, log troncato (`Aborting test run: test run timeout`), log
      senza trailer (test host crashato), e un `.trx` con `notExecuted="0"` più tre
      `outcome="NotExecuted"` — che **deve** riportare 3.
- [ ] Il job *Infra Scripts* di `dev-fast` è verde sulla PR.

### T1b — innesto nel workflow, in sola segnalazione

Una riga che invoca lo script e stampa l'esito, **senza** far fallire il job. Rimuove la riga 296.

### T1c — promozione a errore

Solo dopo che T1b ha prodotto almeno una run con esito coerente.

⚠️ **Correzione alla spec**: R2.2 dice «come già si pretende per i fallimenti». **Falso**: nessun
workflow confronta `Failed: N` con una baseline — la riga 296 la *stampa* e nessuno la legge. Il
braccio dei fallimenti va costruito, non esteso. La spec va corretta nella PR di T1a.

---

## T5 — Attribuzione del provider · R5.3, A6

**Prerequisito, da verificare in T0 e non alla fine**: il criterio non è «esiste `llama3:8b`», è
**l'uguaglianza fra il model id che l'API risolve e i tag installati**.

```
docker exec meepleai-ollama ollama list   → nomic-embed-text:latest · qwen2.5:3b
AgentDefaults.OllamaFallbackModel          → "llama3:8b"
```

Un modello di chat **c'è** (`qwen2.5:3b`, 1,9 GB, già scaricato), ma l'API ne chiede un altro. Quindi
la via a costo zero **non è scaricare 4,7 GB**: è mappare `qwen2.5:3b` in `StrategyModelMapping`,
oppure cambiare `OllamaFallbackModel`. Questa è la prima cosa da fare di T5, e vale da sola.

**Noto, e da verificare prima di promettere A6**: `LlmProviderSelector` pinna
`RequestSource.AutomatedTest` su Ollama **per design**, e gli altri client hardcodano il
`BaseAddress`. Un test «su due provider diversi» potrebbe quindi non essere raggiungibile oggi senza
toccare il prodotto. Se la verifica lo conferma, **T5 si chiude con la sola mappatura del modello** e
A6 resta aperto nella spec con la ragione scritta.

---

## Invarianti di sessione

🔴 Se uno non si può rispettare, **fermarsi e lasciare una nota**, non aggirarlo.

1. **Una PR per task** (o per sotto-task), base `main-dev`, closing keyword verificata con
   `grep -cE "^Closes #NNNN$"` sul corpo **pubblicato**.
2. **Mai mergiare con check rossi.** `CodeQL Analysis (csharp)` pendente su una PR che non tocca C#
   è lento, non rosso.
3. **Non far crescere i conteggi.** Confronto **per shard**, e prima di confrontare verificare che il
   run di riferimento abbia *eseguito* il backend: `gh run view <id> --json jobs` → nessun `skipped`.
4. **Ogni gate nuovo porta la prova che fallisce**, eseguita e incollata nella PR. **Con il suo costo
   dichiarato**: se la prova richiede una run da 110 minuti, il gate va spostato dove la prova costa
   secondi (T1a), non dichiarato soddisfatto.
5. **Ogni numero in un documento porta il comando** che lo produce, o è un esito datato.
6. Niente `--admin`, niente `--auto`, niente force push.
7. **Un task più grande del previsto si ferma al punto di stop sicuro** e apre la PR per ciò che è
   completo. Una PR parziale e verde batte una PR grande e rossa.
8. **Lo stack Docker del proprietario non si toccca**: è su MinIO e funzionante (`s3storage: Healthy`).
   Le prove distruttive vanno su un progetto compose isolato (`-p probe`), con `down -v` e rimozione
   esplicita dei volumi residui alla fine.

## Cosa non toccare

- I filtri dei gate **bloccanti** (`dev-fast`, `ci`).
- Il path filter dei gate storage: 22 commit in 90 giorni su `infra/docker-compose.yml` contro 2
  sull'overlay — è policy CI del proprietario, non un fix.
- I fallimenti già in baseline su `dev-async`.
- **#4016**: due dei tre difetti toccano il presign in produzione e uno è «SigV2 contro SigV4», non
  determinato. Non è lavoro da notte autonoma.
- Download di modelli: `qwen2.5:3b` basta, vedi T5.

## Se tutto finisce

Q2 e Q5 della spec con la forma scelta; poi le parti residue di **#3994** (19 computazioni `isAdmin`
che scavalcano la gerarchia, da classificare in concede / protegge / esenta); poi la manutenzione del
tracker già analizzata. **Non** iniziare #4016.
