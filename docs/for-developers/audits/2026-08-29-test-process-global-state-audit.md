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
funziona in fase di registrazione dei servizi**, e la cosa va detta perché non è intuitiva.

`WebApplicationFactory` applica i propri delegati (`ConfigureAppConfiguration`,
`ConfigureTestServices`) a `builder.Build()`, cioè **dopo** che `Program.cs` ha eseguito tutte le
`builder.Services.Add*`. Quando `AddRateLimitingServices(builder.Configuration)` legge, la
configurazione in-memory della factory non è ancora applicata. È esattamente ciò che affermava il
commento di #3102, ed è il motivo per cui quel fix ricorse a una variabile d'ambiente: era l'unico
canale che raggiungesse quella riga.

Verificato con un esperimento, non dedotto: `DefaultFactory_DisablesRateLimitingViaConfigurationOnly`
sonda 15 volte una policy da 10/min su una factory di default. Con l'interruttore letto in fase di
registrazione la chiamata #11 torna **429** — la configurazione in-memory non era stata letta.

**Soluzione**: l'interruttore si sposta dopo `Build()`. `WebApplicationExtensions` decide se
aggiungere `app.UseRateLimiter()` leggendo `app.Configuration`, che è la configurazione per-host
completa e include ancora la sorgente environment-variables — quindi in produzione un
`DISABLE_RATE_LIMITING` continua a funzionare identico. `AddRateLimitingServices` non legge più
alcuno stato di processo.

Corollario riusabile: **una configurazione che deve variare per host di test non può essere letta in
fase di registrazione dei servizi.** Va letta a request-time o dopo `Build()`. Lo stesso vale per
`ProviderCredentialResolver`, che risolve la chiave provider a request-time e per cui il passaggio a
`IConfiguration` funziona senza accorgimenti.

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

Tutti i 170 attributi `[Fact/Theory(Timeout < 90_000)]` della suite sono stati portati a `90_000`,
la convenzione già dominante (86 attributi). I 29 file coinvolti sono **tutti**
`TestCategories.Integration`: nessun test unit perde un guardrail stretto.

Il compromesso, esplicito: un hang reale ora impiega 90 s invece di 30 s prima di fallire. Alzare la
soglia non può però far fallire un test che passava — cambia esito solo per quelli che già la
superavano, cioè quelli che oggi contribuiscono al rumore.
