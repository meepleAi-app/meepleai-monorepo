# Design — osservabilità egress, enforcement single-pod, cover-gap catalogo

**Data**: 2026-08-06
**Issue**: [#3583](https://github.com/meepleAi-app/meepleai-monorepo/issues/3583), [#3383](https://github.com/meepleAi-app/meepleai-monorepo/issues/3383), [#3590](https://github.com/meepleAi-app/meepleai-monorepo/issues/3590)
**Base**: `main-dev` @ `d3af99912`

Record di design per un lotto di tre issue indipendenti, consegnate in tre PR separate verso
`main-dev`. Il documento è committato con la prima PR perché le tre si toccano in un punto: lo
split dell'alert `denylist_hit` (#3583) è ciò che rende sostenibile il carve-out BGG (#3590).

---

## PR 1 — #3583: reason di osservabilità egress non wired

Tre segnali mancanti su percorsi che **già falliscono chiusi**. Nessuno è un buco di sicurezza:
l'esito è corretto, manca il contatore che lo rende visibile.

### 1.1 `dns_failure`

`SsrfPinnedConnect.ResolveAndValidateAsync` (`SsrfPinnedConnect.cs:30`) conta il blocco quando la
risoluzione ritorna zero indirizzi o un indirizzo bloccato, ma se `IDnsResolver.ResolveAsync`
**lancia** (NXDOMAIN, timeout del resolver, socket error) l'eccezione propaga senza passare da
`RecordEgressBlocked`. Un sink che degrada per problemi DNS diventa indistinguibile da un sink
inattivo.

- Nuova costante `EgressBlockReasons.DnsFailure = "dns_failure"` in `MeepleAiMetrics.Egress.cs`.
- `try/catch` attorno alla risoluzione: registra e **rilancia** (l'esito fail-closed non cambia).
- L'exception filter esclude la cancellazione del chiamante:
  `catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)`.
  Un timeout interno del resolver viene contato; un abort del chiamante no.
- Nessun host/IP nei tag: restano i due soli tag bounded `sink` e `reason`.

### 1.2 `decode_fail` — deviazione motivata dal testo dell'issue

L'issue propone di wirarlo dentro `WebpVariantGenerator`. **Non lo faccio**, per due ragioni
verificate nel codice:

1. Il generator non conosce il `sink` e non potrebbe taggare la metrica senza cambiarne
   l'interfaccia (`IWebpVariantGenerator`). Il sink è una proprietà del chiamante, non del codec.
2. Uno dei tre chiamanti — `MaterializePdfCoverCommandHandler.cs:77` — lavora su un PDF **locale**:
   lì un decode fallito non è un blocco di egress e conteggiarlo falserebbe il rapporto
   blocked/allowed.

Wiro invece i due call site alimentati da rete:

| Call site | Sink |
|---|---|
| `SetManualCoverCommandHandler.cs:85` | `EgressSinks.Manual` |
| `EnrichCatalogCoverCommandHandler.cs:221` (ha già il `catch (ImageProcessingException)`) | `EgressSinks.Wikimedia` |

Copre entrambi i rifiuti di `WebpVariantGenerator` citati dall'issue — decompression bomb
(cap dimensione/megapixel, `WebpVariantGenerator.cs:119-128`) e coder non-raster
(`DetectRasterFormat` → `null`, `WebpVariantGenerator.cs:100-108`) — perché entrambi risalgono come
`ImageProcessingException`.

### 1.3 `denylist_hit` + split dell'alert

`BggHostDenyList` ha due call site, mutuamente esclusivi (se il validator rifiuta, l'handler non
gira): `SetManualCoverCommandValidator.cs:27` e `SetManualCoverCommandHandler.cs:73`
(defense-in-depth). Wiro entrambi con `sink=manual`; l'esclusività garantisce che una richiesta
conti una volta sola.

**Il punto non ovvio**: `infra/prometheus/alerts/egress-guard.yml:18` tratta già
`sink="manual", reason=~"private_ip|denylist_hit"` come **incidente P1 SSRF con SLO=0**. Wirare il
counter così com'è farebbe scattare un P1 ogni volta che un admin incolla un URL BGG — che è una
violazione della policy ADR-059 §5, non un tentativo di raggiungere un target interno. Con il
carve-out BGG di PR 3 gli admin avranno più occasioni di provarci, quindi il falso P1 diventerebbe
ricorrente.

Separo quindi i due segnali:

- `private_ip` → resta P1 SSRF, SLO=0, invariato.
- `denylist_hit` → alert distinto `severity: warning`, descritto come violazione della policy
  ADR-059 (tentativo di laundering attorno al ban BGG), con runbook che punta al carve-out.

`infra/prometheus/alerts/egress-guard.test.yml` va aggiornato in pari passo (i test promtool sono
parte del gate).

### Test PR 1

- Unit su `ResolveAndValidateAsync`: eccezione DNS → `dns_failure` incrementato + rilancio;
  cancellazione del chiamante → **nessun** incremento.
- Unit sui due call site `decode_fail` con il sink atteso.
- Unit su validator e handler per `denylist_hit`.
- Estensione di `Counters_HaveStableBoundedNames` (`EgressMetricsTests.cs:120`) alle nuove reason.
- `promtool test rules` sui file alert modificati.

---

## PR 2 — #3383: enforcement single-pod (D4 di #3373)

I tre punti "Subito" dell'issue sono già a terra e verificati: tripwire
`infra/prometheus/alerts/api-single-instance.yml` (`count(up{job="meepleai-api"}) > 1`, `for: 5m`),
commento sul vincolo in `infra/docker-compose*.yml:60`, emendamento DEC-3e in
`adr-087-cover-procedure-design-decisions.md:38-44`. Restano i due task deferiti.

### 2.1 Gauge dead-letter da `COUNT` DB

Oggi il gauge è ibrido: `WikidataCoverEnrichmentRunner.cs:121` incrementa in memoria a ogni nuovo
dead-letter, e `WikidataCoverDeadLetterRetentionJob.cs:92-93` ri-ancora al `COUNT` reale dopo lo
sweep — che gira **una volta al giorno alle 03:00 UTC**. A >1 pod ogni processo ha il suo contatore:
`sum()` raddoppia, `max()` deriva.

- Nuovo `WikidataDeadLetterMetricsRefreshService : BackgroundService`, sul pattern già in uso di
  `ImpersonationMetricsRefreshService.cs`: ogni 60s apre uno scope, chiama `CountDeadLettersAsync`
  e pusha via `SetWikidataDeadLetterCount`.
- L'`ObservableGauge` continua a leggere il campo in memoria: è il servizio che lo aggiorna. Questo
  evita l'anti-pattern segnalato dall'issue (query al DB a ogni scrape Prometheus).
- **Rimuovo** `IncrementWikidataDeadLetterCount` dal runner: con il refresh periodico l'incremento
  ottimistico diventa solo una fonte di drift, e un valore puramente DB-derivato è esattamente ciò
  che rende `max()` corretto a più pod (ogni pod riporta lo stesso ground truth).
- Resilienza come nel precedente: un refresh fallito logga e ritenta al tick successivo, non
  abbatte l'host.

### 2.2 Lease Redis fail-closed sul batch

`WikidataCoverEnrichmentJob` ha oggi solo `[DisallowConcurrentExecution]`, che è una garanzia
**intra-processo**: non impedisce a due pod di girare lo stesso batch e raddoppiare il rate verso
Wikimedia (violazione ToS, DEC-3e).

- Lease su Redis via `IConnectionMultiplexer` (già disponibile nel progetto), `SET NX PX` con TTL
  maggiore della durata attesa del tick, rilascio in `finally`.
- **Fail-closed**: se il lease non si acquisisce *o Redis non è raggiungibile*, il tick viene
  saltato e loggato.

Trade-off da mettere per iscritto, perché non è gratis: fail-closed significa che
**un'indisponibilità di Redis ferma l'enrichment**. È la semantica di hard-prevention richiesta
dall'issue — si preferisce non arricchire piuttosto che rischiare di violare il rate cap — ma va
documentata nell'emendamento ADR-087 e nel runbook, altrimenti a un'incidente Redis nessuno collega
l'enrichment fermo.

### Test PR 2

- Unit sul refresh service: `RefreshOnceAsync` pusha il valore contato; un'eccezione del repo non
  propaga fuori dal loop.
- Unit sul job: lease acquisito → il batch gira; lease non acquisito → batch saltato; Redis in
  errore → batch saltato (fail-closed), non un'eccezione che risale a Quartz.

---

## PR 3 — #3590: cover-gap catalogo + carve-out BGG

### Premessa: il testo dell'issue è in parte superato

Due assunzioni sono cambiate e vanno corrette nell'issue:

1. **«la via più economica resta sbloccare #3589 e ri-processarli»** — #3589 è chiusa ma ha corretto
   solo la *classificazione* dell'errore 413 (permanente anziché retriable) e il messaggio
   fuorviante. L'issue stessa classifica l'innalzamento del limite come «una scelta di capacità, non
   un bug». Però il default nel codice è **già 100MB** da `0afeb58b8` (#3424, 2026-07-31)
   — `apps/unstructured-service/src/config/settings.py:17` — mentre staging il 2026-08-06 riportava
   ancora `exceeds maximum 52428800 bytes` (50MB). `MAX_FILE_SIZE` non è impostato in `infra/`.
   Ipotesi da verificare: immagine staging stale. Se confermata, i 4 PDF si sbloccano con un
   redeploy, senza codice nuovo.
2. **La via BGG è sbarrata, non aperta** — `BggHostDenyList` blocca `geekdo-images.com`,
   `geekdo.com`, `boardgamegeek.com` sul path manuale in due punti. L'opzione 1 dell'issue non può
   passare per `SetManualCoverCommand`.

### 3.1 Vista admin cover-gap

Il collo di bottiglia reale non è la mancanza di uno strumento per risolvere, ma la mancanza di un
modo per **trovare** i giochi da risolvere: non esiste alcuna vista admin dei giochi senza cover, e
l'unico accesso all'editor è l'affordance a matita in hover sulle card della pagina *pubblica*
`/shared-games`.

- Query CQRS che incrocia `shared_games.{pdf,bgg,wikidata,manual}_cover_r2_key IS NULL` con
  `pdf_documents.{cover_generation_status, error_category}` per derivare la causa, nei tre gruppi
  dell'issue: PDF oltre il limite di dimensione · pagina rifiutata dall'euristica · nessuna sorgente.
- Endpoint admin + pagina sotto `apps/web/src/app/admin/(dashboard)/shared-games/`, con accesso
  diretto all'editor cover esistente.
- **Gotcha da rispettare**: il gruppo "euristica" si riconosce da `cover_generation_status =
  'Skipped'` scritto **direttamente sul campo** da `BackfillPdfCoversJob`, non via
  `PdfDocument.MarkCoverSkipped()` (metodo morto).

Il path manuale a valle funziona già end-to-end: il form "Aggiungi copertina da URL" di
`AdminCoverSourceDialog.tsx` sta fuori dal ramo `candidates.length === 0`, quindi resta usabile
anche sui giochi con zero candidati — cioè esattamente i 26.

### 3.2 Carve-out BGG re-upload

`BggCoverDownloader` + `BggCoverUploadPipeline` esistono già, sono sanzionati da ADR-059 §2 e sono
già in uso da `CreateSharedGameFromPdfCommandHandler.cs:123` — ma solo alla **creazione** di uno
SharedGame da PDF. Non esiste un trigger per un gioco già a catalogo.

Comando/endpoint admin dedicato che riusa quel path, **senza** passare da `SetManualCoverCommand`.

La distinzione è il punto centrale del design: `BggHostDenyList` resta **intatta** sul path a URL
arbitrario, che è esattamente il suo scopo (impedire a un admin di aggirare il ban incollando un
URL geekdo nel campo libero). Non è una nuova postura legale né un allentamento: è un trigger nuovo
su un path server-to-server già esistente e già sanzionato. Il freeze #2123 riguarda le richieste
*browser* verso gli host geekdo e non è toccato.

Da aggiornare: `docs/for-developers/specs/2026-08-02-admin-cover-editor-design.md:88` afferma «host
BGG non bloccati nel fetch server-side (ADR-059 §2)» — affermazione superata da #3495, il doc è
obsoleto su quel punto.

### Test PR 3

- Unit sulla query cover-gap: classificazione corretta nei tre gruppi, inclusa la riga `Skipped`.
- Unit sul comando BGG re-upload: gioco senza `bggId` → errore di dominio; percorso felice →
  chiave R2 attesa; la deny-list resta attiva su `SetManualCover` (test di non-regressione).
- Test FE sulla pagina cover-gap.

### Fuori PR — verifica ops

SSH allo staging per leggere il limite effettivo di `unstructured-service`; se stale, redeploy e
retry sui 4 PDF grandi. Esito riportato su #3590.

---

## Note trasversali

- **Branch**: uno per issue, ciascuno da `main-dev` aggiornato, PR sequenziali (1 issue = 1 PR).
- **Worktree**: `.claude/worktrees/i3583`, da eliminare a merge avvenuto in `main-dev`.
- **Baseline verificata** su `d3af99912`: build API pulita (0 warning, 0 errori); 119 test passati,
  0 falliti sui filtri `EgressMetrics|EgressHostAllowList|BggHostDenyList|WebpVariantGenerator|SsrfPolicy`.
- **Decisioni di #3495 da non riaprire**: facade `IHardenedEgressClient` declinata (E3); four-eyes
  su `source=Manual` deferita (M7); decode-and-recurse dell'IPv4 embedded è una deviazione
  deliberata; gli assert H5 sugli invarianti R2 restano gated su #3498.
