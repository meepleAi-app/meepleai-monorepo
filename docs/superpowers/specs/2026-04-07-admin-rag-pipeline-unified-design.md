# Admin RAG Pipeline — Pagina Unificata

**Data**: 2026-04-07
**Stato**: Draft
**Scope**: Backend fixes + Frontend unificazione pagina admin RAG/embedding

## Problema

L'esperienza admin per gestire il flusso RAG (upload PDF → processing → embedding → indexing) è:

1. **Frammentata**: 6+ pagine separate (KB Hub, Queue, Processing, Embedding, Settings, RAG Wizard)
2. **Backend rotto**: I PDF privati vengono uploadati ma mai messi in coda per il processing
3. **Metriche vuote**: Le ETA non hanno dati storici perché `RecordStepDurationAsync()` non viene mai chiamato

## Soluzione

### Approccio: Refactor Completo (A)

Creare una pagina admin unificata con layout ibrido (dashboard + tab) e fixare i 3 bug backend critici. Massimo riuso dei componenti esistenti.

---

## Backend Fixes

### Fix 1: Event Handler `PrivatePdfAssociatedEvent`

**File**: `DocumentProcessing/Application/EventHandlers/PrivatePdfAssociatedEventHandler.cs`

**Problema**: `TriggerPdfProcessingAsync()` in `UploadPrivatePdfCommandHandler` è uno stub che ritorna `Task.CompletedTask`. Non esiste nessun handler per `PrivatePdfAssociatedEvent`. I PDF privati vengono caricati e salvati ma mai processati.

**Soluzione**:
- Creare `INotificationHandler<PrivatePdfAssociatedEvent>` che:
  1. Recupera il `PdfDocument` dal repository
  2. Crea un `ProcessingJob` con priority 10
  3. Salva il job nel `IProcessingJobRepository`
  4. Pubblica evento SSE `JobQueued` via `IQueueStreamService`
- Rimuovere il metodo stub `TriggerPdfProcessingAsync()`
- Registrare l'handler nel DI (MediatR lo fa automaticamente via assembly scan)

**Verifica**: Upload un PDF privato → il PDF appare nella coda processing entro pochi secondi.

### Fix 2: Registrazione Metriche nel Quartz Job

**File**: `DocumentProcessing/Application/Jobs/PdfProcessingQuartzJob.cs`

**Problema**: Il job processa i PDF e calcola la durata di ogni step, ma non chiama mai `RecordStepDurationAsync()` del `IProcessingMetricsService`. Le ETA calcolate da `CalculateETAAsync()` cadono sempre sul fallback statico (2s/pagina).

**Soluzione**:
- Iniettare `IProcessingMetricsService` nel Quartz job
- Dopo ogni step completato, chiamare:
  ```csharp
  await _metricsService.RecordStepDurationAsync(pdfId, stepType, duration, fileSize, pageCount);
  ```
- I dati storici si accumulano e le ETA diventano progressivamente più accurate

**Verifica**: Dopo 10+ PDF processati, `GetAllStepStatisticsAsync()` ritorna dati reali (non solo fallback).

### Fix 3: Verifica Endpoint Admin Queue

**Problema potenziale**: Gli endpoint SSE e query per la coda potrebbero non essere tutti registrati nel routing.

**Azione**:
- Verificare che tutti gli endpoint usati dal frontend siano registrati:
  - `GET /api/v1/admin/queue` — lista coda con filtri
  - `GET /api/v1/admin/queue/stream` — SSE coda globale
  - `GET /api/v1/admin/queue/{jobId}` — dettaglio job
  - `GET /api/v1/admin/queue/{jobId}/stream` — SSE job singolo
  - `POST /api/v1/admin/queue/enqueue` — enqueue manuale
  - `POST /api/v1/admin/queue/{jobId}/cancel` — cancella job
  - `POST /api/v1/admin/queue/{jobId}/retry` — ritenta job
  - `DELETE /api/v1/admin/queue/{jobId}` — rimuovi dalla coda
  - `PUT /api/v1/admin/queue/reorder` — riordina coda
  - `POST /api/v1/admin/queue/reindex-failed` — ritenta tutti i falliti
  - `GET /api/v1/admin/pipeline/health` — health check pipeline
  - `GET /api/v1/admin/pipeline/metrics` — metriche pipeline
  - `GET /api/v1/admin/queue/eta` — ETA per tutti i job in coda (batch, usato da `useQueueETA`)
  - `POST /api/v1/admin/queue/pause` — pausa coda
  - `POST /api/v1/admin/queue/resume` — riprendi coda
