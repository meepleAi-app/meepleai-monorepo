# MeepleAI — Report Tecnico Integrato & Analisi Architetturale

**Data:** Ottobre 2026  
**Autore:** MeepleAI Engineering & R&D Team  
**Stato:** Documento di Sintesi & Linee Guida Operative  

---

## 1. Executive Summary & Modello di Business

MeepleAI è un assistente intelligente multimodale progettato per identificare giochi da tavolo tramite scansione fotografica della scatola ed erogare regole, chiarimenti e flussi di gioco in modalità progressiva (*Setup, Struttura del Turno, Casi Limite, Punteggio Finale*).

### Vincoli Economici Fondamentali
* **Modello di ricavo a consumo:** Vendita di crediti utente con una tariffa compresa tra **0,70 € e 0,99 € per scansione/sessione**.
* **Requisito di marginalità economica:** Il costo computazionale marginale per singola sessione deve essere contenuto a pochi centesimi di euro. L'adozione non presidiata di modelli multimodali generativi (VLM) o database commerciali esterni comprometterebbe la sostenibilità dell'intero modello.
* **Target di latenza:** Time-To-First-Token (TTFT) inferiore a **200 ms** per le risposte testuali e riconoscimento della scatola in meno di **50 ms**.

---

## 2. Stabilizzazione Operativa del Monorepo (.NET 9 + Next.js 16 + Python)

Il monorepo di MeepleAI presenta una scala considerevole (20 bounded context, oltre 1.380 endpoint HTTP e 220 pagine frontend). La stabilizzazione operativa richiede interventi mirati sul debito tecnico:

### 2.1 Riprogettazione UX: dal Modello "RuleSpec" all'Approccio "Task-Based"
* **Superamento del debito RuleSpec:** Eliminare l'esposizione di dettagli implementativi, gerarchie di dati grezze e versioni di specifica (*RuleSpec*).
* **I 4 Task del Giocatore al Tavolo:**
  1. **Setup & Preparazione:** Checklist interattiva, configurazione componenti in base al numero di giocatori e poteri asimmetrici.
  2. **Flusso del Turno (Turn Structure):** Azioni consentite, sequenza delle fasi, costi e vincoli immediati sintetizzati in diagrammi rapidi.
  3. **Risoluzione Dubbi & Eccezioni (Edge Cases):** Ricerca mirata di interazioni conflittuali, FAQ ed errata con evidenziazione chiara delle deroghe alle regole base.
  4. **Fine Partita & Punteggio (Scoring):** Calcolatore guidato dei punti vittoria e spareggi.
* **Principi Table-First & A11y:** Interfaccia ottimizzata per consultazione mobile/tablet su tavolo da gioco con una sola mano (aree di tocco ampie, contrasti elevati, gestione non intrusiva dei token streaming su screen reader tramite `aria-live`).

### 2.2 Consolidamento del Design System e Risoluzione del Drift
* **Fonte Canonica dei Token:** Riferimento esclusivo al file `apps/web/src/styles/design-tokens-canonical.css`. Rimozione di stili arbitrari o hardcoded.
* **Standardizzazione MeepleCard:** Unificazione delle oltre 200 pagine applicative attorno alle varianti standardizzate della famiglia di componenti `MeepleCard`.
* **Tracciamento:** Bonifica guidata tramite il report di audit *MeepleAI — Dashboard & Design System Consolidation* e il registro operativo *MeepleAI — UI/UX Cleanup Control Board*.

### 2.3 Context Ownership e Test Real-Stack (Playwright L1/L2/L3)
* **Delimitazione della Context Ownership:** Confini netti tra la persistenza dello stato nel backend .NET 9 e l'elaborazione nei microservizi Python, prevenendo sfasamenti temporali della cronologia o perdita di memoria di gioco.
* **Verifica Real-Stack:** Suite di test E2E con Playwright (`apps/web/playwright.audit.config.ts`) su stack reale (.NET 9, Next.js 16, Python, PostgreSQL, Redis) per validare i percorsi utente completi secondo il *Full Feature Audit*.
* **Bonifica Segreti:** Dismissione dello storage legacy `MeepleAi.ConfigKey` su Google Drive, rotazione immediata di tutte le credenziali e migrazione su secret manager protetti.

---

## 3. Architettura dell'Infrastruttura di Sistema

