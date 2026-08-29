# Stato globale di processo nei test — audit #3887

**Data**: 2026-08-29 · **Issue**: [#3887](https://github.com/meepleAi-app/meepleai-monorepo/issues/3887)

## Il difetto

Un test il cui esito dipende da **quando** gira, non da **cosa** fa, sposta quale test fallisce a
ogni esecuzione. Su shard rossi in partenza l'unico segnale utilizzabile è il **confronto dei
falliti per nome** contro una baseline, e un rosso che si sposta rende rumoroso proprio quel
confronto: nella run post-merge di #3884/#3885 `Probe_RateLimitExceeded_Returns429` è comparso come
regressione senza esserlo.

Due meccanismi distinti producono lo stesso effetto.

### 1. Variabile d'ambiente di processo mutata e ripristinata dentro un test

`Environment.SetEnvironmentVariable` è globale al processo. xUnit esegue le collection in parallelo,
quindi una mutazione **con ripristino** dentro un `[Fact]` apre una finestra: ogni host costruito da
un'altra collection in quell'intervallo eredita il valore alterato.

Il caso concreto: `DISABLE_RATE_LIMITING`. Tre punti azzeravano la variabile —
`AdminProviderEndpointsIntegrationTests.Probe_RateLimitExceeded_Returns429`,
`GameNightTokenRateLimitTests.RespondByToken_ExceedsRateLimit_Returns429WithRetryAfter` (in un
`finally`) e `FrontendSdkTestFactory.DisposeAsync` (al teardown, senza ripristino) — perché avevano
bisogno del rate limiting **acceso** per il proprio host. Ogni altro host costruito in quella
finestra prendeva 429 al posto del proprio esito.

Il criterio di triage **non** è «usa `SetEnvironmentVariable`»: mutarla *prima* di costruire l'host
e non ripristinarla è sbagliato ma deterministico. È **«la muta e la ripristina mentre altri test
possono costruire un host»**.

### 2. `[Fact(Timeout = N)]` troppo stretto per una fixture Testcontainers

`TwoFactorSecurityPenetrationTests.BruteForce_RapidFireAttack_ShouldBeRateLimited` portava
`Timeout = 10000` (messo da #1850 per fermare un hang da 12 minuti). Dieci secondi non bastano: il
test andava in timeout **anche su `main-dev`**, in isolamento. Passa quando la fixture condivisa è
calda e la run è veloce, fallisce quando non lo è.

## Il vincolo che ha determinato la soluzione

La correzione ovvia — leggere l'interruttore da `IConfiguration` invece che dal processo — **non
funziona in fase di registrazione dei servizi**, e la cosa va detta perche' non e' intuitiva.

`WebApplicationFactory` applica i propri delegati (`ConfigureAppConfiguration`,
`ConfigureTestServices`) a `builder.Build()`, cioe' **dopo** che `Program.cs` ha eseguito tutte le
`builder.Services.Add*`. Quando `AddRateLimitingServices(builder.Configuration)` legge, la
configurazione in-memory della factory non e' ancora applicata. E' esattamente cio' che affermava il
commento di #3102, ed e' il motivo per cui quel fix ricorse a una variabile d'ambiente: era l'unico
canale che raggiungesse quella riga.

Verificato con un esperimento, non dedotto: `DefaultFactory_DisablesRateLimitingViaConfigurationOnly`
sonda 15 volte una policy da 10/min su una factory di default. Con l'interruttore letto in fase di
registrazione la chiamata #11 torna **429** — la configurazione in-memory non era stata letta.

### La prima correzione non bastava, e il modo in cui falliva e' istruttivo

Spostare *solo* l'interruttore del middleware dopo `Build()`, lasciando in registrazione la scelta
fra policy reali e policy `NoLimiter`, crea **due letture della stessa cosa da due snapshot
diversi**. Quella in registrazione risolve ancora contro lo stato di processo, perche'
`configuration.GetValue("RateLimiting:Enabled")` legge anche la variabile d'ambiente
`RateLimiting__Enabled` — che `E2ETestBase` e `RouteContractTestFactory` impostano a `false` una
volta per sempre.

Riprodotto in modo deterministico con `RateLimiting__Enabled=false dotnet test --filter
AdminProviderProbeRateLimit`: il test 429 **falliva**, con 11 richieste su 11 a 200. L'host aveva
`UseRateLimiter()` montato sopra policy che non limitano nulla — un guasto **silenzioso**: nessun
log, nessuna eccezione, nessuna asserzione rossa se non quella. E il guard test non poteva vederlo,
perche' sulla factory di default il middleware viene saltato in entrambi gli scenari.

**Soluzione definitiva**: `AddRateLimitingServices` registra **sempre** le policy reali e non legge
alcun interruttore — il parametro `IConfiguration` e' stato rimosso perche' non restava nulla da
leggere. L'unico punto di decisione e' `WebApplicationExtensions.ConfigureAuthMiddleware`, che dopo
`Build()` legge `app.Configuration` (per-host, e comprensiva della sorgente environment-variables,
quindi in produzione nulla cambia) e decide se aggiungere `app.UseRateLimiter()`. Senza middleware
non c'e' throttling: le policy registrate restano inerti. Con un solo interruttore le due letture
non possono piu' divergere. Stesso comando di riproduzione, dopo: **verde**.

Corollario riusabile: **una configurazione che deve variare per host di test non puo' essere letta
in fase di registrazione dei servizi.** Va letta a request-time o dopo `Build()` — e va letta in
**un solo** punto, perche' due letture della stessa cosa da snapshot diversi falliscono in silenzio.
Lo stesso vale per `ProviderCredentialResolver`, che risolve la chiave provider a request-time e per
cui il passaggio a `IConfiguration` funziona senza accorgimenti (via `SecretsHelper.GetSecretOrValue`,
come ogni altro consumer di quelle chiavi, cosi' la convenzione Docker `<KEY>_FILE` resta onorata).

## Triage delle occorrenze residue

`grep -rn "Environment.SetEnvironmentVariable" apps/api/tests --include=*.cs`

Le occorrenze rimaste **non** soddisfano il criterio pericoloso, per una delle due ragioni:

| Gruppo | File | Perché non è una finestra |
|---|---|---|
| Ctor statici di factory | `E2ETestBase`, `FrontendSdkTestFactory`, `RouteContractTestFactory` | impostano un valore prima di costruire l'host e **non lo ripristinano**: sbagliato ma deterministico. Le tre factory dichiarano ora anche l'interruttore nella propria config in-memory |
| Unit test con ripristino | `SecretsHelperTests`, `SeedOrchestratorTests`, `MeepleAiDbContextFactoryTests`, `SeedAdminUserCommandHandlerTests` | mutano `POSTGRES_*`, `SEED_PROFILE`, `CONNECTIONSTRINGS__POSTGRES`, `INITIAL_ADMIN_EMAIL` e rileggono tramite una `IConfiguration` costruita localmente o un helper statico. Nessun host viene costruito da quel codice, e le factory che costruiscono host o azzerano le sorgenti (`configBuilder.Sources.Clear()`) o antepongono la propria collection in-memory alla sorgente environment |

Restano quindi come debito **latente**, non come causa attiva: se una futura factory leggesse
`ConnectionStrings__Postgres` o `ASPNETCORE_ENVIRONMENT` dal processo senza azzerare le sorgenti,
quelle mutazioni tornerebbero pericolose.

## Timeout

168 dei 170 attributi `[Fact/Theory(Timeout < 90_000)]` della suite sono stati portati a `90_000`,
la convenzione già dominante (86 attributi). I 29 file coinvolti sono **tutti**
`TestCategories.Integration`: nessun test unit perde un guardrail stretto.

**Due eccezioni esplicite**: `Unstructured_LargeFile_TerraformingMars_HandlesStressTest` e il
corrispettivo in `SmolDoclingIntegrationTests` restano a **60 s**. Lì la soglia non è impalcatura:
è il tetto di prestazione asserito per l'estrazione di PDF grandi, e non c'è altra asserzione sulla
durata. Allargarla farebbe passare in verde un rallentamento del 50%.

Il compromesso, esplicito: un hang reale ora impiega 90 s invece di 30 s prima di fallire. Alzare la
soglia non può però far fallire un test che passava — cambia esito solo per quelli che già la
superavano, cioè quelli che oggi contribuiscono al rumore.

## Due residui noti, non chiusi qui

- **`BruteForce_RapidFireAttack_ShouldBeRateLimited`** non è reso deterministico dall'alzata del
  timeout. Il suo ciclo è auto-limitato a 1 s, quindi i 10 s originali morivano su `InitializeAsync`;
  ma l'asserzione è `attemptsPerSecond > 10` su chiamate reali al DB. Sotto contesa a ~200 ms per
  chiamata il rapporto scende a ~5 e il test diventa rosso per asserzione invece che per timeout.
  Renderlo deterministico richiede di iniettare un `TimeProvider`, come già annotato in #3601 per il
  caso gemello.
- **`ASPNETCORE_ENVIRONMENT` ha due scrittori con valori diversi**: `E2ETestBase` lo mette a `"CI"`,
  `IntegrationWebApplicationFactory.Create` a `"Testing"` a ogni chiamata. Nessuno dei due ripristina,
  quindi il criterio di triage qui sopra li classifica entrambi come innocui — ma il valore che un
  host vede dipende dall'interleaving, e conta in registrazione (`WebApplication.CreateBuilder`
  sceglie `appsettings.{Environment}.json` da lì; `appsettings.CI.json` esiste,
  `appsettings.Testing.json` no). Il criterio va quindi esteso: **anche due scrittori con valori
  diversi aprono una finestra**, non solo il muta-e-ripristina.
