# Image-Table Region Grounding — leggere e localizzare le tabelle renderizzate come immagini

**Data**: 2026-08-01
**Tipo**: design — follow-up investigazione #3419
**Epic**: #3435 — "Image-table extraction & grounding" (il *Livello 2* rinviato dall'epic #3403)
**Branch previsto**: feature branch per sub-progetto (parent `main-dev`)
**Stato**: **design v1.1** — v1 (spec-panel) + **spike Fase-0 parzialmente risolti (2026-08-01)**
> Panel: Fowler/Nygard/Newman/Wiegers/Adzic/Crispin/Hightower. v1 chiude: retrievability Metà R (§2), Fase-0 spike
> (§5), IA-6 (§7), job VLM (§8), contratto SmolDocling (DC-G), golden-set (§10), NFR (§4), esempio worked (§12).
> **v1.1** registra gli esiti spike: **DC-A ✅NO** (fast dà bbox testo ma non le regioni-immagine → serve hi_res async),
> **DC-G ✅SÌ** (VLM image-native → thin endpoint `/extract-image` → Opzione B viabile), **DC-E ✅BLOCCATO** (il box
> staging 8GB non può ospitare SmolDocling — 172 MiB liberi, 2.6 GiB già in swap → benchmark off-box + prerequisito
> infra). Restano DC-C (dipende dal benchmark off-box), DC-B, DC-F.

> **Origine**: investigazione di attivazione di DC-2 #3419 su staging (2026-08-01). Il wiring hi_res-per-tabelle
> è corretto e mergiato ma **inerte sul corpus reale**: né `fast` né `hi_res` producono elementi `Table`,
> perché le tabelle dei rulebook board-game sono **grafica/immagini**, non griglie di testo. Questo spec
> definisce la feature vera necessaria a rendere quelle tabelle leggibili dall'agente e localizzabili nel PDF.

---

## 1. Diagnosi (verificata sul corpus staging)

Investigazione diretta (SSH staging, DB `meepleai_staging`, servizio unstructured):

| Evidenza | Dato |
|---|---|
| `fast` su **tutti** i 52 PDF Ready | **0** elementi `ElementType="Table"` in `structured_elements_json` (solo Title/NarrativeText/UncategorizedText/Header/ListItem/Footer) |
| `TableCount` del table-extractor Docnet dedicato | **0** su tutto il corpus |
| `hi_res` su `agricola` (rulebook table-heavy), `infer_table_structure=True` già attivo | **0** `Table`; emette `Image`(92) + `FigureCaption`(13), **con bbox** |
| `pdfimages` su `agricola` | 10-15+ immagini raster/pagina → rulebook graphic-heavy, tabelle disegnate con icone |
| Tempo `hi_res` | ~185-223s/rulebook > **120s** timeout client Unstructured dell'API (#3424) |
| SmolDocling (Stage 2, VLM image-based, rileva `has_tables`) | **mai triggerato**: Stage 1 fast "riesce" (quality 0.88 sul testo narrativo); e **non deployato** su staging |

**Root cause**: il layout model di Unstructured (e il table-extractor Docnet) rilevano `Table` solo su **griglie di
testo estraibili dal text-layer**. Le tabelle dei rulebook (punteggi, azioni, conversioni risorse) sono **rese come
grafica raster/vettoriale con icone** → classificate `Image`/`FigureCaption`, il loro *contenuto* non entra mai nel
corpus RAG. L'agente non può rispondere su quelle tabelle, e la citazione non può puntare alla regione.

**Relazione con l'epic #3403** (RAG citation region grounding): quell'epic ha esplicitamente messo **fuori scope**
(non-goal §88) «riscrivere l'estrazione tabelle in HTML/celle → è il **Livello 2 separato**». Questo spec **è** quel
Livello 2. #3403 ha già costruito l'infrastruttura di *grounding a regione* (vedi §3) su cui questa feature si innesta.

---

## 2. Insight strategico — due metà, ma NON del tutto separabili

Il problema "tabelle-immagine" ha **due componenti**, con costo e valore diversi:

- **Metà R (Region)** — *localizzare* la tabella nel PDF. hi_res emette già `Image`/`FigureCaption` **con bbox**;
  l'infrastruttura di grounding (`bounding_boxes_json` → `regions[]` → overlay FE) **esiste già** dall'epic #3403.
- **Metà C (Content)** — *leggere* il contenuto della tabella (celle/valori) e indicizzarlo, così l'agente può
  **rispondere** su di esso. Richiede estrazione VLM image-based (SmolDocling).

> **⚠️ Correzione v1 (Fowler, CRITICAL)**: una *chunk regione-immagine* con solo bbox e **testo vuoto** NON ha
> embedding → il RAG **non la recupera** mai. Quindi Metà R non è retrievabile *da sola*. Due modi per darle valore:
>
> - **Legame per-pagina (linkage)** — associare le `Image`/`FigureCaption`-region ai **chunk di testo della stessa
>   pagina**; quando uno di quei chunk viene citato, le regioni-immagine della sua pagina entrano in `regions[]`.
>   Dà **grounding visivo indiretto** (l'utente vede la regione-tabella vicino al testo citato) **senza** contenuto.
>   È euristico (prossimità di pagina, non semantica) — vedi spike **DC-F**.
> - **Contenuto retrievabile (Metà C)** — solo estraendo il *testo* della tabella (SmolDocling) si ottiene una chunk
>   con embedding, quindi **retrieval diretto** e answerability. La bbox della regione viaggia sulla stessa chunk.
>
> **Conseguenza**: la sequenza "A poi B" di v0 è rivista. Metà R "pura" (Opzione A) dà solo grounding indiretto
> per-pagina e **dipende dal linkage DC-F**; l'answerability (il valore-utente pieno) **richiede Metà C**. A e B
> non sono valore-indipendenti come affermava v0.

---

## 3. Materie prime (cosa esiste già)

| Building block | Fornisce | Limite |
|---|---|---|
| **Unstructured `hi_res`** (`infer_table_structure=True`) | Elementi `Image`/`FigureCaption` **con bbox** [0,1] (SP-B #3406) → la *regione* delle tabelle-immagine | Lento (~185-223s/rulebook) > timeout API 120s; **nessun contenuto** delle immagini |
| **SmolDocling** (`apps/smoldocling-service`, VLM 256M) | Rende le pagine a **immagine** (300 DPI) → VLM → **DocTags + Markdown** con **tabelle** (`has_tables`); legge le tabelle-immagine perché *vede* la pagina | Fallback Stage 2 (mai triggerato qui); **non deployato** su staging; CPU-only ~3-5s/pagina; input = **PDF** (non crop-immagine, vedi DC-G); DocTags location da confermare (DC-C) |
| **Region-grounding infra #3403** | `text_chunks.bounding_boxes_json jsonb` [0,1]; `regions[]` nei DTO citazione (gated `Full`); `PdfBBoxOverlay` FE; degradazione `regions=null` | bbox solo dal ramo Unstructured |
| **`ReindexDocumentCommand`** | Re-estrazione per-documento mirata (reset→Pending→enqueue) senza toccare `StructuredElementsJson` | — |
| **`IndexerVersionRegistry`** | Versionamento pipeline + selettore re-index big-bang | il contenuto-tabella nuovo → richiede un **bump** (vedi SP7) |

---

## 4. Obiettivo e requisiti

**Obiettivo**: per un PDF le cui tabelle sono immagini, (R) evidenziare la **regione** della tabella nella citazione
e (C) rendere il **contenuto** della tabella retrievabile dall'agente, con degradazione pulita.

Requisiti funzionali (SMART):

- **R1 (Region)** — Ramo Unstructured hi_res: le regioni `Image`/`FigureCaption` sono persistite come
  `bounding_boxes_json` e sopravvivono al FE come `regions[]`; il viewer disegna la regione, via il linkage per-pagina
  (DC-F) o via la chunk-contenuto (Metà C).
- **R2 (Content)** — Il contenuto di ogni tabella-immagine è estratto (markdown/`text_as_html`) e indicizzato come
  chunk retrievabile con la bbox della sua regione. **Soglia**: ≥1 chunk-tabella con testo non vuoto per ogni regione
  confermata tabella (§10 golden-set definisce "confermata").
- **R3 (Answerability)** — Su una domanda del **golden-set** tabellare (§10), la risposta è corretta con citazione che
  apre la pagina e evidenzia la regione. **Target misurabile**: ≥80% di accuratezza sul golden-set (non "corretta").
- **R4 (Copyright)** — Contenuto-tabella verbatim + `regions[]` geometriche esposti solo `CopyrightTier=Full`
  (ADR-059/#447, coerenza con #3403 DA-4).
- **R5 (Degradazione)** — PDF senza tabelle-immagine, o dove l'estrazione VLM fallisce/timeout → nessun errore;
  comportamento invariato (`regions=null`, nessun chunk-tabella).

Requisiti non-funzionali (NFR — Wiegers/Hightower):

- **NFR1 (Isolamento costo)** — L'estrazione VLM gira **solo** sui PDF/pagine con regioni-immagine candidate
  (router §7 SP2), **mai** su PDF text-only. R6 quantificato: latenza di ingest dei PDF text-only **invariata ±5%**
  vs baseline pre-feature.
- **NFR2 (Throughput/latenza job VLM)** — Il job VLM è **asincrono e batch** (§8): non blocca il `Ready` del
  documento. Target: ≤ N pagine-VLM/PDF processate entro il ciclo batch (N, deadline job, e concurrency da fissare
  post-benchmark DC-E).
- **NFR3 (Qualità estrazione)** — Soglia di accettazione del contenuto VLM misurata sul golden-set (§10); sotto
  soglia → il chunk-tabella non viene indicizzato (evita di iniettare valori allucinati nel corpus).
- **NFR4 (Osservabilità)** — Metriche per job VLM: success rate, latenza, pagine/tabelle estratte, OOM/timeout count
  (§8).

Non-goal:

- Rilevamento sub-cella o semantica avanzata (header multi-livello); real-time; sostituire Unstructured per il testo
  narrativo; GPU obbligatoria (deve girare CPU-only, accettando la lentezza batch).

---

## 5. Fase 0 — Spike bloccanti (PRIMA di scegliere Opzione A/B/C)

> **Correzione v1 (Wiegers, CRITICAL)**: la scelta A/B/C dipende da nodi tecnici irrisolti. Vanno chiusi da spike
> mirati **prima** di stimare l'effort e committare un'opzione. Nessun SP di implementazione parte prima.

| Spike | Domanda | Esito (2026-08-01) |
|---|---|---|
| **DC-A** | `fast` emette bbox `Image`/`FigureCaption` a costo basso, o solo hi_res (>120s)? | ✅ **NO**. `fast` è coordinate-aware (454/454 elementi con bbox su agricola) **ma non emette `Image`/`FigureCaption`** (solo testo) — il layout model è esclusivo di hi_res. La regione precisa richiede hi_res async; niente scorciatoia region-only economica. fast dà solo bbox del testo circostante (grounding debole, già coperto da #3403). |
| **DC-G** *(nuovo v1)* | SmolDocling accetta **input immagine (crop)** o solo **PDF**? | ✅ **SÌ, con aggiunta minima**. La VLM è image-native (`process_page(PageImage)` → `processor(images=…)`); l'endpoint pubblico è PDF-only per convenzione. Opzione B (crop) = thin endpoint `POST /extract-image` che wrappa il crop in `PageImage` e chiama il `process_page` esistente. Zero nuovo servizio. |
| **DC-E** *(riformulato v1.1)* | ~~Deploy SmolDocling su staging 8GB~~ → **Su quale infra girano il benchmark e la Metà C?** | ✅ **BLOCCATO su staging**. Misurato: box 8GB con **172 MiB RAM liberi** + **2.6/4.0 GiB swap già usato** (`unstructured` 2.3 GiB, `embedding` 888 MiB/87%). SmolDocling vuole 2-3 GiB → **headroom negativo** → OOM-kill di un container critico (probabile vittima: `unstructured`) o thrashing. `mem_limit` non risolve (non c'è RAM fisica). **DC-E diventa: benchmark OFF-BOX (locale/VM throwaway) + decisione infra (resize a 16 GiB o nodo dedicato) prima di qualsiasi deploy Metà C.** |
| **DC-C** | SmolDocling DocTags espone **location** (`<loc_*>`) usabili come bbox? | ⏳ Aperto — dipende dal **benchmark off-box** (DC-E): il `process_page` cattura `doctags_text` raw; i DocTags Docling tipicamente hanno `<loc_*>`, ma va confermato su modello running. Decide Opzione B (regione da hi_res) vs C (regione da DocTags). |
| **DC-F** *(nuovo v1)* | Legame citazione-testo → regione-immagine: prossimità pagina basta? | ⏳ Aperto — valore atteso **debole** dato DC-A (fast dà solo bbox testo → highlight del testo vicino, non della tabella). |
| **DC-B** | Router "PDF table-heavy": euristica vs flag manuale | ⏳ Aperto (analisi leggera, no deploy) |

**Output di Fase 0**: una decisione documentata A/B/C con effort stimato. Solo allora partono gli SP §7.

> **⚠️ Prerequisito infra (v1.1, Hightower/Nygard, CRITICAL)**: la Metà C (SmolDocling VLM) **non è deployabile
> sull'infra staging attuale** senza resize/nodo dedicato — il box 8GB è già saturo (172 MiB liberi, swap 65%).
> Questo è un rischio di prima classe: la Metà C ha un **costo infra** non azzerabile. Il benchmark DC-C/DC-E va
> eseguito **fuori dal box condiviso** per non OOM-killare i servizi live.

---

## 6. Opzioni architetturali (scelta POST Fase 0)

Non mutuamente esclusive; la scelta dipende dagli esiti §5.

### Opzione A — Region-only via linkage per-pagina (Metà R)
hi_res (o fast se DC-A) → bbox `Image`/`FigureCaption` → `bounding_boxes_json` → linkage per-pagina (DC-F) → il FE
mostra la regione-tabella quando un chunk-testo della stessa pagina è citato. **Nessun contenuto.**
- **Pro**: costo S; riusa l'infra #3403; valore di verificabilità visiva.
- **Contro**: **grounding indiretto** (il legame è per-pagina, non "questa citazione *è* la tabella"); dipende da DC-F
  (rumore da illustrazioni non-tabella); l'agente **non legge** la tabella. **Non retrievabile da sola** (§2).

### Opzione B — Hybrid hi_res-region + SmolDocling-crop (Metà R+C) — *raccomandata post-Fase-0 se DC-C=no*
hi_res dà le **regioni**; si **ritagliano** dalla pagina renderizzata e si passano a SmolDocling per il **contenuto**;
si fonde contenuto+regione in una chunk-tabella retrievabile.
- **Pro**: regioni precise (hi_res) + contenuto solo sulle tabelle (crop → VLM su poche immagini); grounding esatto.
- **Contro**: orchestrazione (render, crop, N chiamate VLM, correlazione); **dipende da DC-G** (SmolDocling accetta
  crop?); assunzione 1:tabella:regione (IA-6).

### Opzione C — SmolDocling-primary per PDF table-heavy (Metà R+C) — *raccomandata post-Fase-0 se DC-C=sì*
Instrada i PDF table-heavy a SmolDocling primario: il VLM legge la pagina-immagine → DocTags con tabelle **e**
location (se DC-C=sì) → contenuto+regione in un colpo.
- **Pro**: un solo tool image-based; DocTags dà struttura **e** posizione.
- **Contro**: SmolDocling lento CPU (minuti/rulebook), quality più bassa (0.70-0.78) → **rischio di degradare il testo
  narrativo** vs Unstructured; da deployare (DC-E); router affidabile (DC-B).

**Raccomandazione (aggiornata v1.1 con esiti spike)**:
- **Opzione A** indebolita: DC-A=no → nessuna regione-immagine economica via `fast`; A darebbe solo grounding del
  testo circostante (già in #3403). Quick-win marginale.
- **Opzione B** confermata **fattibile** (DC-G=sì, thin endpoint crop) — **ma** richiede la Metà C, quindi
  **SmolDocling deployato**, che è **bloccato dall'infra** (DC-E): serve resize/nodo dedicato.
- **Opzione C** resta contingente a DC-C (DocTags location), risolvibile solo col benchmark off-box.
- **Percorso raccomandato v1.1**: (1) **benchmark off-box** di SmolDocling → chiude DC-C + numeri qualità/latenza/RAM;
  (2) **decisione infra** (resize 16 GiB o nodo VLM dedicato); (3) poi scegliere B (DC-C=no) vs C (DC-C=sì). Senza il
  passo infra, la Metà C non parte — è il gate reale, non la scelta A/B/C.

---

## 7. Decisioni cardine (proposte) + da confermare

| # | Decisione | Proposta | Motivazione |
|---|---|---|---|
| **IA-1** | Segnale "tabella" | `ElementType ∈ {Image, FigureCaption}` da hi_res **candidato**, confermato dal contenuto VLM (`has_tables`/struttura) in Metà C | fast/Docnet non danno segnale; hi_res dà la regione ma non distingue tabella da illustrazione |
| **IA-2** | Grounding regione | Riusa `bounding_boxes_json`+`regions[]`+`PdfBBoxOverlay` (#3403), estendendo il capture a `Image`/`FigureCaption` | zero nuova infra FE/DTO |
| **IA-3** | Estrazione contenuto | **SmolDocling** (VLM image-based) | già in repo, legge immagini, output strutturato, Apache-2.0 |
| **IA-4** | Copyright | Contenuto-tabella + regioni gated `CopyrightTier=Full` | highlight/estratto verbatim = rischio leak |
| **IA-5** | Costo VLM isolato | VLM solo su PDF con regioni candidate (SP2), non su tutto il corpus (NFR1) | nessun peggioramento su PDF text-only |
| **IA-6** *(nuovo v1)* | Cardinalità tabella↔regione | **MVP = 1:1** (una tabella per regione `Image`); N:M (tabella multi-regione o regione multi-tabella) **fuori MVP**, tracciato | evita correlazione ambigua contenuto↔bbox; l'assunzione era implicita in v0 §8 |

**Da confermare (DC)** — vedi Fase 0 §5 per DC-A/DC-C/DC-E/DC-F/DC-G. Inoltre:
- **DC-B** — Router "PDF table-heavy": euristica (densità immagini via `pdfimages`, o `has_tables` da un pass VLM
  campione) vs flag manuale per-gioco.
- **DC-D** — Timeout hi_res vs estrazione tabella-immagine come **job asincrono post-Ready** (§8). *Raccomandato: job
  asincrono* — Metà C è batch, non deve bloccare il Ready.

---

## 8. Design operativo del job VLM di estrazione tabelle (Nygard/Hightower)

> **Correzione v1**: l'estrazione VLM su box 8GB CPU-only è un rischio operativo di prima classe (OOM già visto nel
> bulk SP3 con cap 1G). Il job va progettato prima di stimare l'effort.

- **Asincrono e post-Ready**: il documento raggiunge `Ready` con l'estrazione testo Unstructured; il job VLM gira
  **dopo**, come step di arricchimento separato (nuovo Quartz job o coda dedicata) — non estende il timeout della
  pipeline sincrona.
- **Idempotenza per-documento**: il job è ri-eseguibile senza duplicare chunk-tabella (chiave: `pdf_id` + regione
  bbox); una nuova esecuzione sostituisce i chunk-tabella esistenti del documento.
- **Retry con backoff + circuit-breaker** sul servizio VLM: N tentativi, poi il documento è marcato
  `table-extraction: failed` (degradazione R5, non blocca nulla).
- **Concurrency=1 + `mem_limit`** sul container SmolDocling; il job processa un documento alla volta; ripresa da
  checkpoint per-pagina (su crash a metà, riprende dalla pagina non ancora estratta).
- **Osservabilità** (NFR4): metriche Prometheus `meepleai_table_vlm_{success,failed,oom,timeout}_total`, latenza,
  `tables_extracted_total`; alert su failed-ratio.
- **Gate qualità** (NFR3): se l'output VLM per una regione è sotto soglia (no struttura tabellare rilevata / confidence
  bassa) → **scartare** il chunk (non iniettare valori allucinati); log come skip, non errore.

---

## 9. Decomposizione in sub-progetti (POST Fase 0)

| SP | Cosa | Metà | Costo | Dipende da |
|----|------|------|-------|-----------|
| **SP0 — Fase 0 spike** | ~~DC-A ✅ DC-G ✅ DC-E ✅(blocco infra)~~; resta **benchmark off-box** (chiude DC-C + numeri) + decisione infra | — | S | — |
| **SP1 — Image-region capture** | Estendere il capture bbox #3403 a `Image`/`FigureCaption`; linkage per-pagina (DC-F); FE disegna la regione | R | S | #3403 SP-B/SP-D; DC-A/DC-F |
| **SP2 — Table-region router** | Euristica DC-B per marcare PDF/pagine con tabelle-immagine candidate | R+C | S | SP1 |
| **SP3 — SmolDocling deploy + extract** | Deploy SmolDocling su **infra adeguata** (NON il box staging 8GB — resize/nodo dedicato, DC-E); thin endpoint `POST /extract-image` per i crop (DC-G ✅) | C | L | SP2, DC-E (infra), DC-G |
| **SP4 — VLM enrichment job** | Job asincrono §8 (idempotenza/retry/checkpoint/observability/gate qualità) | C | L | SP3 |
| **SP5 — Table-chunk indexing** | Contenuto-tabella come chunk retrievabile con `bounding_boxes_json`=regione; gating copyright | C | M | SP4 |
| **SP6 — Answerability + citazione tabella** | Citazione chunk-tabella apre pagina + evidenzia regione; test golden-set (§10) | C | M | SP5 |
| **SP7 — Rollout/re-extract** | **Bump `IndexerVersionRegistry`** (il corpus ora ha contenuto-tabella) → re-estrazione mirata (`ReindexDocumentCommand`) dei PDF table-heavy per-env + runbook | R+C | M | SP5 deployato |

SP0 è bloccante. SP1-SP2 (Metà R) spedibili dopo SP0 se DC-A/DC-F ok. SP3-SP6 (Metà C) = il grosso, con VLM mockato.

---

## 10. Strategia di test — golden-set (Crispin/Adzic)

> **Correzione v1**: AC2/AC3 non sono testabili senza ground-truth; il VLM può allucinare valori tabellari.

- **Golden-set**: 5-10 tabelle note prese da 3-4 rulebook del corpus (es. agricola scoring, ark-nova azioni), con:
  (a) la **regione attesa** (pagina + bbox approssimativa), (b) il **contenuto atteso** (celle/valori chiave),
  (c) 2-3 **coppie Q/A** per tabella («quanti punti vale il campo X?» → valore atteso).
- **Metriche**: precision/recall del rilevamento-regione; accuratezza cella-per-valore del contenuto estratto;
  accuratezza risposta sulle Q/A (target R3 ≥80%).
- **False-positive test**: illustrazioni decorative (non-tabelle) NON devono generare chunk-tabella (IA-1 gate).
- **Edge case da coprire**: tabella ruotata; tabella multi-pagina (fuori MVP IA-6, ma il test asserisce degradazione
  pulita); pagina ibrida testo+immagine; regione con più tabelle (fuori MVP, degradazione).

---

## 11. Rischi e mitigazioni

| Rischio | Mitigazione |
|---|---|
| **hi_res troppo lento** (>120s) per il path sincrono | job VLM **asincrono post-Ready** (§8); DC-A (fast dà bbox `Image`?) |
| **Metà R non retrievabile da sola** | §2: linkage per-pagina (DC-F) per grounding indiretto; answerability piena via Metà C |
| **SmolDocling degrada il testo narrativo** (Opzione C) | Preferire Opzione B (VLM solo sui crop; Unstructured resta primario) |
| **False positive** (illustrazioni marcate tabelle) | IA-1 due segnali + gate qualità §8 (NFR3); false-positive test §10 |
| **N:M tabella↔regione** | IA-6: MVP 1:1; N:M fuori scope + test di degradazione |
| **Correlazione crop↔contenuto** (Opzione B) | bbox della regione definisce il crop → contenuto e regione condividono la geometria (valido sotto IA-6 1:1) |
| **Contratto SmolDocling** (PDF vs crop) | DC-G in Fase 0 prima di committare Opzione B |
| **Metà C non deployabile su infra staging attuale** *(v1.1, CRITICAL)* | Misurato: box 8GB con 172 MiB liberi + swap 65%. SmolDocling (2-3 GiB) → headroom negativo → OOM-kill di `unstructured`/critici. `mem_limit` NON basta (manca RAM fisica). **Gate**: benchmark off-box + resize (16 GiB) o nodo VLM dedicato prima di deployare la Metà C |
| **OOM durante benchmark** | Eseguire il benchmark **fuori dal box condiviso** (locale/VM throwaway); mai avviare SmolDocling sul box saturo |
| **Copyright leak** (contenuto verbatim) | IA-4 gating `Full`; test `regions=null`/no-content su Protected |
| **Costo VLM sul corpus** | IA-5/NFR1: VLM solo su PDF con regioni candidate; batch, non ingest sincrono |
| **Drift corpus post-feature** | SP7: bump `IndexerVersion` + re-extract mirato per-env |

---

## 12. Esempio worked (Given/When/Then — Adzic)

```
Scenario: agricola scoring table (immagine) → answerability + grounding

Given un PDF "agricola-revised" ingerito, la cui tabella-punteggi a pagina 4 è renderizzata come immagine
  And hi_res ha prodotto una regione FigureCaption/Image con bbox {page:4, x:0.10, y:0.55, w:0.80, h:0.30}
  And DC-C=no → Opzione B (crop + SmolDocling)

When il job VLM asincrono (§8) ritaglia quella regione e la passa a SmolDocling
Then SmolDocling ritorna il contenuto-tabella (markdown: righe "campo arato → 1pt", "pascolo → 1pt", …)
  And viene indicizzata una chunk-tabella con text=<markdown> e bounding_boxes_json=[{page:4,...}]
  And IndexerVersion è bumpato (SP7)

When l'utente (tier Full) chiede "quanti punti vale un campo arato in Agricola?"
Then il retrieval trova la chunk-tabella
  And la risposta è "1 punto" (∈ golden-set, target ≥80% accuratezza — R3)
  And la citazione apre pagina 4 con la regione {0.10,0.55,0.80,0.30} evidenziata (PdfBBoxOverlay)

When l'utente è tier Protected
Then regions=null e nessun contenuto-tabella verbatim (R4/IA-4)
```

---

## 13. Domande aperte per lo spec-panel / owner

1. **Priorità Metà R vs Metà C**: dato che Metà R "pura" dà solo grounding **indiretto** per-pagina (§2), vale
   spedirla prima, o si va diretti a Metà C (answerability)?
2. **Fase 0 §5** va eseguita come primo SP (SP0) — i suoi 5 spike decidono A/B/C e l'effort reale.
3. **Scope corpus**: solo tabelle-immagine (board-game) o anche tabelle-testo (dove #3419 avrebbe senso)? Il router
   SP2 può gestire entrambi.
4. **Rapporto con #3419**: tenere latente, deprecare, o **assorbire nel router SP2** (il router decide fast/hi_res/VLM
   per-documento — #3419 diventa il ramo "hi_res per tabelle-testo").

---

## 14. Riferimenti

- Investigazione #3419 su staging (2026-08-01): fast/hi_res 0 `Table`, tabelle=immagini, hi_res >120s, SmolDocling non triggerato.
- Epic #3403 — RAG citation region grounding (`docs/superpowers/specs/2026-07-30-rag-citation-region-grounding-design.md`),
  in particolare non-goal §88 ("Livello 2 separato") + infra `bounding_boxes_json`/`regions[]`/`PdfBBoxOverlay` + DA-4 copyright.
- `apps/smoldocling-service/` (VLM image-based, DocTags/Markdown, `has_tables`), Stage 2 pipeline (BGAI-005 #945).
- `apps/unstructured-service/src/infrastructure/unstructured_adapter.py:41-47` (`infer_table_structure=True`, hi_res).
- ADR-059 (copyright posture), #447 (copyright leak guard), ADR-060 (persistence), `ReindexDocumentCommand`/`IndexerVersionRegistry` (re-index).
- **Spec-panel review v1** (2026-08-01): Fowler/Nygard/Newman/Wiegers/Adzic/Crispin/Hightower — retrievability Metà R, Fase-0 spike, IA-6, job VLM, golden-set, NFR.