```
                      ┌──────────────────────────────────────┐
                      │        Client Web / Mobile           │
                      │       (Next.js 16 / React 19)        │
                      └──────────────────┬───────────────────┘
                                         │ HTTPS / SSE
                                         ▼
                      ┌──────────────────────────────────────┐
                      │       Nginx Ingress / HTTP/2         │
                      └──────────┬────────────────┬──────────┘
                                 │                │
            ┌────────────────────▼───┐        ┌───▼────────────────────┐
            │   Core API (.NET 9)    │        │ AI Engine (FastAPI)    │
            │  - Auth & Dominio      │        │ - Ingestion & OCR      │
            │  - Context Session     │        │ - Vector & RAG Router  │
            └────────────┬───────────┘        └───┬────────────────────┘
                         │                        │
         ┌───────────────┴────────────────────────┴───────────────┐
         ▼                                                        ▼
┌─────────────────────────────────┐              ┌─────────────────────────────────┐
│     PostgreSQL + pgvector       │              │        Redis In-Memory          │
│ - Relazionale & Core Data       │              │ - L1 Exact Cache (Hash)         │
│ - Vettori HNSW (halfvec / SQ8)  │              │ - L2 Semantic Cache (RedisVL)   │
│ - Full-Text Search (tsvector)   │              │ - Rate Limiting & Sessioni      │
└─────────────────────────────────┘              └─────────────────────────────────┘
```

### Componenti Core:
1. **Frontend (Next.js 16 / React 19):** Gestione UI task-based, rendering server-side e consumo flussi token-by-token tramite Server-Sent Events (SSE).
2. **Backend (.NET 9):** Logica di business, orchestrazione dei bounded context, persistenza relazionale e gestione delle sessioni.
3. **Microservizi AI (Python / FastAPI):** Pipeline di embedding, ricerca vettoriale, re-ranking e parsing documentale.
4. **Database Unificato (PostgreSQL):** Unica tecnologia di persistenza per dati relazionali, vettori semantici (`pgvector`) e indici lessicali (`tsvector`).
5. **In-Memory Store (Redis):** Cache multi-livello ad altissima velocità e gestione dello stato volatile.

---

## 4. Pipeline Documentale Vision-Guided & Parent-Child Chunking

L'ingestione dei regolamenti dei giochi da tavolo affronta la complessità di diagrammi visivi, tabelle asimmetriche e icone di gioco.

### 4.1 Parser Multimodali per l'Ingestion Offline
* **Docling (IBM) & Marker (Surya OCR):** Strumenti di layout analysis per convertire i PDF dei regolamenti in alberi strutturati Markdown/JSON, preservando didascalie e relazioni tra testo e immagini di setup.
* **Parent-Child Chunking Tipografico:** Tramite **PyMuPDF**, l'algoritmo estrae metadati tipografici (font size, peso grassetto, bounding box) e applica soglie *calibrated mean-offset* per identificare automaticamente la gerarchia delle sezioni senza overhead di GPU a runtime:
  * **Child Chunks (100–250 token):** Frammenti brevi e focalizzati sulla singola sotto-regola, indicizzati vettorialmente per garantire la massima accuratezza nel matching semantico.
  * **Parent Chunks (800–1.500 token):** Intere sezioni o capitoli del regolamento (es. "Fase di Movimento"), recuperati per ID per fornire all'LLM il contesto esauriente comprensivo di eccezioni e note.
* **Architettura Multi-Collection:** Separazione delle collezioni vettoriali tra *Regole Base* e *FAQ / Errata / Espansioni*, garantendo quote fisse di token per impedire che documenti secondari saturino il contesto del modello.

---

## 5. Retrieval Ibrido & Re-Ranking a Due Stadi

```
                         Query Utente / Domanda
                                   │
                 ┌─────────────────┴─────────────────┐
                 ▼                                   ▼
        Dense Vector Search                  Sparse Lexical Search
       (pgvector HNSW / SQ8)                (PostgreSQL tsvector)
                 │                                   │
                 └─────────────────┬─────────────────┘
                                   ▼
                     Reciprocal Rank Fusion (RRF)
                               (k = 60)
                                   ▼
                          Top 50 Candidati
                                   │
                                   ▼
                      Cross-Encoder Re-Ranking
                       (BGE-Reranker-v2-m3)
                                   │
                                   ▼
                     Filtro Relevance Threshold
                                   │
                                   ▼
                       Top 3-5 Parent Chunks
                                   │
                                   ▼
                             Generazione LLM
```

