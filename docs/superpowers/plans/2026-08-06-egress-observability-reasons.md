# Egress observability reasons (#3583) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rendere visibili su `meepleai_egress_blocked_total` tre esiti di egress che oggi falliscono chiusi in silenzio — fallimento DNS, rifiuto del decoder immagine, hit sulla deny-list BGG.

**Architecture:** Nessun cambio di comportamento: ogni percorso già rifiuta correttamente. Si aggiunge una `RecordEgressBlocked` immediatamente **prima** del throw esistente, con i soli due tag bounded `{sink, reason}`. Il `sink` è sempre noto al chiamante, mai al componente generico: per questo `decode_fail` viene wirato ai call site alimentati da rete e non dentro `WebpVariantGenerator`. In coda, lo split dell'alert Prometheus separa `denylist_hit` (violazione di policy) da `private_ip` (incidente SSRF).

**Tech Stack:** .NET 9, xUnit + FluentAssertions + Moq, `System.Diagnostics.Metrics` (`MeterListener`), Prometheus + promtool.

## Global Constraints

- I tag delle metriche egress sono **esclusivamente** `sink` e `reason`, entrambi costanti da `MeepleAiMetrics.EgressSinks` / `MeepleAiMetrics.EgressBlockReasons`. Un host o un IP non è **mai** un tag (cardinalità illimitata + leak del target).
- Ogni `RecordEgressBlocked` va **prima** del `throw`, e il throw resta invariato: nessuna eccezione viene inghiottita, nessun esito cambia.
- La cancellazione iniziata dal chiamante non è un fallimento di egress e non va contata.
- I test asseriscono selezionando la misurazione **per tag**, mai assumendo che la finestra di cattura sia esclusiva: il `MeterListener` è process-wide e xUnit esegue le classi in parallelo (regressione già occorsa in #3495 Slice D).
- Working dir dei comandi: `D:/Repositories/meepleai-monorepo-main/.claude/worktrees/i3583`. Branch: `feature/issue-3583-egress-observability-reasons`.
- Comando test di riferimento (da `apps/api`): `dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "<filtro>"`.

## File Structure

| File | Responsabilità | Azione |
|---|---|---|
| `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Egress.cs` | Costanti bounded delle reason | Modifica: aggiunge `DnsFailure` |
| `apps/api/src/Api/SharedKernel/Infrastructure/Http/SsrfPinnedConnect.cs` | Chokepoint connect-pin | Modifica: try/catch sulla risoluzione |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/SetManualCoverCommandHandler.cs` | Cover manuale da URL admin | Modifica: `decode_fail` + `denylist_hit` |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/SetManualCoverCommandValidator.cs` | Gate di boundary del path manuale | Modifica: `denylist_hit` |
| `.../Commands/EnrichCatalogCover/EnrichCatalogCoverCommandHandler.cs` | Enrichment Wikidata/Commons | Modifica: `decode_fail` |
| `apps/api/tests/Api.Tests/Observability/MetricCapture.cs` | Helper condiviso di cattura misurazioni | **Crea** |
| `apps/api/tests/Api.Tests/Observability/EgressMetricsTests.cs` | Test del chokepoint | Modifica: usa l'helper, aggiunge i test DNS |
| `.../Commands/SetManualCoverCommandHandlerTests.cs` | Test handler manuale | Modifica: test `decode_fail` |
| `.../EnrichCatalogCover/EnrichCatalogCoverCommandHandlerTests.cs` | Test handler enrichment | Modifica: test `decode_fail` |
| `.../Commands/SetManualCoverCommandValidatorTests.cs` | Test validator | Modifica: test `denylist_hit` |
| `infra/prometheus/alerts/egress-guard.yml` | Alert egress | Modifica: split in due regole |
| `infra/prometheus/alerts/egress-guard.test.yml` | Test promtool | Modifica: casi per le due regole |
| `infra/prometheus.staging.yml` · `infra/prometheus.prod.yml` | `rule_files:` per ambiente | Modifica: caricano `egress-guard.yml`, oggi assente |

---

### Task 1: reason `dns_failure` sul chokepoint

**Files:**
- Modify: `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Egress.cs:40`
- Modify: `apps/api/src/Api/SharedKernel/Infrastructure/Http/SsrfPinnedConnect.cs:30`
- Create: `apps/api/tests/Api.Tests/Observability/MetricCapture.cs`
- Modify: `apps/api/tests/Api.Tests/Observability/EgressMetricsTests.cs`

**Interfaces:**
- Consumes: `IDnsResolver.ResolveAsync(string host, CancellationToken ct) -> Task<IReadOnlyList<IPAddress>>`; `MeepleAiMetrics.RecordEgressBlocked(string sink, string reason)`.
- Produces: costante `MeepleAiMetrics.EgressBlockReasons.DnsFailure` (valore `"dns_failure"`), usata dai task successivi solo come riferimento di stile. Helper `Api.Tests.Observability.MetricCapture.Capture(string instrumentName, Action act) -> List<(long Value, IReadOnlyDictionary<string, object?> Tags)>`, consumato dai Task 2 e 3.

- [ ] **Step 1: creare l'helper condiviso di cattura**

Crea `apps/api/tests/Api.Tests/Observability/MetricCapture.cs`. È l'estrazione verbatim del metodo `Capture` già presente in `EgressMetricsTests.cs:28-56`, così i test dei Task 2 e 3 (che vivono in altri file) non lo duplicano.

```csharp
using System.Diagnostics.Metrics;

namespace Api.Tests.Observability;

/// <summary>
/// Cattura le misurazioni di uno strumento durante l'esecuzione di <paramref name="act"/>.
/// Estratto da EgressMetricsTests in #3583 perché i test di decode_fail/denylist_hit vivono
/// nei file dei rispettivi handler/validator.
/// <para>
/// ATTENZIONE: il MeterListener è process-wide e xUnit esegue le classi di test in parallelo, per
/// cui la finestra di cattura può contenere misurazioni di altri test. Chi asserisce DEVE
/// selezionare la propria misurazione per tag, non assumere che la finestra sia esclusiva.
/// </para>
/// </summary>
internal static class MetricCapture
{
    public static List<(long Value, IReadOnlyDictionary<string, object?> Tags)> Capture(
        string instrumentName, Action act)
    {
        var captured = new List<(long, IReadOnlyDictionary<string, object?>)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == instrumentName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var t in tags)
            {
                dict[t.Key] = t.Value;
            }
            captured.Add((value, dict));
        });
        listener.Start();

        act();

        return captured;
    }
}
```

- [ ] **Step 2: far usare l'helper a EgressMetricsTests**

In `apps/api/tests/Api.Tests/Observability/EgressMetricsTests.cs`, elimina il metodo privato `Capture` (righe 28-56) e sostituisci le **due** chiamate `Capture(` (righe 63 e 105) con `MetricCapture.Capture(`. Il terzo `[Fact]`, `Counters_HaveStableBoundedNames`, non usa `Capture`.

Rimuovi anche il `using System.Diagnostics.Metrics;` in testa al file: era lì solo per `MeterListener`, che ora vive nell'helper, e resterebbe orfano (IDE0005).

Nessun altro cambiamento: è un refactor meccanico che i test esistenti devono confermare verde.

- [ ] **Step 3: eseguire i test esistenti per provare che il refactor non regredisce**

Da `apps/api`:

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~EgressMetrics"
```

Atteso: PASS, 3 test.

- [ ] **Step 4: scrivere i due test che falliscono**

Aggiungi in `EgressMetricsTests.cs`, dentro la classe, subito dopo la classe annidata `FakeDnsResolver`:

```csharp
    private sealed class ThrowingDnsResolver : IDnsResolver
    {
        private readonly Exception _toThrow;
        public ThrowingDnsResolver(Exception toThrow) => _toThrow = toThrow;
        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken ct) =>
            Task.FromException<IReadOnlyList<IPAddress>>(_toThrow);
    }