- Se mancanti, creare `AdminQueueEndpoints.cs` con i routing necessari
- Tutti gli endpoint richiedono `RequireAdminSessionFilter`

---

## Frontend — Pagina Unificata

### Route

`/admin/knowledge-base/rag-pipeline/`

Questa pagina sostituisce la navigazione frammentata tra Queue, Processing, Embedding, Upload.

### Layout Generale

**Layout ibrido B+C**: Dashboard con pannelli + sezioni collassabili.

4 tab:
1. **Upload & Coda** (default) — operazioni quotidiane
2. **Storico & Analytics** — dati storici e distribuzione
3. **Embedding Service** — health e metriche modello
4. **Configurazione** — parametri pipeline (read-only)

### Tab 1: Upload & Coda

#### Stats Bar (top, sempre visibile)

Barra orizzontale con 5 celle:

| Cella | Dato | Colore | Fonte |
|-------|------|--------|-------|
| In coda | Count job status=Queued | Orange #f97316 | `useQueueSSE` |
| Processing | Count job status=Processing | Blue #3b82f6 | `useQueueSSE` |
| Completati 24h | Count completati ultime 24h | Green #22c55e | Query on mount |
| Falliti | Count job status=Failed | Red #ef4444 | `useQueueSSE` |
| ETA svuotamento | Tempo stimato per svuotare la coda | Orange bg | `useQueueETA` |

L'ETA svuotamento mostra una progress bar che rappresenta la percentuale di coda completata nella sessione corrente.

#### Upload Zone

Riuso diretto di `upload-zone.tsx` esistente:
- Drag&drop multi-file (max 20 file, 50MB ciascuno, solo PDF)
- Selezione gioco obbligatoria con search
- Toggle priorità: Normale (10) / Urgente (30)
- Upload chunked per file >10MB
- Auto-enqueue dopo upload completato
- Progress bar per file durante upload

Nessuna modifica necessaria al componente.

#### Coda Live

Griglia job con aggiornamento SSE real-time:

**Per item in stato Processing**:
- Nome file, badge "Extracting/Chunking/Embedding/Indexing"
- Step attuale (es. "2/5"), progress bar
- ETA restante per quel PDF
- Background azzurro leggero

**Per item in stato Queued**:
- Nome file, badge "In coda"
- Drag handle (⠿) per riordinare
- Checkbox per selezione multipla
- Badge priorità se Urgente
- ETA calcolata (basata su posizione in coda × tempo medio)
- Bottone "Rimuovi" (✕)

**Per item in stato Failed**:
- Nome file, badge "Fallito"
- Messaggio errore (troncato, espandibile)
- Bottone "Ritenta" se `canRetry = true`

**Sezione Completati/Falliti recenti**: Collassabile, mostra ultimi 20 completati con tempo impiegato.

**Toolbar**:
- Barra ricerca (full-text su filename)
- Filtri (status, date range) — riuso `queue-filters.tsx`
- Bulk Actions: Rimuovi selezionati, Ritenta selezionati
- Indicatore SSE live — riuso `sse-connection-indicator.tsx`

Componenti riusati: `queue-list.tsx`, `queue-item.tsx`, `queue-filters.tsx`, `bulk-actions-bar.tsx`, `sse-connection-indicator.tsx`.

Adattamento a `queue-item.tsx`: aggiungere visualizzazione ETA per elemento (dato dal nuovo hook `useQueueETA`).

#### Sidebar Destra (260px, sticky)

**Tempo medio per PDF**:
- Breakdown per step: Estrazione, Chunking, Embedding, Indexing
- Totale medio + P95
- Nota "Basato su ultimi 100 PDF"
- Fonte: `GetAllStepStatisticsAsync()`

**Stato Pipeline**:
- Health check per stage (OK/Lento/Errore con indicatore colorato)
- Fonte: `GET /api/v1/admin/pipeline/health`

**Embedding Service (mini)**:
- Modello, throughput, failure rate (3 righe)
- Fonte: `GET /api/v1/admin/embedding/info` + `metrics`