### Dettagli Tecnici dei Due Stadi:
* **Stadio 1 — Retrieval Ibrido Parallelo:**
  * **Vettoriale Denso:** Ricerca per similarità coseno su grafi HNSW generati con modelli di embedding (es. BGE-M3 o SigLIP 2), compressi a 16-bit (`halfvec`) o INT8 (SQ8).
  * **Lessicale Sparso:** Corrispondenza esatta tramite `tsvector` con indice GIN basato su dizionario linguistico (`italian`).
  * **Fusione RRF:** Combinazione delle classifiche indipendenti:
    $$\text{RRF\_Score}(d) = \sum_{m \in M} \frac{1}{60 + r_m(d)}$$
    Innalza la precisione di recupero dal ~62% a oltre l'84%, eliminando le cecità semantiche sui nomi propri dei componenti.
* **Stadio 2 — Re-Ranking Cross-Encoder:**
  * Il modello Cross-Encoder (es. `BGE-Reranker-v2-m3`) valuta l'attenzione incrociata su tutti i 50 candidati estratti, applicando una soglia minima di rilevanza e passando solo i migliori **3–5 chunk** all'LLM.

---

## 6. Caching Gerarchico a Tre Livelli (L1 / L2 / L3)

Per intercettare il 40%–60% delle richieste ed eliminare il costo computazionale di inferenza per domande ripetute o parafrasate:

| Livello | Tecnologia | Meccanismo | Latenza | Hit Rate | Beneficio Economico |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **L1** | Redis Key-Value | SHA-256 Hash della query normalizzata | **< 1–5 ms** | 10%–15% | Zero costo computazionale |
| **L2** | RedisVL Semantic Cache | Similarità coseno su vettori ($\tau \ge 0,90 - 0,92$) | **8–15 ms** | 25%–40% | **-30%+ spesa token LLM**; bypass retrieval |
| **L3** | Prefix / Prompt Cache | KV Cache reuse (RadixAttention / provider API) | Sub-second | Variabile | **Sconto 40%–60% sui token di input** |

---

## 7. Serving LLM & Multi-Model Routing Dinamico

### 7.1 Ottimizzazione del Serving con vLLM
* **PagedAttention:** Frammentazione della memoria KV cache in blocchi fisici discontinui, eliminando gli sprechi di VRAM e massimizzando il throughput multi-utente concorrente.
* **Chunked Prefill:** Fissaggio dei batch di lettura contesto (es. 2.048 token) per evitare blocchi nella generazione dei token correnti.

### 7.2 Routing Intelligente dei Carichi
* **Small Language Models (SLM) Quantizzati:** Domande frequenti, spiegazione del turno, definizioni e flussi base vengono assegnati a modelli compatti quantizzati in formato INT4/AWQ (es. **Qwen-8B-AWQ** o **Llama-3.1-8B INT4**). Garantiscono TTFT < 150 ms con un costo computazionale trascurabile.
* **Vision-Language Models (VLM) Dedicati:** Il modello **Qwen2.5-VL** (servito con Tensor Parallelism su nodi GPU) viene invocato esclusivamente per l'analisi visiva complessa o l'interpretazione dello stato di gioco fotografato.
* **Fallback API Cloud:** Gateway su provider esterni attivato dinamicamente unicamente durante i picchi dei fine settimana per assorbire la coda senza sovradimensionare le istanze GPU fisse.

---

## 8. Soluzione di Image Retrieval Vettoriale per le Copertine

Per evitare l'uso sproporzionato del VLM generativo per la scansione iniziale della scatola, MeepleAI impiega una pipeline di **Image Retrieval vettoriale su pgvector**:

### 8.1 Schema DDL & Tuning Parametri su PostgreSQL

```sql
-- Estensione vettoriale
CREATE EXTENSION IF NOT EXISTS vector;

-- Catalogo delle copertine e scatole dei giochi
CREATE TABLE game_box_covers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    game_id UUID NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    edition_id UUID REFERENCES game_editions(id) ON DELETE CASCADE,
    edition_name VARCHAR(150),
    language VARCHAR(10) NOT NULL DEFAULT 'it',
    box_side VARCHAR(50) DEFAULT 'front',
    embedding halfvec(768) NOT NULL, -- Vettore visivo 16-bit
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT now()
);

-- Indice HNSW calibrato su distanza coseno
CREATE INDEX idx_game_box_covers_embedding_hnsw
    ON game_box_covers 
    USING hnsw (embedding halfvec_cosine_ops)
    WITH (m = 16, ef_construction = 128);

CREATE INDEX idx_game_box_covers_lang ON game_box_covers(language);
```

