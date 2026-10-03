# Livelli di dipendenza dei test, e salti che un gate può confrontare

**Stato**: bozza rev. 2, dopo panel esperto · **Data**: 2026-10-02 · **Origine**: #3978 → #4016, e la domanda «a cosa serve lo skip?»

> Convenzione sui numeri: i conteggi qui sono **esiti di una misura datata**, non totali da tenere
> aggiornati. Dove serve il valore corrente c'è il comando che lo produce. Non aggiungere qui
> conteggi che invecchiano da soli — questo documento nasce proprio dal danno che fanno.

## 1. Il problema, e la diagnosi corretta al secondo tentativo

Il 2026-09 le immagini MinIO sono scomparse da Docker Hub. Due suite di integrazione S3 hanno smesso
di eseguire i propri test e nessuno se ne è accorto per un mese.

**Prima diagnosi, sbagliata**: «saltavano in silenzio». Falso — usavano già
`Assert.Skip("S3 storage tests require Docker or TEST_S3_ENDPOINT…")` e comparivano come `Skipped`
con un motivo.

**Seconda diagnosi, sbagliata anche questa**: «i salti non si contano». Falso — `dev-async.yml:288`
grepa il trailer `Passed!  -  Failed: N, Passed: N, Skipped: N` e lo scrive in
`$GITHUB_STEP_SUMMARY` per ciascuno shard. I salti sono **stampati a ogni run**.

**Diagnosi che regge**, e sono tre difetti distinti:

1. **Il motivo era generico, quindi fuorviante.** Diceva «richiede Docker» mentre Docker c'era: la
   causa vera, l'immagine irraggiungibile, finiva solo nella `Console.WriteLine` del `catch`, che
   xUnit non mostra. Un motivo generico spiega l'assenza con la causa sbagliata e chiude l'indagine
   prima che inizi.

2. **Il numero è stampato ma non confrontato.** Nessun gate legge quel `Skipped: N` e lo mette
   accanto a un valore atteso. Stampare non è misurare: un numero che nessuno confronta è decorazione.

3. **La baseline che esiste è prosa, ed è già falsa.** `dev-async.yml:296` stampa
   `"Baseline attesa per shard: Core 9 · KnowledgeBase 4 · Games 3 (#3633)."` — scritta nel workflow,
   **mai confrontata con l'esito**, e con numeri che non corrispondono più: misurati sul run
   36997819474 del 2026-10-02 i fallimenti sono Core 4 · KnowledgeBase 23 · Games 2. Un totale in
   prosa dentro un workflow invecchia come qualunque altro.

E un quarto difetto, che la prima stesura di questa spec non vedeva e che è il più diffuso:
**39 siti in 8 file deducono l'assenza da una risposta ricevuta**, con motivi della forma
«service likely unavailable». Un errore qualunque — 500, timeout, risposta vuota, bug del prodotto —
viene letto come «il servizio non c'è» e convertito in salto. È il caso MinIO generalizzato.

```
grep -rhoE 'Assert\.Skip\([^;]*service likely unavailable' --include=*.cs apps/api/tests/ | wc -l
```

## 2. Decisioni già prese

Dal proprietario, non in discussione qui:

- **D1 — «salta e dillo».** Chi dipende da un servizio non disponibile salta, e lo dice.
- **D2 — Il locale viene prima.** DB, cache, storage devono funzionare in locale.
- **D3 — I servizi esterni gratuiti si usano se disponibili.**
- **D4 — Le chiamate a consumo non sono mai automatiche**, o stanno entro i limiti dichiarati.
- **D5 — Provider AI intercambiabili**, idealmente per configurazione, per poterli in futuro
  **ciclare** o **confrontare**.

## 3. I cinque stati di una dipendenza, non due

La prima stesura conosceva «presente» e «assente». Lo stato che manca è quello che fa più danno.