**Azioni Rapide**:
- 🔄 Ritenta tutti i falliti → `POST /api/v1/admin/queue/reindex-failed`
- 🧹 Pulisci chunk orfani → `POST /admin/pdfs/maintenance/cleanup-orphans`
- ⏸️ Pausa/Riprendi coda → `POST /api/v1/admin/queue/pause` / `POST /api/v1/admin/queue/resume` (se non esistono, crearli nel Fix 3 usando `IProcessingQueueConfigRepository`)

### Tab 2: Storico & Analytics

- Tabella PDF completati: filename, gioco, durata, data, stato — ordinabile e paginata
- Distribuzione stati: contatori per stato con percentuali — fonte: `GetPdfStatusDistributionQuery`
- Storage breakdown: spazio totale, per gioco, per tipo — fonte: `GetStorageBreakdownQuery`
- Componenti nuovi ma semplici (tabelle + cards contatori)

### Tab 3: Embedding Service

- Refactor di `embedding/page.tsx` esistente in componente standalone
- Aggiunta: Vector stats pgvector da `GET /api/v1/admin/kb/vector-stats`
- Auto-refresh 30s (già implementato nella pagina esistente)

### Tab 4: Configurazione

- Refactor di `settings/page.tsx` esistente in componente standalone
- Read-only view dei parametri pipeline
- Nessuna modifica funzionale

### Nuovo Hook: `useQueueETA`

```typescript
function useQueueETA(jobIds: string[]) {
  // Polling ogni 30s (non SSE — i dati cambiano lentamente)
  // Chiama endpoint che usa CalculateETAAsync per ogni job
  // Calcola anche ETA globale (somma ETA rimanenti)
  // Returns: { perJob: Record<string, number>, totalEta: number }
}
```

Questo hook alimenta:
- ETA per singolo elemento nella coda
- ETA svuotamento nella stats bar

### Navigazione

- Aggiungere link nella sidebar admin sotto "Knowledge Base"
- La vecchia pagina KB Hub (`/admin/knowledge-base/`) rimane come landing con link alla nuova pagina
- Le vecchie pagine separate (queue, processing, embedding) fanno redirect alla nuova pagina con il tab corretto

---

## Componenti da Creare

| Componente | Descrizione |
|-----------|-------------|
| `rag-pipeline-page.tsx` | Page component con tab layout |
| `rag-pipeline-client.tsx` | Client component principale |
| `upload-and-queue-tab.tsx` | Tab 1: layout griglia upload + coda + sidebar |
| `queue-stats-bar.tsx` (refactor) | Stats bar orizzontale con ETA |
| `queue-eta-sidebar.tsx` | Sidebar destra con metriche e azioni |
| `history-tab.tsx` | Tab 2: storico e analytics |
| `embedding-tab.tsx` | Tab 3: wrapper componente embedding |
| `config-tab.tsx` | Tab 4: wrapper componente settings |
| `useQueueETA.ts` | Hook per calcolo ETA polling |

## Componenti Riusati Senza Modifiche

- `upload-zone.tsx`
- `queue-list.tsx` + `queue-item.tsx` (modifica minima: colonna ETA)
- `queue-filters.tsx`
- `bulk-actions-bar.tsx`
- `sse-connection-indicator.tsx`
- `useQueueSSE()` hook
- `useJobSSE()` hook

---

## Criteri di Successo

1. **Upload bulk funziona**: Carico 5+ PDF → appaiono tutti in coda → vengono processati
2. **Coda real-time**: Gli stati si aggiornano via SSE senza refresh manuale
3. **ETA accurate**: Dopo 10+ PDF processati, le stime hanno un margine ≤30%
4. **Rimozione dalla coda**: Posso rimuovere singoli elementi o in bulk
5. **Pagina unica**: Tutte le operazioni RAG admin sono accessibili da una sola pagina
6. **Backend fix**: I PDF privati vengono automaticamente enqueued dopo upload

## Fuori Scope

- Modifica del pipeline di processing (estrazione, chunking, embedding)
- Nuovi endpoint backend oltre a quelli necessari per i fix
- Modifica delle pagine non-admin (upload utente, chat)
- Dashboard analytics avanzate (grafici trend, heatmap)
- Configurazione write (modifica parametri pipeline da UI)