### 8.2 Logica di Scansione Runtime e Confronto Benchmark

Durante la scansione:
1. L'immagine scattata viene privata dei metadati EXIF (GDPR).
2. Viene calcolato l'embedding visivo tramite modello leggero (**SigLIP 2**).
3. Esecuzione query k-NN:
   ```sql
   SET LOCAL hnsw.ef_search = 100;
   SET LOCAL hnsw.iterative_scan = strict_order;

   SELECT game_id, edition_id, (1 - (embedding <=> $query_embedding::halfvec(768))) AS similarity_score
   FROM game_box_covers
   ORDER BY embedding <=> $query_embedding::halfvec(768)
   LIMIT 5;
   ```
4. Se `similarity_score >= 0.85`: identificazione immediata in **< 35 ms**.
5. Se `similarity_score < 0.70`: fallback eccezionale su VLM generativo.

### Confronto Prestazionale ed Economico:

| Parametro | VLM Generativo (es. Qwen2.5-VL) | Image Retrieval pgvector (SigLIP 2 + HNSW) | Guadagno |
| :--- | :--- | :--- | :--- |
| **Latenza di Scansione** | 400 – 800 ms (TTFT) | **15 – 35 ms** end-to-end | **Velocità 20x superiore** |
| **Hardware Richiesto** | 2x GPU H100 (Tensor Parallelism = 2) | **CPU standard** + PostgreSQL esistente | Nessun costo GPU dedicato |
| **Costo per Scansione** | ~0,015 € – 0,040 € | **< 0,0005 €** | **–90%+ costo marginale** |
| **Margine su 0,70–0,99 €** | Eroso significativamente | **Completamente preservato** | Modello economico sostenibile |

---

## 9. Budget di Latenza, Privacy e Trasparenza Normativa

### 9.1 Scomposizione Analitica del Budget di Latenza End-to-End
$$T_{\text{totale}} = T_{\text{embedding}} (5\text{–}10\text{ ms}) + T_{\text{retrieval}} (15\text{–}25\text{ ms}) + T_{\text{reranking}} (40\text{–}60\text{ ms}) + T_{\text{prefill}} (80\text{–}120\text{ ms}) + T_{\text{decode}} (150\text{–}300\text{ ms})$$
* **TTFT Complessivo:** **160 – 215 ms**
* **Latenza Totale:** **300 – 515 ms**

### 9.2 Sicurezza, GDPR & EU AI Act
* **Protezione Dati:** Strip automatico di metadati EXIF e geolocalizzazione dai file caricati. Cifratura a riposo (AES-256) e in transito (TLS 1.3). Hosting rigorosamente su server situati nell'Unione Europea.
* **Trasparenza (EU AI Act):** Ogni asserzione o regola generata dall'assistente deve obbligatoriamente contenere la **deep citation verificabile** con rimando al numero di pagina e al paragrafo del manuale originale, azzerando le allucinazioni e consentendo al giocatore la verifica istantanea.
* **Structured Outputs Rigidi:** Risposte incapsulate in schemi JSON validati a livello di motore con meccanismo difensivo di riparazione in caso di troncamento token.

---

## 10. Checklist Operativa di Implementazione

1. [ ] **Fase 1: Setup Dati & Ingestion**
   - Esecuzione dello script DDL per tabelle `game_box_covers` e chunking dei regolamenti su PostgreSQL.
   - Popolamento bulk degli embedding copertina con SigLIP 2 e tipo `halfvec(768)`.
   - Pipeline di parsing PDF con Marker/Docling e Parent-Child basato su metadati tipografici PyMuPDF.
2. [ ] **Fase 2: Caching & Search Engine**
   - Deploy e configurazione dell'istanza Redis con RedisVL (L2 cache con $\tau = 0.90$).
   - Implementazione della funzione di ricerca ibrida SQL (pgvector + tsvector) unificata tramite RRF ($k=60$).
   - Integrazione modello di re-ranking Cross-Encoder su FastAPI.
3. [ ] **Fase 3: Serving & UI Stabilization**
   - Setup cluster vLLM con supporto PagedAttention e Chunked Prefill per SLM quantizzati (Qwen-8B-AWQ).
   - Rifattorizzazione componenti frontend attorno ai 4 task del giocatore e token canonici CSS.
   - Esecuzione della suite E2E Playwright audit (`apps/web/playwright.audit.config.ts`) su stack reale.
   - Rotazione credenziali e rimozione definitiva di `MeepleAi.ConfigKey`.