```

e i due fatti in coda alla classe:

```csharp
    [Fact]
    public void ResolveAndValidate_DnsThrows_IncrementsBlocked_WithDnsFailure_AndRethrows()
    {
        // Un NXDOMAIN / timeout del resolver: la connessione non avviene (fail-closed corretto), ma
        // senza questo counter il sink degradato è indistinguibile da un sink inattivo (#3583).
        var dns = new ThrowingDnsResolver(new System.Net.Sockets.SocketException(11001));

        var captured = MetricCapture.Capture(MeepleAiMetrics.EgressBlocked.Name, () =>
        {
            try
            {
                SsrfPinnedConnect.ResolveAndValidateAsync(
                        dns, "nxdomain.example.com", MeepleAiMetrics.EgressSinks.Wikidata, CancellationToken.None)
                    .GetAwaiter().GetResult();
                throw new Xunit.Sdk.XunitException("expected the DNS failure to propagate");
            }
            catch (System.Net.Sockets.SocketException)
            {
                // atteso — il guard registra e RILANCIA senza alterare l'esito
            }
        });

        var mine = captured
            .Where(c => Equals(c.Tags.GetValueOrDefault("sink"), "wikidata")
                && Equals(c.Tags.GetValueOrDefault("reason"), "dns_failure"))
            .ToList();

        mine.Should().NotBeEmpty("un fallimento DNS deve essere visibile sul counter di blocco");
        mine[0].Value.Should().Be(1);
        captured.Should().AllSatisfy(c => c.Tags.Keys.Should().BeEquivalentTo("sink", "reason"));
    }

    [Fact]
    public void ResolveAndValidate_CallerCancellation_DoesNotIncrementBlocked()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var dns = new ThrowingDnsResolver(new OperationCanceledException(cts.Token));

        var captured = MetricCapture.Capture(MeepleAiMetrics.EgressBlocked.Name, () =>
        {
            try
            {
                SsrfPinnedConnect.ResolveAndValidateAsync(
                        dns, "cancelled.example.com", MeepleAiMetrics.EgressSinks.Wikimedia, cts.Token)
                    .GetAwaiter().GetResult();
                throw new Xunit.Sdk.XunitException("expected the cancellation to propagate");
            }
            catch (OperationCanceledException)
            {
                // atteso
            }
        });

        captured
            .Where(c => Equals(c.Tags.GetValueOrDefault("sink"), "wikimedia")
                && Equals(c.Tags.GetValueOrDefault("reason"), "dns_failure"))
            .Should().BeEmpty("un abort del chiamante non è un fallimento DNS e non va contato");
    }
```

Estendi anche l'asserzione sui nomi stabili, in `Counters_HaveStableBoundedNames`. Non solo la costante nuova: dal Task 4 in poi queste stringhe sono un **contratto con i selettori delle regole Prometheus** (`reason="private_ip"`, `reason="denylist_hit"`), quindi un rename della costante renderebbe gli alert silenziosamente morti senza che nessun test se ne accorga.

```csharp
        MeepleAiMetrics.EgressBlockReasons.DnsFailure.Should().Be("dns_failure");
        // Pinnati perché sono il selettore delle regole in infra/prometheus/alerts/egress-guard.yml:
        // rinominarli spegnerebbe gli alert senza rompere nulla in compilazione (#3583).
        MeepleAiMetrics.EgressBlockReasons.PrivateIp.Should().Be("private_ip");
        MeepleAiMetrics.EgressBlockReasons.DenylistHit.Should().Be("denylist_hit");
        MeepleAiMetrics.EgressBlockReasons.DecodeFail.Should().Be("decode_fail");
```

- [ ] **Step 5: eseguire i test per verificare che falliscano**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~EgressMetrics"
```

Atteso: FALLISCE in compilazione, con `'EgressBlockReasons' does not contain a definition for 'DnsFailure'`.