| stato | come si riconosce | cosa deve fare il test |
|---|---|---|
| **presente e sana** | sonda OK entro il timeout | eseguire |
| **assente** | connessione rifiutata, DNS che non risolve, immagine non scaricabile | saltare (L2/L3) o **fallire** (L1) |
| **degradata** | risponde entro il timeout ma lenta, o 200 con corpo vuoto, o intermittente | **non saltare**: un salto qui nasconde un guasto reale del servizio |
| **oltre il limite** | 429, o quota esaurita | saltare come PREVISTO, dichiarando il limite |
| **presente ma non è lei** | risponde un surrogato dove serviva il reale, o viceversa | fallire: l'ambiente non è quello dichiarato |

Il vincolo che ne deriva, e che è la correzione del difetto §1.4: **l'assenza si accerta con una
sonda, non si deduce da un errore.** Un `catch` attorno alla logica del test non può distinguere
«assente» da «degradata» da «rotta», e se converte tutto in salto cancella il test.

## 4. I quattro livelli di dipendenza

| livello | esempi | se non è sana |
|---|---|---|
| **L1 — locale deterministico** | postgres, redis, minio, mailpit | **fallire**: quei servizi ci devono essere |
| **L2 — locale pesante** | embedding, reranker, smoldocling, Ollama | saltare, dicendo come avviarlo |
| **L3 — esterno gratuito con limiti** | BGG, Wikidata, free tier AI | saltare, dichiarando che era opzionale |
| **L4 — a consumo** | modelli LLM a pagamento, email reali | non selezionato in automatico |

MinIO è **L1** e la suite lo trattava come opzionale: è l'errore di fondo di #3978.

## 5. Le quattro classi di salto

La prima stesura ne aveva tre, e non erano esaustive: il gruppo più numeroso degli skip esistenti non
rientrava in nessuna. Misurato — `[Fact(Skip` / `[Theory(Skip` sono 44 occorrenze, di cui 3 sono
commenti XML, quindi **41 vivi**; di questi **17 dicono «EF Core InMemory provider cannot
translate…»**: nessun servizio opzionale, nessun servizio che dovrebbe esserci, nessun bug di
prodotto, nessun numero di issue.

| classe | significato | esempio | chi agisce |
|---|---|---|---|
| **PREVISTO** | il servizio è opzionale per progetto (L3/L4) | «nessuna chiave configurata» | nessuno |
| **GUASTO** | il servizio *dovrebbe* esserci (L1/L2) e non è sano | «immagine non scaricabile» | chi tiene l'ambiente, subito |
| **DIFETTO** | il prodotto è rotto, il test è corretto | i quattro di #4016 | chi possiede l'issue |
| **LIMITE** | il test non è esprimibile sotto questo harness | «EF InMemory non traduce sub-query cross-BC» | chi possiede il test: spostarlo di livello o cancellarlo |

Per **DIFETTO** e **LIMITE** la forma statica `[Fact(Skip = …)]` è corretta: l'esito non dipende
dall'ambiente, quindi non c'è nulla da valutare a runtime. Per **PREVISTO** e **GUASTO** serve il
runtime, perché la condizione si conosce solo allora.

Il caso MinIO era **GUASTO travestito da PREVISTO**, ed è per questo che è durato un mese.

## 6. Requisiti

### R1 — Il motivo dichiara la classe, la causa concreta e la via d'uscita

- **R1.1** Il motivo inizia con `PREVISTO:`, `GUASTO:`, `DIFETTO:` o `LIMITE:`.
- **R1.2** Per GUASTO il motivo riporta **l'errore osservato**, non la condizione generica.
  Controesempio da non ripetere: «requires Docker or TEST_S3_ENDPOINT» con Docker attivo.
- **R1.3** Per PREVISTO il motivo dice come abilitare il test: variabile, comando o chiave.
- **R1.4** Per DIFETTO il motivo contiene il numero della issue.
- **R1.5** Per LIMITE il motivo nomina la limitazione **e** il livello a cui il test andrebbe spostato.
- **R1.6** 🔴 Un salto non si deduce da un'eccezione della logica del test: la non-sanità si accerta
  con una sonda dedicata **prima** di eseguire. I 39 siti «service likely unavailable» vanno convertiti
  o riclassificati.

