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

In `apps/api/tests/Api.Tests/Observability/EgressMetricsTests.cs`, elimina il metodo privato `Capture` (righe 28-56) e sostituisci le tre chiamate `Capture(` con `MetricCapture.Capture(`. Nessun altro cambiamento: è un refactor meccanico che i test esistenti devono confermare verde.

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

Estendi anche l'asserzione sui nomi stabili, in `Counters_HaveStableBoundedNames`:

```csharp
        MeepleAiMetrics.EgressBlockReasons.DnsFailure.Should().Be("dns_failure");
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
            // Il filtro esclude la cancellazione iniziata dal chiamante — che non è un guasto di
            // egress — pur continuando a contare un timeout interno del resolver.
            _ = ex;
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

Atteso: `Avvisi: 0`, `Errori: 0`. Se compare CA1031 (cattura di `Exception` generica), **non** aggiungere una soppressione: il `when` filter + `throw;` non inghiottono nulla, ma se l'analyzer protesta comunque restringi la cattura a `catch (Exception ex) when (...)` → già così; in tal caso riporta il warning esatto invece di sopprimerlo alla cieca.

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

**Perché qui e non in `WebpVariantGenerator`:** il generator non conosce il `sink` e non potrebbe taggare la metrica senza cambiare `IWebpVariantGenerator`; inoltre il suo terzo chiamante, `MaterializePdfCoverCommandHandler.cs:77`, decodifica un PDF **locale**, dove un decode fallito non è un blocco di egress e falserebbe il rapporto blocked/allowed. Entrambi i rifiuti citati da #3583 — decompression bomb (`WebpVariantGenerator.cs:119-128`) e coder non-raster (`WebpVariantGenerator.cs:100-108`) — risalgono comunque come `ImageProcessingException`, quindi wirare il catch al call site li copre entrambi.

- [ ] **Step 1: scrivere il test che fallisce sul path manuale**

In `SetManualCoverCommandHandlerTests.cs` aggiungi in coda alla classe. Il fixture monta il **vero** `WebpVariantGenerator`, quindi basta far restituire allo stub HTTP un payload non-raster perché il decoder lo rifiuti:

```csharp
    [Fact]
    public async Task Handle_NonRasterPayload_RecordsDecodeFail_OnManualSink()
    {
        var game = NewGame();
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        // SVG: coder non-raster, rifiutato da DetectRasterFormat prima di raggiungere Magick (#3495 M1).
        _httpHandler.ImageBytes = System.Text.Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"10\" height=\"10\"/></svg>");

        var captured = Api.Tests.Observability.MetricCapture.Capture(
            MeepleAiMetrics.EgressBlocked.Name,
            () =>
            {
                Func<Task> act = () => CreateHandler().Handle(Command(game.Id), CancellationToken.None);
                act.Should().ThrowAsync<ImageProcessingException>().GetAwaiter().GetResult();
            });

        captured
            .Where(c => Equals(c.Tags.GetValueOrDefault("sink"), "manual")
                && Equals(c.Tags.GetValueOrDefault("reason"), "decode_fail"))
            .Should().NotBeEmpty("il rifiuto del decoder su un payload scaricato è un blocco di egress");
    }
```

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

In `EnrichCatalogCoverCommandHandlerTests.cs`, il test esistente `Handle_ImageProcessingError_ReturnsFailedImageProcessingError` (riga 372) copre già l'esito. Aggiungi un fatto sibling che asserisce la metrica, replicando lo stesso arrangiamento di quel test — leggi le righe 372-395 e riusa il medesimo setup di mock, cambiando solo l'asserzione finale:

```csharp
    [Fact]
    public async Task Handle_ImageProcessingError_RecordsDecodeFail_OnWikimediaSink()
    {
        // Stesso arrangiamento di Handle_ImageProcessingError_ReturnsFailedImageProcessingError:
        // il generator WebP mockato lancia ImageProcessingException sul payload scaricato da Commons.
        var captured = Api.Tests.Observability.MetricCapture.Capture(
            MeepleAiMetrics.EgressBlocked.Name,
            () => { /* invocazione dell'handler, come nel test sibling */ });

        captured
            .Where(c => Equals(c.Tags.GetValueOrDefault("sink"), "wikimedia")
                && Equals(c.Tags.GetValueOrDefault("reason"), "decode_fail"))
            .Should().NotBeEmpty("il rifiuto del decoder su un'immagine Commons è un blocco di egress");
    }
