# Game Cover Configuration — Design Spec

**Data**: 2026-07-14
**Stato**: Design approvato (brainstorming) — pronto per implementation plan
**Branch**: `feature/game-cover-configuration`
**Bounded context toccati**: `DocumentProcessing`, `SharedGameCatalog`, `UserLibrary`, `GameManagement`

---

## 1. Contesto e problema

Un gioco (di catalogo o privato) deve poter avere una **cover** configurabile da due attori:

1. **Admin** — gestisce le cover degli shared game del catalogo comune.
2. **Utente** — quando crea un gioco o aggiunge un PDF a un gioco, può impostarne la cover, opzionalmente usando **una pagina del PDF** caricato. In aggiunta, sia utente sia admin possono **recuperare dati (cover + metadati) da Wikidata**.

La ricognizione del codebase (2026-07-14) ha rivelato che **gran parte delle primitive esiste già**, ma in modo asimmetrico (spesso solo lato admin) e non "cablato" nel flusso utente. Questo spec definisce il wiring mancante e i consolidamenti necessari, entro i vincoli di compliance del **freeze BGG (#2123 / ADR-059 §5)**.

### Obiettivo

Rendere disponibile la configurazione cover a utente e admin, con tre sorgenti (pagina-PDF, Wikidata, placeholder di default), governance appropriata per il bene condiviso, deduplicazione PDF coerente, e piena aderenza ai vincoli di licenza/attribuzione/compliance.

---

## 2. Decisioni chiave (decision log)

| ID | Decisione | Motivazione |
|----|-----------|-------------|
| **DEC-1** | La cover che un utente sceglie su uno **SharedGame** è **condivisa (L4)**, visibile a tutti. | Scelta prodotto. Implica governance di scrittura (DEC-5). |
| **DEC-2** | Il flusso copre **entrambe** le entità: `PrivateGame` (cover privata) e `SharedGame` (cover condivisa). | Scelta prodotto. Due percorsi/command distinti. |
| **DEC-3** | Sorgenti cover utente: **pagina-PDF + Wikidata + placeholder** (default). Nessun upload di immagine arbitraria. | YAGNI + compliance: la provenienza è un rulebook caricato dall'utente o una fonte CC0/whitelist. Chiude il canale URL esterno. |
| **DEC-4** | **Un** componente `CoverPagePicker` riusato, esposto da **due ingressi**: fine upload PDF + azione "Imposta cover" sul gioco. | Aderente al requisito ("crea gioco / aggiunge PDF"), DRY. |
| **DEC-5** | Governance cover **pagina-PDF su SharedGame (L4)** = **proposal + approvazione admin**, estendendo `ShareRequest` con `ContributionType.CoverChange`. | La pagina-PDF è contenuto **arbitrario** → rischio vandalismo sul catalogo pubblico. Riusa l'infrastruttura di moderazione esistente. |
| **DEC-6** | La cover-da-pagina è **materializzata come copia WebP indipendente in R2** (sync); il PDF sorgente è **reference-counted** (eliminabile solo all'ultimo `EntityLink`). | Evita il re-render on-the-fly; la cover non si rompe mai. |
| **DEC-7** | Dedup PDF via `ContentHash` (SHA-256): **riuso trasparente** via `EntityLink`, scope **globale sul catalogo** / **per-utente sui privati**. Allineare il path chunked al riuso. | "Non vogliamo doppioni"; rispetta il confine `SharedGameId`/`PrivateGameId`. |
| **DEC-8** | Wikidata: scope **cover + metadati**. Cover L2 utente **diretta** (deterministica + licenza-validata). Metadati **fill-gaps only** (non sovrascrivono BGG/manuali). | Il recupero Wikidata è deterministico (P18 del QID), non arbitrario → nessuna moderazione. Fill-gaps è additive, zero perdita dati. |
| **DEC-9** | Materializzazione cover **sincrona** (spinner ~1-2s), non async. | È una singola pagina; l'anteprima è già stata renderizzata prima della conferma. |

---

## 3. Stato attuale del codebase (evidence)

### 3.1 Sistema cover a livelli

`CoverUrlResolver` (`SharedGameCatalog/Application/Services/CoverUrlResolver.cs:104`) risolve server-side in ordine di priorità e restituisce una presigned R2 URL nel campo `SharedGameDto.coverUrl`:

| Livello | Sorgente | Campo | Stato |
|---------|----------|-------|-------|
| L3 | Cover custom utente | `UserLibraryEntry.CustomCoverR2Key` | ⏳ non implementato (#1824) |
| L4 | Cover da pagina PDF | `SharedGame.PdfCoverR2Key` / `PdfDocument.CoverR2Key`+`CoverPageIndex` | ⚠️ parziale (`MarkCoverGenerated()` esiste, #1852) |
| L2.5 | BGG re-upload server-side | `SharedGame.BggCoverR2Key` | ✅ (admin/pipeline) |
| L2 | Wikidata | `SharedGame.WikidataCoverR2Key` (+`WikidataCoverLicense/Attribution/SourceUrl`) | ✅ (job M8) |
| — | Placeholder deterministico | — (`cover-utils.ts`) | ✅ |

Entity: `SharedGameEntity.cs` (`ImageUrl:28`, `ThumbnailUrl:29` — legacy nullable; `WikidataCoverR2Key:68`, `PdfCoverR2Key:78`, `BggCoverR2Key:112`; license/attribution `81-87`). Domain: `SharedGame.cs` (`SetPdfCoverR2Key:616`, `AssignWikidataQid:651`, `SetWikidataCover:703`, `EnrichFromBgg:544`).

Frontend fallback: `cover-utils.ts` (`shouldUsePlaceholder:93`, `isBlockedImageHost:77`, `BLOCKED_IMAGE_HOSTS:26-33`, `hashToHue:133`, `extractInitials:208`).

### 3.2 Primitive PDF → immagine (già disponibili)

- `GetPdfPageImageQuery(PdfDocumentId, PageNumber)` → handler chiama SmolDocling `POST /api/v1/page-image` (`smoldocling-service/src/main.py:315`, `pdf2image`+ghostscript, DPI 150, JPEG q85).
- Endpoint `GET /ingest/pdf/{pdfId}/page-image?page=N` → JPEG (`PdfUploadEndpoints.cs:125`, autenticato).
- FE: `CoverImagePicker.tsx` (admin import wizard) — 3 tab (Placeholder / Dal PDF / Upload custom). `PdfViewerModal.tsx` (react-pdf).
- `PdfDocument.cs`: `PageCount:27`, `CoverR2Key:93`, `CoverPageIndex:95`, `CoverGenerationStatus:94`, `MarkCoverGenerated:843-855`, `CreateCopy:941` (#2732, copia su approvazione share; call-site `ShareRequestDocumentService.cs:201`).
- State machine 7 stati: Pending → Uploading → Extracting → Chunking → Embedding → Indexing → Ready (+ Failed).

### 3.3 Deduplicazione PDF (esiste, ma incoerente)

- `PdfDocument.ContentHash` (`:60-61`, SHA-256). Repository: `FindByContentHashAsync:152`, `ExistsByContentHashAsync(hash, gameId, privateGameId):163-166`.
- `AddRulebookCommandHandler.cs:93-158` — **riuso trasparente**: hash → lookup globale → se `Ready`/in-corso, riusa via `EntityLink` (nessun ri-upload); se `Failed`, tratta come nuovo. Messaggio *"Regolamento già disponibile — collegato al tuo gioco!"*.
- `CompleteChunkedUploadCommandHandler.cs:112-139` — **comportamento opposto**: rigetta con `DuplicateContentErrorMessage:36`.

### 3.4 Proposal system (moderazione, esiste)

`ShareRequest` (entity) + `ContributionType` (VO) + `ApproveGameProposalCommand`/Handler/Validator + `ProposalApprovalAction` (enum) + notifiche (`ShareRequestReviewStarted/Approved`) + email (`EmailService.ShareRequests`) + endpoint admin (`SharedGameCatalogAdminShareRequestEndpoints`). Usato oggi per proporre un gioco privato al catalogo (private→shared). `PdfDocument.CreateCopy:941` (call-site `ShareRequestDocumentService.cs:201`) dimostra il pattern copia-su-approvazione (#2732).

### 3.5 Integrazione Wikidata (cover completa admin-only; metadati assenti)

**Cover L2 — production-hardened, trigger solo-admin.** Catena: `WikidataCatalogProvider.FetchCoverImageAsync(qid)` (`:116`, SPARQL `wdt:P18`, QID regex `^Q\d+$` `:211`) → `WikimediaCommonsClient.FetchLicenseAsync` (`:60`) + `LicenseValidator` whitelist PD/CC0/CC-BY/CC-BY-SA (`:34`) → WebP 200×300 q85 (`WebpVariantGenerator.cs:40`) → `CoverR2UploadPipeline.cs:35` (`covers/{gameId}/cover.webp`) → `EnrichCatalogCoverCommandHandler.cs:97` (M8) → `SharedGame.SetWikidataCover`. **Runner condiviso** `WikidataCoverEnrichmentRunner.EnrichAndRecordAsync` (`:45`) è single-source-of-truth per scheduler **e** admin. Scheduler Quartz 1 min batch 30 (`WikidataCoverEnrichmentJob.cs:38`). Rate-limit 5 RPS shared (`InMemoryWikimediaRateLimiter`), circuit breaker Polly, retry max-3 + DLQ + audit. Freshness 90gg, re-verifica trimestrale M15.

**Esposizione HTTP — solo admin.** `POST /api/v1/admin/wikidata/enrichment/{gameId}` (+dead-letters, bulk-retry, SSE, `AdminWikidataCoverEnrichmentEndpoints.cs:30-76`); `POST /admin/catalog/covers/enrich-batch` max 200 fire-and-forget (`SharedGameCatalogAdminEndpoints.cs:429`). Command `AdminEnrichWikidataCoverCommand.cs:24`. Nessun endpoint user.

**Metadati non-cover — ASSENTI come scrittura.** Solo preview read-only: `WikidataSearchQuery`→`WikidataSearchResult` con `FieldProvenance` (`:12-27`, `Fields:24`), per la seed-queue admin. Nessun command idrata un `SharedGame` esistente. `SharedGame.AssignWikidataQid:651` linka solo il QID. Metadati di gioco vengono da BGG (`EnrichFromBgg:544`) o seed.

**Attribuzione.** `MeepleCardAttributionFooter.tsx:29` rende license+attribution+sourceUrl (null se license null). `AttributionTextExtractor.cs:27` HTML-strip server-side. Wikimedia in allowlist `next.config.js:148`.

### 3.6 Freeze BGG (#2123 / ADR-059 §5)

Ban lato browser di host `*.geekdo-images.com` / `*.boardgamegeek.com`. Enforcement: Next.js allowlist fail-closed (`next.config.js`), ESLint `local/no-bgg-host`, safe-loader runtime + metrica SLO=0 `meepleai_bgg_url_attempted_render_total`. `AddPrivateGameRequest` espone `ImageUrl:string?` (`:453`) **e** `ThumbnailUrl:string?` (`:454`): **entrambi** canali di rientro di URL esterni da chiudere. BGG resta admin-server-side-only; Wikimedia (CC0/whitelist) è la fonte "giusta" da esporre agli utenti.

---

## 4. Design

### § 4.1 Le tre sorgenti cover

Default = placeholder deterministico. Sorgenti attive:

| Sorgente | Per | Natura | Livello |
|----------|-----|--------|---------|
| Pagina-PDF | User + Admin | contenuto **arbitrario** | L4 (`PdfCoverR2Key`) |
| Wikidata | User + Admin | **deterministico** (P18 del QID), licenza-validato | L2 (`WikidataCoverR2Key`) |
| Placeholder | — | fallback automatico | — |

**Vincolo**: Wikidata richiede un `WikidataQid` assegnato → disponibile **solo su SharedGame** (catalogo). I `PrivateGame` non hanno QID → sorgente = pagina-PDF + placeholder.

### § 4.2 Matrice di governance

| Attore | Entità | Sorgente | Approvazione |
|--------|--------|----------|--------------|
| User | PrivateGame | pagina-PDF | **no** (cover privata) |
| User | PrivateGame | Wikidata | **N/A** (no QID sui private game) |
| User | SharedGame | pagina-PDF (L4) | **sì** — proposal + admin |
| User | SharedGame | Wikidata cover (L2) | **no** — diretto |
| User | SharedGame | Wikidata metadati | **no** — diretto, **solo fill-gaps** |
| Admin | SharedGame | tutte | **no** (`forceRefresh` admin-only) |

### § 4.3 Cover-da-PDF: un picker, due ingressi

- Componente unico `CoverPagePicker` (wrapper/estrazione da `CoverImagePicker` admin, oggi in `admin/.../shared-games/import/`). Per l'utente: tab *Dal PDF* + placeholder default.
- **Ingresso 1**: al termine dell'upload PDF (`ProcessingState == Ready`) → prompt "usa una pagina come cover?".
- **Ingresso 2**: azione "Imposta cover" sulla pagina del gioco → elenca i PDF `Ready` → picker.
- Conferma → command `MaterializePdfCover` **sync**: render pagina (via `GetPdfPageImageQuery`) → encode WebP → upload R2 → set key. Su SharedGame → non scrive L4 direttamente, ma crea la proposal (§ 4.4).

Data flow (SharedGame, con moderazione):

```
utente sceglie (pdfId, pageN)
  → MaterializePdfCover: render → WebP → R2 (area "pending")
  → ShareRequest{ ContributionType: CoverChange, pendingCoverR2Key, pdfId, pageIndex }
  → notifica/email admin (infra esistente)
admin approva → promuove pendingCoverR2Key → SharedGame.PdfCoverR2Key (L4)
admin rifiuta  → cleanup best-effort della pending R2 key
```

Per `PrivateGame`: stesso `MaterializePdfCover`, scrittura **diretta** sulla cover del gioco privato (nessuna coda).

### § 4.4 Governance L4 pagina-PDF: estensione ShareRequest

Aggiungere il valore `CoverChange` a `ContributionType`; il flusso di approvazione riusa `ApproveGameProposalCommand` + notifiche + email + endpoint admin. All'approvazione: promozione della pending key a `SharedGame.PdfCoverR2Key`. Al rifiuto: cleanup della pending key.

### § 4.5 Wikidata cover on-demand (utente diretto)

- Nuovo endpoint autenticato `POST /api/v1/games/{gameId}/cover/wikidata-refresh` → command `UserRequestWikidataCoverCommand` → **stesso** `WikidataCoverEnrichmentRunner.EnrichAndRecordAsync` (`:45`).
- **Identità dell'attore** (decisione di WP5): la signature attuale è `EnrichAndRecordAsync(Guid gameId, bool forceRefresh, Guid? triggeredByAdminUserId = null, CancellationToken)`. Passare uno user non-admin nel parametro `triggeredByAdminUserId` è semanticamente errato e inquina l'audit. WP5 **generalizza** l'identità: rinominare `triggeredByAdminUserId` in un attore tipizzato (`TriggeredBy { UserId, Role }`) — oppure, per minimizzare il blast radius, aggiungere un parametro distinto `triggeredByUserId` + flag `isAdminTrigger`. L'audit deve registrare id + ruolo corretti.
- **`forceRefresh` enforced al command boundary**: `UserRequestWikidataCoverCommand` **non espone affatto** `forceRefresh` (il campo non esiste nel command user; solo il path admin può passare `true`). Non è una validazione UI — è assenza del campo dal contratto.
- **Rate-limit per-utente**: max **10 refresh Wikidata / utente / ora**, backing store **Redis** (sliding window), a monte del runner. In aggiunta al budget Wikimedia condiviso (5 RPS, `InMemoryWikimediaRateLimiter`) che il trigger user NON bypassa.
- Precondizione: il gioco deve avere `WikidataQid` assegnato; altrimenti l'opzione non è offerta.

### § 4.6 Wikidata metadati (nuovo, fill-gaps)

- Nuovo metodo di dominio `SharedGame.EnrichFromWikidata(fields, provenance)` + command di scrittura, alimentato da `WikidataSearchResult.Fields` + `FieldProvenance`.
- **Policy di conflitto vs BGG — fill-gaps only**: Wikidata idrata **solo i campi vuoti**; **non sovrascrive** valori già presenti, verificando via `FieldProvenance`. L'admin può forzare l'overwrite; l'utente no.
- **Definizione di "campo vuoto" (gap)**: un campo è un gap se **(a)** non ha `FieldProvenance` (nessuna fonte lo ha reclamato) **e (b)** il valore è nullo o, per le stringhe, whitespace-only. La `FieldProvenance` ha **precedenza sul valore**: provenance presente ⇒ campo reclamato ⇒ **non-gap**, anche se il valore è null (evita che Wikidata sovrascriva una scelta deliberata di lasciare un campo non valorizzato). Per i numerici (anno, min/max giocatori) uno `0`/sentinella con provenance presente **non** è un gap.
- Esposizione: endpoint admin subito; endpoint user (diretto, fill-gaps) coerente con DEC-8.

### § 4.7 Deduplicazione PDF (consolidamento)

- Estrarre la regola *"hash noto → riusa via EntityLink"* in un `PdfDeduplicationService` di dominio, invocato da **tutti** i path di ingest.
- **Allineare `CompleteChunkedUploadCommandHandler`**: da rigetto (`DuplicateContentErrorMessage`) a riuso trasparente con feedback esplicito.
- Scope: **globale** sui PDF di catalogo (`SharedGameId`), **per-utente** sui privati (`PrivateGameId`), via il parametro già presente in `ExistsByContentHashAsync(hash, gameId, privateGameId)`.
- Regola lifecycle: un PDF è **eliminabile solo alla rimozione dell'ultimo `EntityLink`** (reference counting).

### § 4.8 Error handling & compliance

- SmolDocling 503/404 alla conferma → materializzazione **rifiutata**, messaggio non-bloccante, gioco resta con placeholder. Nessuno stato "cover a metà".
- Selezione pagina disponibile **solo** con `ProcessingState == Ready` && `PageCount != null`.
- Licenza whitelist PD/CC0/CC-BY/CC-BY-SA; cover non conformi → skip senza far apparire un "errore utente" nella UX.
- **Attribuzione**: ogni cover Wikidata popola le 3 colonne o la footer sparisce; **azzerare** le 3 colonne quando L3/L4 rimpiazza L2 (fix del cascade-gap noto; **net-new**: il resolver oggi solo legge — serve un nuovo command/handler che azzeri le colonne al cambio di livello vincente).
- Nessun URL esterno arbitrario come cover utente: **chiudere entrambi** i campi `AddPrivateGameRequest.ImageUrl` **e** `ThumbnailUrl` (`PrivateGameEndpoints.cs:453-454`, compliance BGG #2123).
- Metrica: `meepleai.cover.resolution.total{source=r2_pdf|r2_wikidata|placeholder}`.

---

## 5. Scenari di accettazione (Gherkin)

### WP1 — Dedup PDF

```gherkin
Scenario: Riuso trasparente su hash noto (path chunked, oggi rigetta)
  Given un PDF Ready con ContentHash H esiste per un gioco di catalogo
  When un utente carica via upload chunked un file con lo stesso ContentHash H
  Then il sistema NON crea un doppione fisico
  And collega l'utente al PDF esistente via EntityLink
  And mostra il feedback esplicito "Regolamento già disponibile — collegato al tuo gioco!"

Scenario: Isolamento sui PDF privati
  Given l'utente A ha un PDF privato con ContentHash H (PrivateGameId)
  When l'utente B carica lo stesso file su un proprio gioco privato
  Then B ottiene un proprio record (nessun cross-user link sui privati)

Scenario: Eliminazione bloccata finché esistono link
  Given un PDF con 2 EntityLink attivi
  When si rimuove 1 dei 2 link
  Then il PDF NON viene eliminato
  When si rimuove anche il secondo link
  Then il PDF diventa eliminabile
```

### WP2 — Materializzazione cover-da-PDF

```gherkin
Scenario: Utente sceglie pagina 3 come cover (PrivateGame)
  Given un PDF Ready con PageCount=12 su un gioco privato
  When l'utente seleziona pagina 3 e conferma
  Then la pagina 3 è renderizzata → WebP → caricata su R2
  And la cover del gioco privato punta alla nuova key
  And le richieste successive NON ri-renderizzano il PDF

Scenario: SmolDocling non disponibile alla conferma
  Given SmolDocling risponde 503
  When l'utente conferma la pagina-cover
  Then la materializzazione è rifiutata con messaggio non-bloccante
  And il gioco resta con placeholder (nessuno stato cover-a-metà)
```

### WP3/WP4 — Picker + proposal L4 (SharedGame)

```gherkin
Scenario: Cover-da-PDF su gioco di catalogo passa da approvazione admin
  Given un utente sceglie pagina 5 di un PDF su uno SharedGame
  When conferma
  Then viene creata una ShareRequest con ContributionType=CoverChange e la pending R2 key
  And un admin riceve notifica
  When l'admin approva
  Then SharedGame.PdfCoverR2Key = pending key (L4 pubblica)
  When invece l'admin rifiuta
  Then la pending R2 key viene ripulita best-effort
```

### WP5 — Wikidata cover on-demand utente

```gherkin
Scenario: Utente recupera la cover Wikidata (diretto, no approvazione)
  Given uno SharedGame con WikidataQid assegnato e nessuna cover L3/L4
  When l'utente invoca POST /api/v1/games/{id}/cover/wikidata-refresh
  Then il runner condiviso recupera P18, valida la licenza, materializza L2
  And le 3 colonne di attribuzione vengono popolate
  And nessuna approvazione admin è richiesta

Scenario: Rate-limit per-utente
  Given l'utente ha superato la propria soglia di refresh Wikidata
  When invoca di nuovo l'endpoint
  Then riceve un errore di rate-limit per-utente
  And il budget Wikimedia condiviso (5 RPS) non viene intaccato

Scenario: forceRefresh negato all'utente
  When un utente prova a forzare il refresh entro la finestra di freshness 90gg
  Then l'operazione è negata (forceRefresh è admin-only)

Scenario: L2 non sovrascrive una cover superiore
  Given uno SharedGame con cover L4 (pagina-PDF approvata)
  When un utente recupera la cover Wikidata (L2)
  Then il resolver continua a servire L4 (L2 sta sotto)
```

### WP6 — Wikidata metadati (fill-gaps)

```gherkin
Scenario: Wikidata riempie solo i campi vuoti
  Given uno SharedGame con "anno" valorizzato da BGG e "descrizione" vuota
  When si esegue l'enrichment metadati da Wikidata
  Then la "descrizione" viene riempita da Wikidata
  And l'"anno" (provenance BGG) NON viene sovrascritto

Scenario: Admin può forzare l'overwrite
  Given un campo con provenance BGG
  When un admin esegue l'enrichment Wikidata con override
  Then il campo viene sovrascritto (privilegio admin-only)
```

### WP7 — Attribuzione & metriche

```gherkin
Scenario: Attribuzione mostrata per cover Wikidata
  Given uno SharedGame con cover L2 Wikidata (license CC-BY)
  Then la footer di attribuzione mostra license + attribution + sourceUrl

Scenario: Cascade-clear al rimpiazzo di L2
  Given uno SharedGame con cover L2 e attribuzione popolata
  When una cover L4 (pagina-PDF) viene approvata e diventa vincente
  Then le 3 colonne di attribuzione Wikidata vengono azzerate
```

---

## 6. Work package

| WP | Contenuto | Dipende da |
|----|-----------|-----------|
| **WP1** | Consolidamento dedup PDF: `PdfDeduplicationService` unico + allineamento `CompleteChunkedUploadCommandHandler` al riuso + regola reference-counting | — |
| **WP2** | Command `MaterializePdfCover` sync (render→WebP→R2) + chiusura di `AddPrivateGameRequest.ImageUrl` **e** `ThumbnailUrl` | WP1 |
| **WP3** | FE `CoverPagePicker` (wrapper) + due ingressi (post-upload + azione "Imposta cover") | WP2 |
| **WP4** | Governance L4 pagina-PDF: `ContributionType.CoverChange` su `ShareRequest` + promozione/cleanup su approvazione/rifiuto | WP2 |
| **WP5** | Wikidata cover on-demand user: endpoint + `UserRequestWikidataCoverCommand` + rate-limit per-utente | — |
| **WP6** | Wikidata metadati: `SharedGame.EnrichFromWikidata` + policy fill-gaps + command + esposizione admin/user | — |
| **WP7** | Compliance: attribuzione footer su tutte le cover + cascade-clear + metriche `cover.resolution.total` | WP4, WP5 |

Ordine di valore suggerito: WP1 → WP2 → WP3 → WP4 (fronte cover-da-PDF), poi WP5 → WP6 (fronte Wikidata), infine WP7 (compliance trasversale).

---

## 7. Strategia di test

- **Unit (dominio)**: `PdfDeduplicationService` (globale/privato, stati Ready/Failed), regola reference-counting, `MaterializePdfCover` state transitions, `EnrichFromWikidata` fill-gaps + provenance.
- **Integration (Testcontainers Postgres)**: dedup end-to-end sui due path (diretto + chunked ora coerenti), proposal CoverChange → approvazione → promozione L4, Wikidata refresh user → L2, rate-limit per-utente.
- **E2E / acceptance**: gli scenari Gherkin di § 5.
- Target coverage backend 90%+, frontend 85%+ (baseline di progetto).

---

## 8. Out of scope / follow-up

- **Upload immagine custom utente (L3, #1824)**: escluso per compliance (provenance ignota). Resta un possibile follow-up separato.
- **Async/batch materializzazione**: non necessario per una singola pagina (DEC-9). Da rivalutare solo se emergono flussi batch.
- **Promozione PrivateGame → SharedGame** (#3665 Phase 4): fuori scope. **Regola esplicita sulla cover**: la promozione **non** trasferisce la cover privata (materializzata da PDF) a L4 — la cover pubblica deve passare dal flusso `CoverChange` proposal come qualsiasi altra cover di catalogo. La copia WebP privata resta associata al `PrivateGame` finché esiste, poi orfanizzata (cleanup best-effort).
- **Selezione automatica della pagina-cover "migliore"** (#1852 Gap A): questo spec richiede scelta esplicita dell'utente; l'auto-selezione resta follow-up.

---

## 9. Riferimenti

- Freeze BGG: #2123, [ADR-059 §5](../../for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md)
- Proposal system: #3665 (private→shared), pattern copy-on-approval #2732
- Cover L3 user custom: #1824 (non implementato)
- PDF cover gap: #1852 (`MarkCoverGenerated`)
- Citation dedup (contesto SHA-256): #2051
- File chiave: vedi § 3 per le citazioni `path:riga`.