*Scenari*

```
Given  un servizio L3 senza credenziali
Then   Assert.Skip("PREVISTO: provider esterno non configurato — imposta <VAR> per eseguirlo")

Given  un servizio L1 la cui sonda non risponde
Then   il test FALLISCE, con il messaggio della sonda           (R3)

Given  un servizio L3 che risponde 429
Then   Assert.Skip("PREVISTO: limite di <N>/min raggiunto su <servizio>")

Given  un servizio L2 che risponde entro il timeout ma impiega 10x il previsto
Then   il test NON salta: esegue e, se l'asserzione cade, FALLISCE        (par. 3)

Given  un test che non passa per un bug noto, con tutti i servizi sani
Then   [Fact(Skip = "DIFETTO: #4016 — …")]
```

### R2 — I salti si confrontano, non si stampano

- **R2.1** La baseline dei salti per shard vive in un **file che un gate legge**, non in un `echo`
  dentro un workflow né in un documento. Prosa che nessuno rimisura è il difetto §1.3.
- **R2.2** Il gate confronta il conteggio dei salti con la baseline e **fallisce** su un aumento non
  dichiarato.

  > ⚠️ Correzione del 2026-10-03: questo requisito diceva «come già si pretende per i fallimenti».
  > **Falso.** Nessun workflow confronta `Failed: N` con una baseline: in `dev-async.yml` la baseline
  > dei fallimenti compare **solo** in due `echo` (righe 296 e 300) e nessuno la legge. Il braccio dei
  > fallimenti va **costruito**, non esteso. Il pattern da seguire esiste altrove nel repo, due volte:
  > `infra/scripts/rag-smoke-assert.sh` e `infra/scripts/title-health-assert.sh`, entrambi con una
  > baseline in `infra/fixtures/<nome>-baseline.json`, un flag `--update-baseline`, una suite bats e
  > un'invocazione di una riga dal workflow.
  >
  > 🔴 E la **fonte** del conteggio non è ovvia: nel `.trx` il campo `Counters/@notExecuted` vale
  > **0** su tutti e tre gli shard mentre i salti reali sono **45 · 11 · 8** (misurato sul run
  > 36997819474). Il conteggio vero è il numero di elementi `<UnitTestResult outcome="NotExecuted">`.
  > Un gate scritto col campo apparentemente ovvio leggerebbe sempre zero — il gate verde e vuoto,
  > costruito dentro il lavoro che dovrebbe chiuderne la famiglia.
- **R2.3a** L'ambito della policy sui conteggi passa da *unit test* a **ogni gate che pubblica un
  conteggio** (dev-fast, ci, dev-async per shard).
- **R2.3b** Uno skip introdotto come rimedio a un fallimento **sposta** il test da `Failed` a
  `Skipped`: va dichiarato in entrambe le baseline nella stessa PR. Oggi la policy sanziona quel
  rimedio senza registrarne l'effetto sull'altro conteggio — vedi §8 Q6.
- **R2.4** La riga `"Baseline attesa per shard: Core 9 · KnowledgeBase 4 · Games 3"` di
  `dev-async.yml:296` va rimossa o sostituita dal confronto di R2.2: oggi è un numero falso che
  nessuno verifica.

### R3 — Un servizio L1 non si salta

- **R3.1** La lista L1 è dichiarata in **un solo posto eseguibile**, non per convenzione.
- **R3.2** Prerequisito L1 non sano ⇒ il test **fallisce**, con un messaggio che distingue «ambiente
  non pronto» da «asserzione violata».
- **R3.3** Caso di prova: con l'immagine MinIO irraggiungibile, le due suite di #3978 diventano
  **rosse**, non gialle.
