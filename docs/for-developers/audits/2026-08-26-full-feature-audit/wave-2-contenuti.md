# Ondata 2 — Contenuti

> **Contesti**: SharedGameCatalog · GameManagement · UserLibrary · DocumentProcessing
> **Ambiente**: locale, `make dev`, stack completo · **Data**: 2026-08-26
> **Ruoli**: `test@meepleai.com` (utente) · `badsworm@gmail.com` (superadmin)

## Esito in breve

| Contesto | Coperte | Totale |
|---|---|---|
| SharedGameCatalog | 85 | 181 |
| GameManagement | 68 | 131 |
| UserLibrary | 49 | 91 |
| DocumentProcessing | 33 | 71 |

Il finding più importante riguarda il percorso centrale del prodotto: **caricare il manuale di un
gioco risponde 200 e l'elaborazione fallisce subito dopo**, senza che l'utente ne sappia nulla.

## Il ciclo di upload PDF — [#3846](https://github.com/meepleAi-app/meepleai-monorepo/issues/3846)

Caricato un PDF reale (49 KB) su un gioco esistente:

| Passo | Esito |
|---|---|
| `POST /api/v1/ingest/pdf` | **200**, `documentId` restituito |
| Riga in `pdf_documents` | creata, nome e dimensione corretti |
| `processing_state` | **`Failed`**, `failed_at_state: Extracting` |
| `ProcessingError` | `Could not find a part of the path '/app/pdfs/<gameId>/<hash>_….pdf'` |
| File nel container | **assente** |

Con `STORAGE_PROVIDER=s3` l'upload invia il file allo storage remoto, mentre l'estrazione lo cerca
sul filesystem locale. In tutto `DocumentProcessing` **un solo servizio** usa `IStorageService`
(`ShareRequestDocumentService`); l'orchestratore accede al disco direttamente.

**Cosa non ho verificato**: nel database di prova ci sono 132 PDF in stato `Ready`, quindi il
flusso ha funzionato in qualche configurazione. Non ho accertato il valore di `STORAGE_PROVIDER`
sugli ambienti reali né come quei 132 siano stati elaborati. La issue riporta il comportamento
osservato, non una diagnosi su staging.

Da notare comunque: l'utente riceve **200** e nessun segnale successivo. Il documento resta
inutilizzabile e l'interfaccia non lo dice.

## Letture

149 endpoint provati con entrambi i ruoli.

| Contesto | Provati | Conformi | Difformi |
|---|---|---|---|
| DocumentProcessing | 26 | 17 | 9 |
| UserLibrary | 23 | 19 | 4 |
| GameManagement | 34 | 27 | 7 |
| SharedGameCatalog | 66 | 45 | 21 |

22 endpoint sono stati saltati perché richiedono parametri non risolvibili in questo ambiente
(collezioni, job, immagini di pagina): restano `⬜ non coperto`.

### Nove endpoint in 500, quattro famiglie di cause

| Endpoint | Causa | Issue |
|---|---|---|
| `/games/{id}/similar` | `InternalDbSet.get_EntityType()` — entità non nel modello del context | [#3839](https://github.com/meepleAi-app/meepleai-monorepo/issues/3839) |
| `/admin/shared-games/pending-deletes` | LINQ non traducibile | #3839 |
| `/admin/shared-games/{id}/documents/overview` (2 varianti) | 500 | #3839 |
| `/library/games/batch-status` | LINQ non traducibile | #3839 |
| `/wizard/game-preview/{gameId}` | `ConcurrencyDetector` — query concorrenti sullo stesso DbContext | [#3843](https://github.com/meepleAi-app/meepleai-monorepo/issues/3843) |
| `/users/me/contribution-stats` | `ShareRequestLimit` non deserializzabile da System.Text.Json | [#3845](https://github.com/meepleAi-app/meepleai-monorepo/issues/3845) |
| `/resources/cache/metrics` | `StackExchange.Redis.CheckMessage` | #3845 |
| `/resources/database/tables/top` | colonna SQL inesistente | [#3833](https://github.com/meepleAi-app/meepleai-monorepo/issues/3833) |

### Un difetto che si nascondeva dietro una richiesta incompleta

`GET /library/games/batch-status` risponde **400** senza `gameIds` e **500** appena glielo si
fornisce. Una sonda che si fermasse al 400 lo archivierebbe come "parametro mancante" e non lo
vedrebbe mai. È la ragione per cui i 400 vanno ricontrollati con i parametri giusti, non scartati.

Sedici endpoint hanno risposto 400 per parametri mancanti; il campione ricontrollato ne ha
riabilitati due (`/library/entity-links` e `/catalog/trending` rispondono 200 con i parametri) e
scoperto il caso sopra. I restanti non sono marcati come difetti.

## Cosa resta

| Area | Righe | Nota |
|---|---|---|
| Mutazioni di contenuto | ~200 | Creazione e modifica di giochi, voci di libreria, collezioni, richieste di condivisione |
| Letture con parametri non risolvibili | 22 | Servono collezioni, job e immagini di pagina che l'ambiente non ha |
| Manutenzione PDF | ~6 | `bulk/delete`, `purge-stale`, `cleanup-orphans`, `seeding/orchestrate`: distruttive, da provare su ambiente sacrificabile |
