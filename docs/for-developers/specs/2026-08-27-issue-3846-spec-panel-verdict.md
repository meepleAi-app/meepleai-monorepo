# #3846 — spec-panel verdict: upload PDF con storage S3

**Issue**: [#3846](https://github.com/meepleAi-app/meepleai-monorepo/issues/3846) — con `STORAGE_PROVIDER=s3` l'upload risponde 200 ma l'elaborazione fallisce
**Origine**: ondata 2 del [Full Feature Audit](../audits/2026-08-26-full-feature-audit/README.md)
**Data**: 2026-08-27
**Metodo**: `/sc:spec-panel` — modalità critique (Wiegers · Nygard · Fowler · Adzic · Crispin · Hightower)
**Stato**: verdetto emesso, DoD proposta. Il fix non è implementato.
**Precedenti diretti**: [#2671](https://github.com/meepleAi-app/meepleai-monorepo/issues/2671) (fileId ricostruito) · [#3568](https://github.com/meepleAi-app/meepleai-monorepo/issues/3568) (resourceKey ricostruito, corretto solo sul download)

---

## 1. Le tre domande aperte della issue hanno risposta

La issue si chiudeva con tre incognite dichiarate. Sono state risolte prima di far parlare il panel,
perché due di esse cambiano il contenuto del fix.

| Domanda | Risposta verificata | Evidenza |
|---|---|---|
| Valore reale di `STORAGE_PROVIDER` su staging | **`s3`** — Cloudflare R2, bucket `meepleai-uploads` | `docker exec meepleai-api printenv` sul VPS staging |
| Come sono stati elaborati i PDF in stato `Ready` | **Non dal percorso di upload.** 127 righe, tutte `Ready`, tutte con `FilePath = pdfs/{pdfId-N}/{fileId}_nome.pdf` | `select "Id", "FilePath" from pdf_documents order by "UploadedAt" desc` su staging |
| Il difetto si manifesta anche su staging? | **Sì**, per ogni upload che passa da `POST /api/v1/ingest/pdf` | vedi §2 |

Il corpus di staging **non** prova che il flusso funzioni: prova che funziona un percorso diverso da
quello rotto. Il seed scrive sotto una cartella pari al `pdfId`, che è esattamente ciò che il lato di
lettura ricostruisce — combaciano per coincidenza di convenzione, non per contratto.

## 2. Rettifica: la causa indicata nella issue è imprecisa

La issue afferma che «l'orchestratore di elaborazione e i percorsi di estrazione accedono al filesystem
direttamente» e che un solo servizio usa l'astrazione dello storage. Non è così, e la differenza cambia
il fix.

Il bounded context usa `IBlobStorageService` in oltre 30 punti — è l'astrazione viva.
`IStorageService`/`LocalStorageService`, citata nella issue, è una **seconda** astrazione quasi morta
(un solo consumatore: `ShareRequestDocumentService`). La pipeline di elaborazione **passa già** dal blob
store: il difetto è che compone la chiave di lettura sbagliata, e il fallback filesystem trasforma il
MISS in un errore fuorviante.

```
scrittura  UploadPdfCommandHandler.cs:557   resourceKey = gameId ?? privateGameId
           UploadPdfCommandHandler.cs:602   pdfDoc.Id   = fileId random generato da StoreAsync
           => chiave S3 reale:  pdfs/{gameId}/{fileId}_manuale.pdf

lettura    UploadPdfCommandHandler.Processing.cs:230   bucketKey = PdfStorageKey.ForPdf(pdfDoc.Id)
           UploadPdfCommandHandler.Processing.cs:231   RetrieveAsync(fileId, Pdf, bucketKey) -> null
           UploadPdfCommandHandler.Processing.cs:236   new FileStream(filePath)   <- filePath e' una CHIAVE S3
           => "Could not find a part of the path '/app/pdfs/<gameId>/<hash>_manuale.pdf'"
```

Il `fileId` è corretto su questo percorso (`pdfDoc.Id` **è** il fileId, riga 602): è la **cartella** a
essere ricostruita invece che letta. Con storage locale il difetto è invisibile, perché `FilePath` è un
percorso assoluto reale e il fallback filesystem lo apre senza problemi.

È lo stesso difetto di #3568, chiuso il 2026-08-05 riparando **solo** `DownloadPdfQueryHandler.cs:91`
— tuttora l'unico consumatore di `PdfStorageKey.ResourceKeyFromPath`. Il percorso di ingestione è
rimasto indietro, insieme a `PdfProcessingPipelineService.cs:529` e `:1161`.

**Corollario utile**: derivare il resourceKey da `FilePath` è retrocompatibile per costruzione anche sui
127 PDF di staging, perché lì `FilePath` contiene già il `pdfId`. Il fix non richiede una migrazione dei
dati né un rebucket.

## 3. Findings del panel

| # | Sev | Esperto | Finding |
|---|---|---|---|
| F-1 | 🔴 | Nygard | Il fallback al filesystem accetta una chiave S3 come percorso: degrada un MISS in un errore che punta all'oggetto sbagliato — ed è ciò che ha prodotto la diagnosi errata nella issue |
| F-2 | 🔴 | Nygard | Secondo guasto, **silenzioso**: con S3 tabelle/diagrammi/regole atomiche non vengono mai estratti, e il PDF arriva comunque a `Ready` |
| F-3 | 🔴 | Crispin | Nessun test in essere può fallire per questo difetto: la suite gira con `STORAGE_PROVIDER=local`, dove il fallback copre il MISS |
| F-4 | 🔴 | Wiegers | La DoD non è verificabile: 1 punto già risolto, 1 non falsificabile, 1 troppo debole, 1 fondato su una premessa errata |
| F-5 | 🟡 | Fowler | La stessa chiave è composta in 4 punti con 3 regole diverse; è già andata storta 3 volte (#2671, #3568, #3846) |
| F-6 | 🟡 | Adzic | La spec non contiene un solo esempio del formato delle chiavi, che è l'intero contenuto del difetto |
| F-7 | 🟡 | Hightower | Il quarto punto della DoD è già implementato e spento: `NEXT_PUBLIC_ENABLE_PROGRESS_UI` non è definita in nessun ambiente |
| F-8 | 🟢 | Fowler | Due astrazioni di storage coesistenti — non è la causa, ma ha indotto in errore la diagnosi |
| F-9 | 🟢 | Hightower | Nessuna metrica distingue «blob assente» da «estrazione fallita» |

### F-2 in dettaglio (il finding che la DoD attuale non intercetterebbe)

`ExtractStructuredContentAsync` (`UploadPdfCommandHandler.Processing.cs:291` → `:312`) passa `filePath`
a `ITextPdfTableExtractor`, che fa `File.Exists` e ritorna `CreateFailure("File not found: …")`; il
chiamante reagisce con `if (!structuredResult.Success) return;`. Nessun log di errore, nessuno stato
`Failed`, nessun effetto sul passaggio a `Ready`.

Conseguenza operativa: **riparando solo il resourceKey, la DoD «con storage remoto un upload arriva a
Ready» diventerebbe verde su un documento amputato.** Lo stesso schema — firme che prendono un path e
presuppongono un disco — vale per `TesseractOcrAdapter`, `VisionOcrAdapter` e `BggGameExtractor`.

### F-3 in dettaglio (perché è arrivato in produzione)

`infra/secrets/test.secret:53` fissa `STORAGE_PROVIDER=local`. In quella configurazione `FilePath` è un
percorso assoluto reale e il fallback filesystem restituisce il file: il difetto è **strutturalmente
invisibile** alla configurazione con cui testiamo. Un doppio in-memory di `IBlobStorageService` che
rifiuta le letture fuori dalla chiave scritta lo coglierebbe a livello unit.

### F-6 — l'esempio mancante

```gherkin
Dato   STORAGE_PROVIDER=s3 e un gioco {gameId}
Quando carico manuale.pdf via POST /api/v1/ingest/pdf
Allora l'oggetto esiste in  pdfs/{gameId}/{fileId}_manuale.pdf
E      l'estrazione lo legge da quella chiave, non da pdfs/{pdfId}/...
E      processing_state raggiunge Ready con table_count > 0 su una fixture con tabelle
```

L'infrastruttura per eseguirlo **esiste già**: `infra/compose.e2e-storage.yml` (MinIO,
`STORAGE_PROVIDER=s3`), usata da `.github/workflows/e2e-cover-r2-strict.yml`. Il costo è aggiungere un
job, non costruire un ambiente.

## 4. Scorecard della specifica

| Dimensione | Voto | Nota |
|---|---|---|
| Chiarezza | 8/10 | sintomo, comando di riproduzione e riga di DB: esemplare |
| Onestà epistemica | 9/10 | «segnalo il comportamento osservato, non una diagnosi»: ha evitato di propagare l'errore |
| Accuratezza della causa | 4/10 | astrazione sbagliata indicata (vedi §2) |
| Completezza | 4/10 | mancano F-2 e il precedente #3568 |
| Testabilità della DoD | 3/10 | vedi F-4 |
| **Complessivo** | **5,6/10** | ottima segnalazione, spec di fix non ancora eseguibile |

## 5. DoD proposta

- [ ] La lettura del blob deriva il resourceKey da `FilePath` persistito (`PdfStorageKey.ResourceKeyFromPath`)
      nei tre punti rimasti: `UploadPdfCommandHandler.Processing.cs:230`,
      `PdfProcessingPipelineService.cs:529` e `:1161`
- [ ] Il fallback `new FileStream(filePath)` è attivo solo con `STORAGE_PROVIDER=local`; altrove il MISS
      fallisce con `error_category = StorageObjectMissing` e messaggio contenente la chiave cercata
- [ ] Il contenuto strutturato (tabelle/diagrammi/regole atomiche) è estratto anche con storage remoto —
      materializzando un temp file dal blob — e il suo fallimento non è più silenzioso (F-2)
- [ ] Test unit su un doppio di `IBlobStorageService` che rifiuta le letture fuori dalla chiave scritta:
      deve essere rosso prima del fix (F-3)
- [ ] Job E2E su `infra/compose.e2e-storage.yml`: upload → `Ready` **con `table_count > 0`** su una
      fixture con tabelle note
- [ ] Verificato su staging che un upload reale raggiunge `Ready` e che l'oggetto esiste alla chiave
      persistita
- [ ] `NEXT_PUBLIC_ENABLE_PROGRESS_UI` attivata negli ambienti (è una `NEXT_PUBLIC_*`, quindi build arg)
      **oppure** scorporata in una issue separata con motivazione (F-7)

Il primo punto della DoD originale — «chiarito il valore di `STORAGE_PROVIDER`» — è risolto in §1 e va
spostato nel corpo della issue come contesto: non è un requisito, è un'indagine.

## 6. Nota implementativa

**Opzione raccomandata — leggere la posizione, non ricostruirla.** `FilePath` è l'unico record di dove il
file è realmente finito (è già la conclusione scritta nel commento di `ResourceKeyFromPath`). Un value
object `PdfBlobLocation`, costruito solo da `FilePath` e usato da tutti i lettori, chiude la classe di
difetti invece del singolo caso; `PdfStorageKey.ForPdf(Guid)` va confinato al lato scrittura.

**Opzione scartata — uniformare il resourceKey di scrittura al `pdfId`.** Renderebbe scrittura e lettura
coerenti, ma richiede un rebucket degli oggetti esistenti scritti sotto `{gameId}` e lascia in piedi la
ricostruzione a mano della chiave in 4 punti. Più rischiosa e meno risolutiva.

## 7. Verifiche eseguite

```bash
# provider e corpus su staging
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  "docker exec meepleai-api printenv | grep ^STORAGE_PROVIDER="
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  "docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -tAc \
   'select \"Id\"::text, \"FilePath\" from pdf_documents order by \"UploadedAt\" desc limit 6;'"

# unico consumatore di ResourceKeyFromPath
grep -rn "ResourceKeyFromPath" --include=*.cs apps/api/src/Api | grep -v Tests

# il flag della UI di progresso non e' definito in nessun ambiente
grep -rn "NEXT_PUBLIC_ENABLE_PROGRESS_UI" infra/ apps/web/.env* apps/web/next.config.js
```