- **R3.4** Mitigazione del costo quotidiano: chi lavora senza Docker deve poter eseguire la selezione
  che non lo richiede, con un comando documentato. R3 non deve trasformare «Docker spento» in una
  giornata rossa — ma la via d'uscita è **non selezionare** quei test, non saltarli.

### R4 — Nessuna spesa in un gate automatico

- **R4.1** 🔴 La decisione di spesa **non è un host**: è un **model id**. Misurato —
  `LlmCostCalculator.cs` prezza `meta-llama/llama-3.3-70b-instruct:free` a `0m` e
  `meta-llama/llama-3.3-70b-instruct` a `0.59m/0.79m`, con `Provider = "OpenRouter"` **identico**.
  Una deny-list di host non può distinguerli. La guardia va estesa su **due assi**: (a) le superfici
  di test non coperte, (b) il model id selezionato.
- **R4.2** I test L4 vivono in una categoria che nessun workflow su push, PR o cron seleziona, e
  restano **eseguibili** tramite un workflow `workflow_dispatch`. Il comando è in `tests/README.md`.
- **R4.3** Per L3 il limite assunto è scritto accanto al test, e chi lo rispetta è un meccanismo —
  throttle o quota — non la disciplina di chi scrive il test.
- **R4.4** L'invariante «ogni workflow automatico inietta una chiave placeholder» oggi vale ed è
  **non misurata**: un workflow nuovo che usasse la chiave reale passerebbe in silenzio. Serve il
  gate, che è un grep.

### R5 — Provider selezionabile, e attribuibile

- **R5.1** Cambiare provider non richiede modifiche al codice dei test.
- **R5.2** Il design non preclude di eseguire lo stesso test su più provider. **Ciclare** (rotazione
  fra equivalenti) e **confrontare** (esecuzione multipla con raccolta di esiti) sono due requisiti
  distinti: nessuno dei due è richiesto ora.
- **R5.3** 🔴 Un test parametrico sul provider deve **asserire quale provider ha servito la
  richiesta**. Esiste un fallback automatico (`LlmProviderSelector`: *«Fallback from {provider}
  (circuit open or unhealthy)»*) che può far passare due esecuzioni servite dallo **stesso** provider.
  Senza l'attribuzione, A6 è falsamente verificabile.

## 7. Criteri di accettazione