- [ ] **Step 6: aggiungere la costante**

In `MeepleAiMetrics.Egress.cs`, subito dopo `DecodeFail` (riga 40):

```csharp
        /// <summary>La risoluzione DNS ha lanciato (NXDOMAIN, timeout del resolver, socket error) — #3583.</summary>
        public const string DnsFailure = "dns_failure";
```

- [ ] **Step 7: eseguire i test — ora compilano ma il primo fallisce**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~EgressMetrics"
```

Atteso: `ResolveAndValidate_DnsThrows_...` FALLISCE su `mine.Should().NotBeEmpty(...)`. `ResolveAndValidate_CallerCancellation_...` passa già (nessuno registra nulla oggi) — è un test di non-regressione, è corretto che sia verde fin da subito.

- [ ] **Step 8: implementare il guard**

In `SsrfPinnedConnect.cs`, sostituisci la riga 30:

```csharp
        IReadOnlyList<IPAddress> addresses = await dns.ResolveAsync(host, ct).ConfigureAwait(false);
```

con:

```csharp
        IReadOnlyList<IPAddress> addresses;
        try
        {
            addresses = await dns.ResolveAsync(host, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // #3583 — l'esito era già fail-closed (la connessione non avviene), ma senza questo
            // counter un sink che degrada per problemi DNS è indistinguibile da un sink inattivo su
            // meepleai_egress_blocked_total. Registra e RILANCIA: nessun esito cambia.
            //
            // Il filtro esclude la cancellazione iniziata dal chiamante — che non è un guasto di
            // egress — pur continuando a contare un timeout interno del resolver (che si presenta
            // come OperationCanceledException con un CTS proprio, quindi con `ct` non cancellato).
            //
            // Due limiti noti e accettati:
            //   - se il chiamante cancella e il resolver risponde con SocketException invece che
            //     OperationCanceledException, il caso viene contato come dns_failure;
            //   - questo guard gira dentro il ConnectCallback, quindi UNA VOLTA PER CONNESSIONE:
            //     ogni hop di redirect e ogni nuova connessione del pool incrementa. Il counter va
            //     letto come rate di fallimenti di dial, MAI come "richieste utente fallite".
            MeepleAiMetrics.RecordEgressBlocked(sink, MeepleAiMetrics.EgressBlockReasons.DnsFailure);
            throw;
        }
```

- [ ] **Step 9: eseguire i test per verificare che passino**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~EgressMetrics"
```

Atteso: PASS, 5 test.

- [ ] **Step 10: verificare che il build non abbia introdotto warning**

Da `apps/api/src/Api`:

```bash
dotnet build --nologo -v q
```

Atteso: `Avvisi: 0`, `Errori: 0`.

CA1031 **non** dovrebbe scattare: lo stesso idioma `catch (Exception ex) when (...)` è già in uso in `EnrichCatalogCoverCommandHandler.cs:195` sotto lo stesso `TreatWarningsAsErrors`. Se comparisse comunque un warning, riportalo testualmente invece di sopprimerlo alla cieca.

Nota: questo build copre solo `src/Api`. Gli errori nel progetto di test emergono soltanto con `dotnet test`, già eseguito allo Step 9.

- [ ] **Step 11: commit**

```bash
git add apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Egress.cs \
        apps/api/src/Api/SharedKernel/Infrastructure/Http/SsrfPinnedConnect.cs \
        apps/api/tests/Api.Tests/Observability/MetricCapture.cs \
        apps/api/tests/Api.Tests/Observability/EgressMetricsTests.cs
git commit -m "feat(egress): conta le eccezioni DNS come dns_failure (#3583)"
```

---

### Task 2: reason `decode_fail` sui due call site di rete

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/SetManualCoverCommandHandler.cs:84-86`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCover/EnrichCatalogCoverCommandHandler.cs:221`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/SetManualCoverCommandHandlerTests.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCover/EnrichCatalogCoverCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `MetricCapture.Capture` (Task 1); `MeepleAiMetrics.EgressBlockReasons.DecodeFail` (già esistente, valore `"decode_fail"`); `ImageProcessingException` da `Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services`.
- Produces: nulla di consumato da task successivi.

**Perché qui e non in `WebpVariantGenerator`:** il generator non conosce il `sink` e non potrebbe taggare la metrica senza cambiare `IWebpVariantGenerator`; inoltre il suo terzo chiamante — `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/MaterializePdfCoverCommandHandler.cs:77`, nel bounded context **DocumentProcessing**, non in SharedGameCatalog — decodifica un PDF **locale**, dove un decode fallito non è un blocco di egress e falserebbe il rapporto blocked/allowed. Entrambi i rifiuti citati da #3583 — decompression bomb (`WebpVariantGenerator.cs:119-128`) e coder non-raster (`WebpVariantGenerator.cs:100-108`) — risalgono comunque come `ImageProcessingException`, quindi wirare il catch al call site li copre entrambi.

- [ ] **Step 1: scrivere il test che fallisce sul path manuale**

In `SetManualCoverCommandHandlerTests.cs` aggiungi in coda alla classe. Il fixture monta il **vero** `WebpVariantGenerator`, quindi basta far restituire allo stub HTTP un payload non-raster perché il decoder lo rifiuti:

```csharp
    [Fact]
    public void Handle_NonRasterPayload_RecordsDecodeFail_OnManualSink()
    {
        var game = NewGame();
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        // SVG: coder non-raster, rifiutato da DetectRasterFormat prima di raggiungere Magick (#3495 M1).
        // Lo StubImageHandler dichiara Content-Type: image/png e il fetch non ispeziona i magic bytes,
        // quindi il payload arriva davvero al decoder.
        _httpHandler.ImageBytes = System.Text.Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"10\" height=\"10\"/></svg>");

        var captured = Api.Tests.Observability.MetricCapture.Capture(
            MeepleAiMetrics.EgressBlocked.Name,
            () =>
            {
                try
                {
                    CreateHandler().Handle(Command(game.Id), CancellationToken.None)
                        .GetAwaiter().GetResult();
                    throw new Xunit.Sdk.XunitException(
                        "expected the decoder to reject the non-raster payload");
                }
                catch (ImageProcessingException)
                {
                    // atteso — l'handler registra e RILANCIA
                }
            });

        captured
            .Where(c => Equals(c.Tags.GetValueOrDefault("sink"), "manual")
                && Equals(c.Tags.GetValueOrDefault("reason"), "decode_fail"))
            .Should().NotBeEmpty("il rifiuto del decoder su un payload scaricato è un blocco di egress");
    }
```

Il metodo è volutamente `void` e non `async Task`: non c'è nulla da attendere, e `MetricCapture.Capture` accetta una `Action` sincrona. Lo stile `try/catch` + `GetAwaiter().GetResult()` è lo stesso del Task 1 — evita di mescolare due idiomi e, se `Handle` lanciasse un'eccezione diversa da quella attesa, produce un messaggio d'errore che punta al tipo sbagliato invece di far esplodere l'assertion dentro la lambda con una diagnostica fuorviante.

Aggiungi in testa al file i `using` mancanti:

```csharp
using Api.Observability;
```

- [ ] **Step 2: eseguire il test per verificare che fallisca**

Da `apps/api`:

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~SetManualCoverCommandHandlerTests.Handle_NonRasterPayload_RecordsDecodeFail_OnManualSink"
```

Atteso: FALLISCE su `Should().NotBeEmpty(...)` — l'eccezione risale già correttamente, manca solo il counter.

- [ ] **Step 3: implementare sul path manuale**

In `SetManualCoverCommandHandler.cs` aggiungi `using Api.Observability;` alla lista dei using, poi sostituisci le righe 84-86:

```csharp
        var webpBytes = await _webpGenerator
            .GenerateWebpAsync(imageBytes, WebpTargetWidth, WebpTargetHeight, cancellationToken)
            .ConfigureAwait(false);
```

con:

```csharp
        byte[] webpBytes;
        try
        {
            webpBytes = await _webpGenerator
                .GenerateWebpAsync(imageBytes, WebpTargetWidth, WebpTargetHeight, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ImageProcessingException)
        {
            // #3583 — il payload scaricato è stato rifiutato dal decoder (decompression bomb o coder
            // non-raster, #3495 M1). Il rifiuto è corretto: qui lo si rende visibile.
            MeepleAiMetrics.RecordEgressBlocked(
                MeepleAiMetrics.EgressSinks.Manual, MeepleAiMetrics.EgressBlockReasons.DecodeFail);
            throw;
        }
```

- [ ] **Step 4: eseguire il test per verificare che passi**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~SetManualCoverCommandHandlerTests"
```

Atteso: PASS, tutti i test della classe.

- [ ] **Step 5: scrivere il test che fallisce sul path enrichment**

In `EnrichCatalogCoverCommandHandlerTests.cs`, il test esistente `Handle_ImageProcessingError_ReturnsFailedImageProcessingError` (righe 371-402) copre già l'esito. Aggiungi questo fatto sibling, che riusa il medesimo arrangiamento:

```csharp
    [Fact]
    public void Handle_ImageProcessingError_RecordsDecodeFail_OnWikimediaSink()
    {
        // Stesso arrangiamento di Handle_ImageProcessingError_ReturnsFailedImageProcessingError.
        // L'harness monta il VERO WebpVariantGenerator: sono i byte corrotti scaricati da Commons a
        // farlo fallire (DetectRasterFormat -> null), non un mock che lancia.
        var harness = BuildHarness();
        var game = BuildGame(qid: TestQid);
        harness.RepoMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        harness.WikidataHandler.SparqlJson = BuildSparqlImageResponse(TestFilename);
        harness.CommonsHandler.LicenseJson = BuildImageInfoResponse("CC0");
        harness.CommonsHandler.ImageBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE };

        EnrichCatalogCoverResult? result = null;
        var captured = Api.Tests.Observability.MetricCapture.Capture(
            MeepleAiMetrics.EgressBlocked.Name,
            () => result = harness.Sut
                .Handle(new EnrichCatalogCoverCommand(game.Id), CancellationToken.None)
                .GetAwaiter().GetResult());

        // L'handler CATTURA ImageProcessingException e ritorna Failed: nessuna eccezione esce,
        // quindi qui NON serve try/catch (a differenza del path manuale del Task 2 Step 1).
        result.Should().BeOfType<EnrichCatalogCoverResult.Failed>();

        captured
            .Where(c => Equals(c.Tags.GetValueOrDefault("sink"), "wikimedia")
                && Equals(c.Tags.GetValueOrDefault("reason"), "decode_fail"))
            .Should().NotBeEmpty("il rifiuto del decoder su un'immagine Commons è un blocco di egress");
    }
```

- [ ] **Step 6: eseguire il test per verificare che fallisca**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~EnrichCatalogCoverCommandHandlerTests.Handle_ImageProcessingError_RecordsDecodeFail_OnWikimediaSink"
```

Atteso: FALLISCE su `Should().NotBeEmpty(...)`.

- [ ] **Step 7: implementare sul path enrichment**

In `EnrichCatalogCoverCommandHandler.cs`, dentro il `catch (ImageProcessingException ex)` già presente alla riga 221, come **prima** istruzione del blocco:

```csharp
            // #3583 — il payload scaricato da Commons è stato rifiutato dal decoder. L'esito
            // (Failed/image_processing) resta invariato; qui si aggiunge la visibilità su egress.
            MeepleAiMetrics.RecordEgressBlocked(
                MeepleAiMetrics.EgressSinks.Wikimedia, MeepleAiMetrics.EgressBlockReasons.DecodeFail);
```

Il file ha già `using Api.Observability;` — non aggiungerlo.

- [ ] **Step 8: eseguire i test per verificare che passino**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~EnrichCatalogCoverCommandHandlerTests|FullyQualifiedName~SetManualCoverCommandHandlerTests"
```

Atteso: PASS.

- [ ] **Step 9: commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/SetManualCoverCommandHandler.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCover/EnrichCatalogCoverCommandHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/SetManualCoverCommandHandlerTests.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCover/EnrichCatalogCoverCommandHandlerTests.cs
git commit -m "feat(egress): conta i rifiuti del decoder come decode_fail (#3583)"
```

---

### Task 3: reason `denylist_hit` sui due gate della deny-list BGG

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/SetManualCoverCommandValidator.cs:27`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/SetManualCoverCommandHandler.cs:73`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/SetManualCoverCommandValidatorTests.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/SetManualCoverCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `MetricCapture.Capture` (Task 1); `BggHostDenyList.IsBanned(string? url) -> bool`; `MeepleAiMetrics.EgressBlockReasons.DenylistHit` (già esistente, valore `"denylist_hit"`).
- Produces: nulla di consumato da task successivi.

**Perché entrambi i gate:** sono mutuamente esclusivi — se il validator rifiuta, l'handler non gira — quindi una richiesta conta esattamente una volta. Il gate del validator cattura il caso reale (admin che incolla un URL BGG); quello dell'handler cattura la difesa in profondità (un percorso che raggiunge l'handler saltando la pipeline FluentValidation). Wirarne uno solo lascerebbe cieco l'altro.

- [ ] **Step 1: scrivere il test che fallisce sul validator**

In `SetManualCoverCommandValidatorTests.cs` aggiungi in coda alla classe, usando l'idioma già in uso nel file (`_validator.TestValidate(Valid() with { … })`, non `new SetManualCoverCommand(...)` + `.Validate(...)`):

```csharp
    [Fact]
    public void Validate_BggHost_RecordsDenylistHit_OnManualSink()
    {
        // NOTA: il file contiene già Fails_WhenSourceUrlIsBannedBggHost con 5 [InlineData], ognuna
        // delle quali emetterà una misurazione {manual, denylist_hit}. La finestra di MetricCapture è
        // process-wide: asserire NotBeEmpty, MAI un conteggio esatto.
        var captured = Api.Tests.Observability.MetricCapture.Capture(
            MeepleAiMetrics.EgressBlocked.Name,
            () => _validator
                .TestValidate(Valid() with { SourceUrl = "https://cf.geekdo-images.com/abc/cover.jpg" })
                .ShouldHaveValidationErrorFor(x => x.SourceUrl));

        captured
            .Where(c => Equals(c.Tags.GetValueOrDefault("sink"), "manual")
                && Equals(c.Tags.GetValueOrDefault("reason"), "denylist_hit"))
            .Should().NotBeEmpty("un tentativo di aggirare il ban BGG deve essere visibile");
    }
```

Aggiungi in testa al file, se assenti:

```csharp
using Api.Observability;
```

- [ ] **Step 2: eseguire il test per verificare che fallisca**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~SetManualCoverCommandValidatorTests.Validate_BggHost_RecordsDenylistHit_OnManualSink"
```

Atteso: FALLISCE su `Should().NotBeEmpty(...)` — il rifiuto avviene già, manca il counter.

- [ ] **Step 3: implementare sul validator**

In `SetManualCoverCommandValidator.cs` aggiungi `using Api.Observability;`, sostituisci la riga 27:

```csharp
            .Must(url => !BggHostDenyList.IsBanned(url))
```

con:

```csharp
            .Must(url => !IsBannedAndRecorded(url))
```

e aggiungi il metodo privato accanto a `BeAbsoluteHttps`:

```csharp
    /// <summary>
    /// #3583 — registra l'hit sulla deny-list ADR-059 §5 prima di far fallire la regola, così un
    /// tentativo ripetuto di laundering attorno al ban BGG è visibile su
    /// <c>meepleai_egress_blocked_total</c>. Il predicato conserva la semantica originale:
    /// ritorna true quando l'URL è bandito (quindi la regola `Must(!…)` fallisce).
    /// <para>
    /// PRECONDIZIONE per la correttezza del conteggio: questo predicato viene valutato ESATTAMENTE
    /// una volta per richiesta. Oggi è vero perché (a) il cascade rule-level è il default `Continue`
    /// e la catena su SourceUrl esegue questo Must una sola volta, e (b) il validator è invocato solo
    /// dal <c>ValidationBehavior</c> di MediatR, una volta per comando. È una garanzia EMERGENTE, non
    /// protetta da un test: un secondo <c>IValidator&lt;SetManualCoverCommand&gt;</c> registrato, o un
    /// endpoint filter che validi prima di MediatR, farebbero raddoppiare il counter in silenzio.
    /// </para>
    /// </summary>
    private static bool IsBannedAndRecorded(string? url)
    {
        if (!BggHostDenyList.IsBanned(url))
        {
            return false;
        }

        MeepleAiMetrics.RecordEgressBlocked(
            MeepleAiMetrics.EgressSinks.Manual, MeepleAiMetrics.EgressBlockReasons.DenylistHit);
        return true;
    }
```

- [ ] **Step 4: eseguire i test del validator per verificare che passino**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~SetManualCoverCommandValidatorTests"
```

Atteso: PASS, tutti i test della classe (i test esistenti sul rifiuto BGG devono restare verdi: la semantica del predicato non cambia).

- [ ] **Step 5: implementare sul gate difensivo dell'handler**

In `SetManualCoverCommandHandler.cs` (che dopo il Task 2 ha già `using Api.Observability;`), sostituisci le righe 73-77:

```csharp
        if (BggHostDenyList.IsBanned(command.SourceUrl))
        {
            throw new ArgumentException(
                "SourceUrl host is banned by ADR-059 §5 (BGG/geekdo assets).", nameof(command));
        }
```

con:

```csharp
        if (BggHostDenyList.IsBanned(command.SourceUrl))
        {
            // #3583 — raggiungibile solo se un percorso salta la pipeline FluentValidation (il
            // validator rifiuta prima). Mutuamente esclusivo con il gate del validator, quindi una
            // richiesta conta una volta sola.
            MeepleAiMetrics.RecordEgressBlocked(
                MeepleAiMetrics.EgressSinks.Manual, MeepleAiMetrics.EgressBlockReasons.DenylistHit);
            throw new ArgumentException(
                "SourceUrl host is banned by ADR-059 §5 (BGG/geekdo assets).", nameof(command));
        }
```

- [ ] **Step 5b: scrivere ed eseguire il test del gate difensivo dell'handler**

La spec chiede test su validator **e** handler. `SetManualCoverCommandHandlerTests.cs` non contiene oggi alcun test con un URL BGG, quindi senza questo il ramo modificato allo Step 5 resta non eseguito. Aggiungi in coda alla classe:

```csharp
    [Fact]
    public void Handle_BggHost_RecordsDenylistHit_AndNeverFetches()
    {
        // Gate difensivo: raggiungibile solo saltando la pipeline FluentValidation, quindi qui si
        // invoca l'handler direttamente. Deve rifiutare PRIMA di qualunque egress.
        var game = NewGame();
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var banned = Command(game.Id) with { SourceUrl = "https://cf.geekdo-images.com/abc/cover.jpg" };

        var captured = Api.Tests.Observability.MetricCapture.Capture(
            MeepleAiMetrics.EgressBlocked.Name,
            () =>
            {
                try
                {
                    CreateHandler().Handle(banned, CancellationToken.None).GetAwaiter().GetResult();
                    throw new Xunit.Sdk.XunitException("expected the ADR-059 deny-list to reject the host");
                }
                catch (ArgumentException)
                {
                    // atteso
                }
            });

        captured
            .Where(c => Equals(c.Tags.GetValueOrDefault("sink"), "manual")
                && Equals(c.Tags.GetValueOrDefault("reason"), "denylist_hit"))
            .Should().NotBeEmpty("anche il gate difensivo deve essere visibile");
    }
```

Se `StubImageHandler` espone un contatore di richieste, asserisci anche che sia rimasto a zero: il rifiuto deve precedere il fetch. Se non lo espone, non aggiungerlo solo per questo — il `catch (ArgumentException)` prova già che il flusso si è fermato al gate.

Esegui:

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~SetManualCoverCommandHandlerTests"
```

Atteso: PASS.

- [ ] **Step 6: eseguire la suite delle aree toccate**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~EgressMetrics|FullyQualifiedName~EgressHostAllowList|FullyQualifiedName~BggHostDenyList|FullyQualifiedName~WebpVariantGenerator|FullyQualifiedName~SsrfPolicy|FullyQualifiedName~SetManualCover|FullyQualifiedName~EnrichCatalogCover"
```

Atteso: PASS, 0 falliti. Il baseline pre-modifica su questi filtri (senza `SetManualCover`/`EnrichCatalogCover`) era 119 passati / 0 falliti.

- [ ] **Step 7: commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/SetManualCoverCommandValidator.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/SetManualCoverCommandHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/SetManualCoverCommandValidatorTests.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/SetManualCoverCommandHandlerTests.cs
git commit -m "feat(egress): conta gli hit sulla deny-list BGG come denylist_hit (#3583)"
```

---

### Task 4: split dell'alert Prometheus

**Files:**
- Modify: `infra/prometheus/alerts/egress-guard.yml`
- Modify: `infra/prometheus/alerts/egress-guard.test.yml`
- Modify: `infra/prometheus.staging.yml`
- Modify: `infra/prometheus.prod.yml`

**Interfaces:**
- Consumes: la serie `meepleai_egress_blocked_total{sink,reason}` ora popolata anche con `reason="denylist_hit"` (Task 3).
- Produces: due alert distinti, `EgressBlockedManualSensitive` (invariato nel nome, ristretto a `private_ip`) e `EgressDenylistHit` (nuovo, `severity: warning`).

**Perché:** `egress-guard.yml:18` tratta oggi `private_ip|denylist_hit` come un unico incidente P1 con SLO=0. Dal Task 3 in poi il ramo `denylist_hit` diventa raggiungibile: senza questo split, un admin che incolla un URL BGG — errore di policy, non tentativo di raggiungere un target interno — farebbe scattare un P1. Con il carve-out BGG previsto da #3590 il caso diventerà più frequente, non meno.

Attenzione a non fraintendere la portata attuale: quel P1 oggi esiste **solo in dev**, perché `egress-guard.yml` non è referenziato nei `rule_files:` di staging e prod (vedi Step 3b, che lo corregge). Lo split resta comunque necessario proprio perché lo Step 3b accende quelle regole nei due ambienti dove finora tacevano.

- [ ] **Step 1: aggiornare le regole**

In `egress-guard.yml`, sostituisci la riga 18 (`expr:` di `EgressBlockedManualSensitive`) restringendola a `private_ip`:

```yaml
        expr: (sum(rate(meepleai_egress_blocked_total{sink="manual",reason="private_ip"}[5m])) or vector(0)) > 0
```

aggiorna le due annotazioni della stessa regola perché non citino più `denylist_hit`:

```yaml
          summary: "Manual-cover SSRF attempt blocked (sink=manual, private_ip)"
          description: "meepleai_egress_blocked_total incremented for sink=manual with reason private_ip: an admin-supplied manual-cover URL resolved to an internal/reserved address. SLO for this metric is ZERO; any nonzero rate is a P1 SSRF incident (#3495)."
```

e aggiungi in coda al blocco `rules:` la nuova regola:

```yaml
      - alert: EgressDenylistHit
        # #3583 — separata da EgressBlockedManualSensitive: un hit sulla deny-list ADR-059 §5 è una
        # violazione di POLICY (un URL BGG/geekdo nel campo cover manuale), non un tentativo di
        # raggiungere un target interno. Trattarlo come P1 SSRF genererebbe pagine su un errore di
        # battitura di un admin — tanto più col carve-out BGG server-to-server (#3590).
        expr: (sum(rate(meepleai_egress_blocked_total{sink="manual",reason="denylist_hit"}[5m])) or vector(0)) > 0
        for: 0m
        labels:
          severity: warning
          subsystem: egress-policy
        annotations:
          summary: "Manual-cover URL rejected by the ADR-059 BGG deny-list (sink=manual)"
          description: "meepleai_egress_blocked_total incremented for sink=manual with reason denylist_hit: someone submitted a BGG/geekdo asset URL to the manual-cover field, which ADR-059 §5 bans. A one-off is an admin mistake; a sustained rate is an attempt to launder around the ban and warrants review of who is submitting it. The legitimate route for a BGG cover is the admin server-to-server re-upload path, never the arbitrary-URL field."
          runbook_url: "https://github.com/meepleAi-app/meepleai-monorepo/blob/main-dev/docs/for-developers/operations/operations-manual.md"
```

Aggiorna infine il commento di testata (righe 8-10) perché descriva i due segnali separati:

```yaml
# SLO for the manual (arbitrary-URL) sink hitting private_ip is ZERO: any nonzero rate means an
# admin-supplied manual-cover URL resolved to an internal/reserved address — a live SSRF attempt.
# severity=critical pages via the alertmanager critical-alerts route.
#
# denylist_hit is tracked SEPARATELY at severity=warning (#3583): it is an ADR-059 §5 policy
# violation (a BGG/geekdo URL in the manual field), not an internal-target attempt.
```

- [ ] **Step 2: aggiornare i test promtool**

In `egress-guard.test.yml`, aggiorna il commento del caso 3 (riga 31) e aggiungi due casi. Il caso 3 resta valido così com'è (`size_cap` non è né `private_ip` né `denylist_hit`), cambia solo la dicitura:

```yaml
  # 3) DOES NOT FIRE for a non-sensitive reason on the manual sink (size_cap is neither rule's reason).
```

Aggiungi in coda al file:

```yaml
  # 4) denylist_hit FIRES EgressDenylistHit at warning — and NOT the critical SSRF alert (#3583).
  - interval: 1m
    input_series:
      - series: 'meepleai_egress_blocked_total{sink="manual",reason="denylist_hit"}'
        values: '0 0 1 1 1 1'
    alert_rule_test:
      - eval_time: 5m
        alertname: EgressDenylistHit
        exp_alerts:
          - exp_labels:
              severity: warning
              subsystem: egress-policy
      - eval_time: 5m
        alertname: EgressBlockedManualSensitive
        exp_alerts: []

  # 5) private_ip does NOT cross-trigger the policy alert (its own firing is covered by case 1).
  - interval: 1m
    input_series:
      - series: 'meepleai_egress_blocked_total{sink="manual",reason="private_ip"}'
        values: '0 0 1 1 1 1'
    alert_rule_test:
      - eval_time: 5m
        alertname: EgressDenylistHit
        exp_alerts: []
```

- [ ] **Step 3: eseguire promtool per verificare le regole**

Dalla root del worktree:

```bash
docker run --rm -v "$(pwd)/infra/prometheus/alerts:/work" prom/prometheus:v3.7.0 promtool test rules /work/egress-guard.test.yml
```

Atteso: `SUCCESS`.

Questo passo è **bloccante**, non una formalità: nessun workflow CI invoca `promtool` (l'header di `egress-guard.test.yml:1` lo dice esplicitamente, e un grep su `.github/workflows/` e `infra/Makefile` non trova nessuna invocazione). Se le regole sono sbagliate, niente le intercetta a valle. Se Docker non è disponibile in questa sessione, **riporta il fatto** invece di dichiarare il passo completato.

- [ ] **Step 3b: caricare le regole in staging e prod**

`egress-guard.yml` è montato in tutti e tre i compose (`docker-compose.yml:358`, `compose.staging.yml:360`, `compose.prod.yml:245`) ma compare in `rule_files:` **soltanto** in `infra/prometheus.yml` (dev). In staging e prod le regole egress non sono mai state caricate: `EgressBlockedManualSensitive` non esiste lì da #3495 M2, e senza questo passo il nuovo `EgressDenylistHit` nascerebbe morto negli stessi ambienti.

Aggiungi in `infra/prometheus.staging.yml` e `infra/prometheus.prod.yml`, nel blocco `rule_files:`, dopo la riga di `bgg-tos-compliance.yml` (stessa posizione che ha in `infra/prometheus.yml`, per tenere le tre liste allineate):

```yaml
  # #3495 M2 — SSRF egress guard (private_ip = P1) + #3583 policy alert (denylist_hit = warning).
  # Il file era già montato ma mai referenziato in staging/prod: regole caricate solo da #3583.
  - '/etc/prometheus/egress-guard.yml'
```

- [ ] **Step 4: commit**

```bash
git add infra/prometheus/alerts/egress-guard.yml infra/prometheus/alerts/egress-guard.test.yml \
        infra/prometheus.staging.yml infra/prometheus.prod.yml
git commit -m "feat(egress): separa denylist_hit dall'alert P1 SSRF (#3583)"
```

Nota per il corpo della PR: le regole Prometheus **non si ricaricano al deploy**. Dopo il merge serve un force-recreate del container prometheus su staging perché le nuove regole diventino attive; finché non avviene, la config è a posto ma inerte.

---

### Task 5: verifica finale e PR

**Files:** nessuna modifica di codice.

- [ ] **Step 1: build pulita**

Da `apps/api/src/Api`:

```bash
dotnet build --nologo -v q
```

Atteso: `Avvisi: 0`, `Errori: 0`.

- [ ] **Step 2: suite delle aree toccate**

Da `apps/api`:

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~EgressMetrics|FullyQualifiedName~EgressHostAllowList|FullyQualifiedName~BggHostDenyList|FullyQualifiedName~WebpVariantGenerator|FullyQualifiedName~SsrfPolicy|FullyQualifiedName~SetManualCover|FullyQualifiedName~EnrichCatalogCover"
```

Atteso: 0 falliti. Riporta il conteggio esatto passati/falliti — non "sembra a posto".

- [ ] **Step 3: formattazione backend**

Il pre-commit salta il format backend sulle branch `feature/*`, quindi va lanciato a mano prima della PR. **Dalla root del worktree** (non da `apps/api`: `git diff --name-only` restituisce path relativi alla root, che con un'altra cwd non matcherebbero nulla e produrrebbero un `--include` vuoto):

```bash
dotnet format apps/api/MeepleAI.Api.sln --include $(git diff --name-only origin/main-dev...HEAD -- '*.cs' | tr '\n' ' ')
```

**`--include` è obbligatorio**: senza, `dotnet format` applica i fix degli analyzer a tutto il progetto e ha già cancellato costruttori DI usati solo via reflection (S1144), lasciando 3 test rossi per mesi.

Se il comando non modifica nulla, `git status` resta pulito: è l'esito atteso, non un errore.

- [ ] **Step 4: push e apertura PR verso main-dev**

```bash
git push -u origin feature/issue-3583-egress-observability-reasons
gh pr create --base main-dev \
  --title "feat(egress): wire delle reason di osservabilità mancanti (#3583)" \
  --body "<vedi contenuti obbligatori sotto>"
```

Il target **deve** essere `main-dev`, mai `main`.

Il corpo della PR deve contenere, oltre a cosa cambia e all'esito di test e promtool:

1. **Le tre deviazioni dal testo dell'issue** e il perché: `decode_fail` sui call site invece che nel generator; split dell'alert `denylist_hit`; nessun allentamento della deny-list.
2. **Il denominatore del block-ratio si sporca.** `RecordEgressAllowed` è emesso solo dal connect-pin, una volta per connessione TCP validata. Ora `denylist_hit` dal validator incrementa `blocked` **senza** che sia mai avvenuto un tentativo di connessione (nessun `allowed` corrispondente), mentre `decode_fail` incrementa `blocked` **dopo** che la connessione è stata concessa (quindi con un `allowed` già contato per la stessa operazione). Qualunque alert futuro su `blocked/(blocked+allowed)` — il motivo per cui #3495 M2 ha creato `EgressAllowed` — va progettato sapendolo.
3. **La semantica del counter cambia**: da "connessione rifiutata" a "connessione o payload rifiutati". Le dashboard esistenti su `meepleai_egress_blocked_total` vanno rilette in questa luce.
4. **`egress-guard.yml` non era caricato in staging né in prod** (bug pre-esistente di #3495 M2, corretto qui): prima di questa PR quegli alert non esistevano fuori dal dev.
5. **Serve un force-recreate di prometheus su staging** dopo il merge perché le regole diventino attive — il deploy da solo non le ricarica.

---

## Self-Review

**Copertura della spec (sezione PR 1):**
- §1.1 `dns_failure` → Task 1. ✔
- §1.2 `decode_fail` sui due call site di rete, con la motivazione della deviazione → Task 2. ✔
- §1.3 `denylist_hit` sui due gate + split dell'alert → Task 3 (wiring) e Task 4 (alert). ✔
- §"Test PR 1": unit DNS + cancellazione → Task 1 Step 4; decode_fail nei due sink → Task 2; denylist_hit → Task 3; estensione di `Counters_HaveStableBoundedNames` → Task 1 Step 4; promtool → Task 4 Step 3. ✔

**Scan dei placeholder:** nessuno residuo. Il Task 2 Step 5, che nella prima stesura rimandava genericamente al test sibling, è ora letterale: l'arrangiamento reale (`BuildHarness`, `BuildGame`, i due stub handler, i byte corrotti) è trascritto, ed è chiarito che l'handler **cattura** `ImageProcessingException` e ritorna `Failed` invece di rilanciare — quindi lì non serve `try/catch`, a differenza del path manuale.

**Coerenza dei tipi:** `MetricCapture.Capture(string, Action) -> List<(long Value, IReadOnlyDictionary<string, object?> Tags)>` è definita nel Task 1 Step 1 e usata con la stessa firma nei Task 2 e 3. Le costanti citate esistono tutte: `DecodeFail` e `DenylistHit` già in `MeepleAiMetrics.Egress.cs:31,40`; `DnsFailure` la crea il Task 1 Step 6. `ImageProcessingException` e `BggHostDenyList.IsBanned` sono verificati nel sorgente. Il costruttore posizionale `SetManualCoverCommand(GameId, SourceUrl, License, Attribution, AdminId)` è verificato in `SetManualCoverCommandValidatorTests.cs:22-23`.

**Dipendenza d'ordine:** il Task 3 Step 5 assume che il Task 2 abbia già aggiunto `using Api.Observability;` a `SetManualCoverCommandHandler.cs`. Eseguendo i task fuori ordine non compila — vanno eseguiti in sequenza.

## Trovato durante la review — fuori scope di questa PR

`infra/prometheus/alerts/api-single-instance.yml` (il tripwire `count(up{job="meepleai-api"}) > 1` di #3383 / ADR-087 D4) **non è montato in nessun compose e non compare in nessun `rule_files:`**, dev incluso. Il file esiste e ha il suo test promtool, ma Prometheus non lo carica in alcun ambiente: l'alert che secondo ADR-087 D4 «rende un scale-out rumoroso» — e che è la ragione dichiarata per cui la hard-prevention è stata rinviata — non è mai stato attivo.

Non lo correggo qui perché appartiene a #3383 (PR 2), dove va aggiunto sia il mount nei tre compose sia la riga nei tre `rule_files:`. Va anche riaperta la prima checkbox "Subito" di #3383, oggi spuntata.