```

Nota per chi implementa: `MetricCapture.Capture` accetta un `Action` sincrona; l'handler è async. Nel corpo della lambda usa lo stesso pattern del Task 1 — `handler.Handle(...).GetAwaiter().GetResult()` racchiuso in un `try/catch` dell'eccezione attesa, oppure `.Wait()` se il test sibling non si aspetta un throw ma un `EnrichCatalogCoverResult.Failed` (in quel caso non serve il try/catch: l'handler cattura internamente).

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

**Interfaces:**
- Consumes: `MetricCapture.Capture` (Task 1); `BggHostDenyList.IsBanned(string? url) -> bool`; `MeepleAiMetrics.EgressBlockReasons.DenylistHit` (già esistente, valore `"denylist_hit"`).
- Produces: nulla di consumato da task successivi.

**Perché entrambi i gate:** sono mutuamente esclusivi — se il validator rifiuta, l'handler non gira — quindi una richiesta conta esattamente una volta. Il gate del validator cattura il caso reale (admin che incolla un URL BGG); quello dell'handler cattura la difesa in profondità (un percorso che raggiunge l'handler saltando la pipeline FluentValidation). Wirarne uno solo lascerebbe cieco l'altro.

- [ ] **Step 1: scrivere il test che fallisce sul validator**

In `SetManualCoverCommandValidatorTests.cs` aggiungi in coda alla classe. Adatta la costruzione del comando al costruttore già usato nel file (`new SetManualCoverCommand(gameId, sourceUrl, license, attribution, adminId)`):

```csharp
    [Fact]
    public void Validate_BggHost_RecordsDenylistHit_OnManualSink()
    {
        var command = new SetManualCoverCommand(
            Guid.NewGuid(),
            "https://cf.geekdo-images.com/abc/original/cover.jpg",
            "CC BY-SA 4.0",
            "Jane Doe",
            Guid.NewGuid());

        var captured = Api.Tests.Observability.MetricCapture.Capture(
            MeepleAiMetrics.EgressBlocked.Name,
            () =>
            {
                var result = new SetManualCoverCommandValidator().Validate(command);
                result.IsValid.Should().BeFalse("la deny-list ADR-059 §5 deve rifiutare un host BGG");
            });

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

- [ ] **Step 6: eseguire la suite delle aree toccate**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~EgressMetrics|FullyQualifiedName~EgressHostAllowList|FullyQualifiedName~BggHostDenyList|FullyQualifiedName~WebpVariantGenerator|FullyQualifiedName~SsrfPolicy|FullyQualifiedName~SetManualCover|FullyQualifiedName~EnrichCatalogCover"
```

Atteso: PASS, 0 falliti. Il baseline pre-modifica su questi filtri (senza `SetManualCover`/`EnrichCatalogCover`) era 119 passati / 0 falliti.

- [ ] **Step 7: commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/SetManualCoverCommandValidator.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/SetManualCoverCommandHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/SetManualCoverCommandValidatorTests.cs
git commit -m "feat(egress): conta gli hit sulla deny-list BGG come denylist_hit (#3583)"
```

---

### Task 4: split dell'alert Prometheus

**Files:**
- Modify: `infra/prometheus/alerts/egress-guard.yml`
- Modify: `infra/prometheus/alerts/egress-guard.test.yml`

**Interfaces:**
- Consumes: la serie `meepleai_egress_blocked_total{sink,reason}` ora popolata anche con `reason="denylist_hit"` (Task 3).
- Produces: due alert distinti, `EgressBlockedManualSensitive` (invariato nel nome, ristretto a `private_ip`) e `EgressDenylistHit` (nuovo, `severity: warning`).

**Perché:** `egress-guard.yml:18` tratta oggi `private_ip|denylist_hit` come un unico incidente P1 con SLO=0. Dal Task 3 in poi il ramo `denylist_hit` diventa raggiungibile: senza questo split, un admin che incolla un URL BGG — errore di policy, non tentativo di raggiungere un target interno — farebbe scattare un P1. Con il carve-out BGG previsto da #3590 il caso diventerà più frequente, non meno.

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

  # 5) private_ip FIRES the critical alert and NOT the policy one — the two must not cross-trigger.
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

Atteso: `SUCCESS`. Se Docker non è disponibile in questa sessione, riporta il fatto invece di dichiarare il passo completato: la validazione promtool è l'unico gate su questi file e non va saltata in silenzio.

- [ ] **Step 4: commit**

```bash
git add infra/prometheus/alerts/egress-guard.yml infra/prometheus/alerts/egress-guard.test.yml
git commit -m "feat(egress): separa denylist_hit dall'alert P1 SSRF (#3583)"
```

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

Il pre-commit salta il format backend sulle branch `feature/*`, quindi va lanciato a mano prima della PR. Da `apps/api`:

```bash
dotnet format --include $(git diff --name-only origin/main-dev...HEAD -- '*.cs' | tr '\n' ' ')
```

**`--include` è obbligatorio**: senza, `dotnet format` applica i fix degli analyzer a tutto il progetto e ha già cancellato costruttori DI usati solo via reflection (S1144), lasciando 3 test rossi per mesi.

- [ ] **Step 4: push e apertura PR verso main-dev**

```bash
git push -u origin feature/issue-3583-egress-observability-reasons
gh pr create --base main-dev \
  --title "feat(egress): wire delle reason di osservabilità mancanti (#3583)" \
  --body "<corpo: cosa cambia, le tre deviazioni dal testo dell'issue e il perché, esito dei test e di promtool>"
```

Il target **deve** essere `main-dev`, mai `main`.

---

## Self-Review

**Copertura della spec (sezione PR 1):**
- §1.1 `dns_failure` → Task 1. ✔
- §1.2 `decode_fail` sui due call site di rete, con la motivazione della deviazione → Task 2. ✔
- §1.3 `denylist_hit` sui due gate + split dell'alert → Task 3 (wiring) e Task 4 (alert). ✔
- §"Test PR 1": unit DNS + cancellazione → Task 1 Step 4; decode_fail nei due sink → Task 2; denylist_hit → Task 3; estensione di `Counters_HaveStableBoundedNames` → Task 1 Step 4; promtool → Task 4 Step 3. ✔

**Scan dei placeholder:** un punto resta deliberatamente non letterale — Task 2 Step 5, dove il corpo della lambda rimanda all'arrangiamento del test sibling alle righe 372-395 di `EnrichCatalogCoverCommandHandlerTests.cs`. Non ho letto quel blocco per intero, quindi trascriverlo a memoria avrebbe prodotto codice plausibile ma non verificato: peggio di un rimando esplicito. Chi implementa legge quelle righe e riusa il setup reale.

**Coerenza dei tipi:** `MetricCapture.Capture(string, Action) -> List<(long Value, IReadOnlyDictionary<string, object?> Tags)>` è definita nel Task 1 Step 1 e usata con la stessa firma nei Task 2 e 3. Le costanti citate esistono tutte: `DecodeFail` e `DenylistHit` già in `MeepleAiMetrics.Egress.cs:31,40`; `DnsFailure` la crea il Task 1 Step 6. `ImageProcessingException` e `BggHostDenyList.IsBanned` sono verificati nel sorgente.