- [x] **A1** — fatto in **#4027**. Ogni sito di salto in `apps/api/tests/**` porta una delle quattro
  classi, verificato da `SkipReasonClassArchitectureTests`, che **scansiona i sorgenti** via
  `SourceScanner` (estratto da `EgressHttpClientPinArchitectureTests`) e non per riflessione: il
  motivo di un `Assert.Skip` è un argomento dentro un corpo di metodo, dove
  `GetCustomAttributesData()` non arriva.
  **Due cose che il gate ha dimostrato di non essere decorativo**, entrambe la notte stessa:
  (a) la sua contro-prova `TheScanFindsTheKnownPopulation` ha scoperto che la prima stesura trovava
  **0 siti su 22 file** contenenti `Assert.Skip(` — perché riusava `SourceScanner.ReadStatement`,
  che *salta i letterali*, per leggere un motivo che **è** un letterale; il test principale passava
  vacuamente; (b) `EveryExemptionStillAppliesToSomething` ha dichiarato stale due esenzioni appena
  il lavoro sottostante le ha svuotate (`E2EServiceProbe.cs` in #4023,
  `AdminGameCreationJourneyE2ETests.cs` in #4033), costringendo a rimuoverle nello stesso passaggio.
  **Limite dichiarato**: un motivo costruito in una **variabile** è invisibile a una scansione dei
  sorgenti. Dove serve, il prefisso va garantito da un test del costruttore del motivo.
- [ ] **A2** — fatto in **#4030**, con un residuo dichiarato. Esistono
  `infra/fixtures/dev-async-shard-baseline.json` (con `runId` e `capturedAt`) e
  `infra/scripts/shard-skip-assert.sh`, che **fallisce** su un aumento non dichiarato di fallimenti
  *o di salti*. Prova in CI, non a mano: i casi girano nel job *Infra Scripts* di dev-fast
  (run `37109361903`, `ok 58`–`ok 70`), fra cui il decisivo — un `.trx` con `notExecuted="0"` **e**
  tre elementi `NotExecuted` ⇒ `skipped=3`.
  🔴 **La trappola che il criterio non prevedeva**: nel `.trx` i salti hanno due rappresentazioni e
  una è falsa. `Counters/@notExecuted` vale **0** su tutti e tre gli shard anche con 45 salti
  (misurato sul run `36997819474`); il conteggio vero sta negli elementi
  `<UnitTestResult outcome="NotExecuted">`. Un gate scritto col campo apparentemente ovvio avrebbe
  letto sempre zero: sarebbe stato il gate verde e vuoto costruito dentro il lavoro che dovrebbe
  chiuderne la famiglia.
  **Residuo (T1c)**: dentro `dev-async.yml` il comparatore è ancora in **report-only** (`|| true`).
  Promuoverlo a errore nello stesso passaggio in cui lo si introduce significherebbe spedire un gate
  di cui non si è mai visto l'output su un `.trx` prodotto da dev-async invece che da una fixture.
  La casella si spunta quando il `|| true` sparisce.
- [x] **A3** — **verificato con un drill distruttivo**, 2026-10-03. Le due suite di #3978
  (`S3BlobStorageIntegrationTests`, `CoverR2ConventionIntegrationTests`) contro l'immagine MinIO
  puntata a un tag inesistente:

  | | immagine valida | tag inesistente |
  |---|---|---|
  | Superati | 12 | **0** |
  | **Non superati** | 0 | **12** |
  | Ignorati | 4 | 4 |

  I 4 ignorati sono gli stessi nei due casi e sono `DIFETTO: #4016` — salti statici su bug noti di
  prodotto, che non dipendono dall'ambiente e quindi *devono* restare invariati. Il messaggio dei 12
  fallimenti è quello di `L1Services.FailBecauseUnavailable`: «*un servizio L1 deve esserci, quindi
  la sua assenza è un guasto da riparare. Un salto la nasconderebbe — è così che l'immagine MinIO
  sparita è passata inosservata per un mese (#3978)*». Ripristinato il tag, il verde torna
  (contro-verifica eseguita).
  È lo scenario esatto di #3978, e ora è **rosso invece che giallo**.
- [x] **A4** — fatto in **#4028**. `apps/api/tests/Api.Tests/Infrastructure/L1Services.cs` è la lista
  L1 **eseguibile** (postgres, redis, minio, mailpit, ciascuno col comando che lo avvia) con
  `FailBecauseUnavailable`, e `L1NeverSkippedArchitectureTests` fallisce se un L1 viene saltato. Le
  esenzioni residue nominano la issue che le chiude, non un numero.
- [x] **A5** — fatto in **#4019**, e il criterio sottostimava il lavoro: la guardia esisteva ma
  copriva **2 superfici su 8**. Ora `PaidAiGuardCoverageArchitectureTests` misura la copertura, e il
  gate su `.github/workflows/**` in `validate-workflows.yml` impedisce a un workflow nuovo di
  iniettare una chiave reale al posto di un placeholder (`PAID_KEYS` copre `OPENROUTER_API_KEY`,
  `DEEPSEEK_API_KEY`, `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`).
- [x] **A6** — ⛔ **non raggiungibile per design, e la parte raggiungibile è fatta** (#4025).
  La prima metà del criterio — «gira contro due provider cambiando solo configurazione» — è
  **preclusa da una decisione di prodotto, non da un difetto**: `LlmProviderSelector` instrada
  `RequestSource.AutomatedTest` su Ollama *qualunque* sia la strategia, perché un test automatico
  non consumi quota OpenRouter gratuita. Un test parametrico sui due provider passerebbe quindi due
  volte sullo stesso, che è esattamente il falso verde che R5.3 descrive. Raggiungere A6 alla
  lettera richiederebbe di far spendere quota a un gate automatico — il contrario di R4 e del
  vincolo dichiarato dal richiedente («i test a consumo non devono essere automatici»).
  Il vincolo è **fissato da un test** (`OllamaChatModelConfigurationTests.UnTestAutomatizzatoEPinnatoSuOllamaPerDesign`)
  invece di essere aggirato: se il pin cadesse, si rilegge questo criterio, non si silenzia il test.
  La seconda metà — **«asserisce quale provider ha risposto»** — è invece soddisfatta:
  `ProviderSelectionResult.Decision` porta `ProviderName`, `ModelId` e `Reason`, e un test lo
  verifica fino al modello risolto. Resta il presupposto che il percorso Ollama **funzioni**: non
  funzionava, perché `OLLAMA_CHAT_MODEL` era letta da compose e ignorata dall'API (ogni chat →
  `404 model not found`); corretto nello stesso passaggio.
- [ ] **A7** I siti «service likely unavailable» sono convertiti a una sonda o riclassificati.
  **Misura**: il grep di §1 torna a zero, con il pattern validato su un positivo noto.
  **Stato**: convertiti in due passaggi — **#4023** (`E2ETestBase` e le sei suite che la estendono)
  e **#4033** (`AdminGameCreationJourneyE2ETests`, l'unica che richiedeva giudizio). La casella si
  spunta quando entrambe sono su `main-dev` e il grep misura zero: finché una delle due è aperta, il
  conteggio non è zero **per costruzione**, non per un sito dimenticato — verificato che i file
  residui su ciascun branch sono esattamente quelli nel diff dell'altra PR, senza un terzo insieme.
  **Il criterio taceva su una cosa che è emersa convertendo**: una sonda sbagliata è peggio del
  difetto che sostituisce. In #4023 la prima stesura ha gatato tre test su un check `orchestrator`
  che quell'host non registra, e **tre test che passavano sono diventati salti** — visibile solo
  confrontando due run dello stesso gate (`37108228227` contro `37109312581`). Da lì la regola che
  la sonda ora applica: **un check assente non è un servizio assente**, è una sonda che non può
  accertare niente, e quindi **fallisce**.

## 8. Punti aperti

| # | domanda | stato |
|---|---|---|
| **Q1** | Quali provider AI gratuiti? | **superata in parte**: Ollama è già locale e gratuito (§9) |
| **Q2** | Forma del file di baseline dei salti (R2.1) | aperto |
| **Q3** | I quattro skip di #4016 restano DIFETTO o si risolvono prima? | proprietario |
| **Q4** | Il prodotto supporta un provider LLM locale? | **CHIUSO: sì** (§9) |
| **Q5** | Dove vive la lista L1 di R3.1 | aperto |
| **Q6** | Uno skip-come-rimedio va contato in entrambe le baseline, o la policy deve smettere di sanzionarlo? | proprietario |

## 9. Stato misurato dell'ambiente

Accertato il 2026-10-02. Giustifica le priorità, e alcune voci sono difetti attivi.

**Q4 è chiuso: un provider LLM locale esiste già.** `enum LlmProvider { OpenRouter, Ollama }`, tre
client registrati (Ollama, OpenRouter, DeepSeek), `OllamaLlmClient` completo — chat, streaming,
`CheckHealthAsync`, costo dichiarato $0 — e il servizio `ollama` è nel compose sotto `--profile ai`,
incluso in `make dev`. Quindi R5.1 è **parzialmente vero**: il provider si sceglie per
configurazione, ma si risolve e si attribuisce in più posti disgiunti, il che è esattamente ciò che
R5.3 deve rendere verificabile.

Difetti attivi, in ordine di danno:

1. 🔴 **`make dev` scrive sullo storage R2 reale.** `infra/secrets/storage.secret` ha
   `STORAGE_PROVIDER=s3` e un endpoint `*.r2.cloudflarestorage.com` con credenziali vere; è un
   `env_file` dell'api in `compose.dev.yml` e nessun `environment:` lo sovrascrive. Ogni upload in
   sviluppo va su un bucket cloud a consumo — **D2 è violata sullo storage**. Il surrogato locale è
   già provato in CI (`compose.e2e-storage.yml`), e il modello da replicare è l'email: `email.secret`
   ha `EMAIL_PROVIDER=smtp` e `api.env.dev` punta su Mailpit.
2. 🔴 **Una chat su Ollama risponderebbe 404.** `ollama-pull` scarica solo `nomic-embed-text`, che è
   un modello di *embedding*, mentre il fallback LLM nel codice è `llama3:8b`; `ollama-init.sh`
   (`qwen2.5:1.5b`) non è invocato da nessuno e porta una lista di modelli diversa. Gap di tre righe
   fra «AI gratis in locale» e «funziona».
3. 🟡 **La guardia AI copre 2 superfici su 8.** `PaidAiHostGuardHandler` (REQ-AI-TEST-001) è
   fail-closed a livello HTTP e ha test propri, ma solo `E2EWebApplicationFactory` e
   `IntegrationWebApplicationFactory` lo installano. Quattro delle sei superfici scoperte mettono una
   chiave finta: la chiamata **esce** e muore con 401; due non mettono nemmeno quella.
4. 🟡 **`OpenRouterLlmClient` hardcoda il `BaseAddress`** e ignora `OpenRouter:BaseUrl`: non si può
   redirigere il traffico OpenRouter su un endpoint locale per configurazione.
5. 🟡 **`pnpm test:e2e` in locale raccoglie `smoke-real-backend/` e `smoke-real-llm/`** — 150 test in
   16 file che vogliono backend e LLM reali. In CI le invocazioni sono mirate: **il rischio è locale,
   non in CI**. Il tag `@slow` è documentato come escluso e nessun `--grep` lo implementa.
6. 🟡 **MSW parte con `onUnhandledRequest: 'bypass'`**: una richiesta non gestita esce dal processo
   invece di far fallire il test. È la polarità opposta a D2.
7. 🟡 **Cinque immagini Testcontainers non esistono**: puntano a `infra-<x>-service:latest` mentre il
   compose costruisce `meepleai-<x>-service`. I test che le usano non girano.
8. 🟡 **`TEST_PDF_SERVICES` non è mai acceso**: tre suite (28 fatti) non girano né in CI né in locale,
   e sono la parte dello stack che gira in locale *per costruzione* (modelli nell'immagine, nessuna
   rete). Miglior rapporto fra copertura guadagnata e costo.
9. 🟢 **In CI il consumo è già ~zero**: `secrets.OPENROUTER_API_KEY` compare una volta, in un
   workflow con cron disabilitato e solo `workflow_dispatch`; tutti gli altri iniettano placeholder.
   È l'invariante da **proteggere** con R4.4, non da costruire.

**Il precedente da generalizzare esiste**: `GoldenDatasetLoaderTests` ha sostituito uno `Skip` fisso
con `Assert.SkipUnless(<prerequisito>, "<come abilitarlo>")`, motivando che *«uno skip che non dice
cosa fare equivale a un test cancellato»*. Applicato in un punto su cinquanta.

## 10. Fuori perimetro

- Azzerare i fallimenti già in baseline su `dev-async`.
- Implementare rotazione o confronto fra provider (D5): qui si chiede solo di non precluderli.
- Cambiare i filtri dei gate **bloccanti** (dev-fast, ci). L'aggiunta di un workflow
  `workflow_dispatch` per L4 e la classificazione delle categorie corrispondenti sono **dentro** il
  perimetro, perché sono l'unico percorso di attuazione di R4.2.
