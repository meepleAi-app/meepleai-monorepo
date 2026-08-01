# Image-Table Region Grounding — leggere e localizzare le tabelle renderizzate come immagini

**Data**: 2026-08-01
**Tipo**: design (draft) — follow-up investigazione #3419
**Epic proposta**: "Image-table extraction & grounding" (il *Livello 2* rinviato dall'epic #3403)
**Branch previsto**: feature branch per sub-progetto (parent `main-dev`)
**Stato**: design v0 (draft) — da rivedere via `/sc:spec-panel` prima dell'implementazione

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
| SmolDocling (Stage 2, VLM image-based, rileva `has_tables`) | **mai triggerato**: Stage 1 fast "riesce" (quality 0.88 sul testo narrativo) < soglia 0.80 non scatta; e **non deployato** su staging |

**Root cause**: il layout model di Unstructured (e il table-extractor Docnet) rilevano `Table` solo su **griglie di
testo estraibili dal text-layer**. Le tabelle dei rulebook (punteggi, azioni, conversioni risorse) sono **rese come
grafica raster/vettoriale con icone** → classificate `Image`/`FigureCaption`, il loro *contenuto* non entra mai nel
corpus RAG. L'agente non può rispondere su quelle tabelle, e la citazione non può puntare alla regione (perché non
esiste un chunk-tabella).

**Relazione con l'epic #3403** (RAG citation region grounding): quell'epic ha esplicitamente messo **fuori scope**
(non-goal §88) «riscrivere l'estrazione tabelle in HTML/celle → è il **Livello 2 separato**». Questo spec **è** quel
Livello 2. #3403 ha già costruito l'infrastruttura di *grounding a regione* (vedi §3) su cui questa feature si innesta.

---

## 2. Insight strategico — due metà distinte

Il problema "tabelle-immagine" ha **due componenti indipendenti**, con costo e valore diversi:

- **Metà R (Region)** — *localizzare* la tabella nel PDF. hi_res emette già `Image`/`FigureCaption` **con bbox**;
  l'infrastruttura di grounding (`bounding_boxes_json` → `regions[]` → overlay FE) **esiste già** dall'epic #3403.
  Costo basso, valore: l'utente **vede** la regione-tabella evidenziata (verificabilità), anche senza contenuto testuale.
- **Metà C (Content)** — *leggere* il contenuto della tabella (celle/valori) e indicizzarlo, così l'agente può
  **rispondere** su di esso. Richiede estrazione VLM image-based (SmolDocling) o OCR+table-structure. Costo alto,
  valore: l'agente risponde correttamente a domande tabellari.

Come per #3403 (dove "risolvere B rende A meno grave"), qui **Metà R dà valore da sola** e va spedita per prima;
Metà C è il grosso e può maturare dopo con contratto mockabile.

---

## 3. Materie prime (cosa esiste già)

| Building block | Fornisce | Limite |
|---|---|---|
| **Unstructured `hi_res`** (`strategy=hi_res`, `infer_table_structure=True`) | Elementi `Image`/`FigureCaption` **con bbox** [0,1] normalizzate (SP-B #3406) → la *regione* delle tabelle-immagine | Lento (~185-223s/rulebook) > timeout API 120s; **nessun contenuto** delle immagini |
| **SmolDocling** (`apps/smoldocling-service`, VLM 256M) | Rende le pagine a **immagine** (300 DPI) → VLM → **DocTags + Markdown** con **tabelle** rilevate (`has_tables`); legge le tabelle-immagine perché *vede* la pagina | Fallback Stage 2 (trigger quality<0.80, non scatta qui); **non deployato** su staging; CPU-only sul box 8GB → ~3-5s/pagina; DocTags: location tokens da confermare |
| **Region-grounding infra #3403** | `text_chunks.bounding_boxes_json jsonb` [0,1] top-left; `regions[]` nei DTO citazione (gated `CopyrightTier=Full`); `PdfBBoxOverlay` FE (%-based, child di `<Page>`); degradazione `regions=null` | bbox solo dal ramo Unstructured; SmolDocling/Docnet oggi → `regions=null` |
| **`ReindexDocumentCommand`** | Re-estrazione per-documento mirata (reset→Pending→enqueue) senza toccare `StructuredElementsJson` | — |
| **`IndexerVersionRegistry`** | Versionamento pipeline + selettore re-index big-bang | — |

---

## 4. Obiettivo e requisiti

**Obiettivo**: per un PDF le cui tabelle sono renderizzate come immagini, (R) evidenziare la **regione** della tabella
nella citazione e (C) rendere il **contenuto** della tabella retrievabile dall'agente, con degradazione pulita quando
l'estrazione non è disponibile.

Requisiti (SMART):

- **R1 (Region)** — Per un PDF ramo Unstructured hi_res, le regioni `Image`/`FigureCaption` sono persistite come
  `bounding_boxes_json` e sopravvivono fino al FE come `regions[]`; il viewer disegna la regione-tabella.
- **R2 (Content)** — Il contenuto di ogni tabella-immagine è estratto (celle/valori come markdown o `text_as_html`)
  e indicizzato come chunk retrievabile, associato alla sua regione (bbox) per il grounding.
- **R3 (Answerability)** — Una domanda su un valore tabellare (es. «quanti punti vale X in Agricola?») ritorna una
  risposta corretta con citazione che apre la pagina e evidenzia la regione-tabella.
- **R4 (Copyright)** — Contenuto verbatim + regioni di tabelle esposti secondo la posture ADR-059/#447: `regions[]`
  geometriche solo `CopyrightTier=Full` (coerenza con #3403 DA-4).
- **R5 (Degradazione)** — PDF senza tabelle-immagine, o dove l'estrazione VLM fallisce/timeout → nessun errore;
  comportamento invariato rispetto a oggi (`regions=null`, nessun chunk-tabella).
- **R6 (No regressione)** — Baseline unit-test fail = 0; nessun peggioramento di latenza/costo sul path di ingest
  dei PDF senza tabelle-immagine (il costo VLM si paga **solo** dove servono tabelle).

Non-goal:

- Rilevamento di tabelle sub-cella o riconoscimento semantico avanzato (es. gerarchie di header multi-livello).
- Real-time (l'estrazione tabella-immagine è batch, in fase di ingest/re-index).
- Sostituire Unstructured come estrattore primario per il testo narrativo.
- GPU obbligatoria (deve funzionare CPU-only sul box staging, accettando la lentezza batch).

---

## 5. Opzioni architetturali

Tre percorsi, dal più economico (solo regione) al più completo (contenuto+regione). Non mutuamente esclusivi:
il MVP è A, poi si aggiunge B o C.

### Opzione A — Region-only MVP (solo Metà R) — *raccomandato come primo passo*
Cattura le bbox di `Image`/`FigureCaption` da hi_res → `bounding_boxes_json` → `regions[]` → overlay FE. Nessun
contenuto. L'utente **vede** la regione-tabella evidenziata quando una citazione (dal testo narrativo attorno alla
tabella) tocca quella zona. Riusa **quasi tutta** l'infra #3403.
- **Pro**: costo XS-S; nessun nuovo servizio; valore immediato di verificabilità.
- **Contro**: l'agente **non legge** la tabella (nessuna risposta tabellare); il legame citazione↔regione-immagine è
  euristico (una citazione testuale vicina alla tabella, non una citazione *della* tabella).
- **Nodo**: hi_res è lento (>120s timeout) → serve alzare il timeout hi_res **oppure** ottenere le bbox `Image`
  senza hi_res (fast le rileva? — **DC-A** sotto).

### Opzione B — Hybrid hi_res-region + SmolDocling-crop (Metà R+C, preciso)
hi_res dà le **regioni** immagine; si **ritagliano** quelle regioni dalla pagina renderizzata e si passano crop a
SmolDocling (VLM) per il **contenuto** (markdown/HTML della tabella); si fonde contenuto+regione in un chunk-tabella
retrievabile con bbox.
- **Pro**: regioni precise (hi_res) + contenuto solo sulle tabelle (crop → VLM veloce su poche immagini); grounding
  esatto contenuto↔regione.
- **Contro**: orchestrazione complessa (render pagina, crop per bbox, N chiamate VLM, correlazione); doppio giro
  (hi_res + SmolDocling) → latenza ingest.

### Opzione C — SmolDocling-primary per PDF table-heavy (Metà R+C, semplice ma grosso)
Instrada i PDF "graphic/table-heavy" a **SmolDocling come estrattore primario** (non fallback): il VLM legge l'intera
pagina-immagine, emette DocTags con tabelle **e** (da confermare) location tokens → contenuto+regione in un colpo.
- **Pro**: un solo tool image-based; legge nativamente le tabelle-immagine; DocTags può dare struttura **e** posizione.
- **Contro**: SmolDocling lento CPU (~3-5s/pagina → minuti/rulebook), quality più bassa (0.70-0.78) → rischio di
  peggiorare il **testo narrativo** rispetto a Unstructured; DocTags location parsing = lavoro nuovo; SmolDocling da
  **deployare** su staging (non presente). Serve un router "questo PDF ha tabelle-immagine" affidabile (**DC-B**).

**Raccomandazione**: **A adesso** (valore R con costo minimo, riusa #3403), poi **B** per il contenuto (isola il
costo VLM alle sole regioni-tabella, non degrada il testo narrativo). C solo se emergesse che B è troppo fragile
nel correlare crop↔contenuto.

---

## 6. Decisioni cardine (proposte) + da confermare

| # | Decisione | Proposta | Motivazione |
|---|---|---|---|
| **IA-1** | Segnale "questo è una tabella" | `ElementType ∈ {Image, FigureCaption}` da hi_res **come candidato**, confermato dal contenuto VLM (SmolDocling `has_tables`) in Metà C | fast/Docnet non danno segnale; hi_res dà la *regione* ma non distingue tabella da illustrazione |
| **IA-2** | Grounding regione | Riusa `bounding_boxes_json` + `regions[]` + `PdfBBoxOverlay` (#3403), estendendo il capture alle categorie `Image`/`FigureCaption` (oggi si prendono solo gli element testuali) | zero nuova infra FE/DTO |
| **IA-3** | Estrazione contenuto | **SmolDocling** (VLM image-based) sulle regioni-tabella, non OCR+table-transformer | già in repo, legge immagini, output strutturato (markdown/DocTags), Apache-2.0 |
| **IA-4** | Copyright | Contenuto-tabella + regioni gated `CopyrightTier=Full` (come #3403 DA-4) | highlight/estratto verbatim = rischio leak |
| **IA-5** | Costo VLM isolato | L'estrazione VLM gira **solo** su PDF con regioni-immagine candidate (Opzione B crop), non su tutto il corpus | R6: nessun peggioramento su PDF text-only |

**Da confermare (DC)**:
- **DC-A** — Le bbox `Image`/`FigureCaption` si ottengono **solo** con hi_res (lento, >120s), o `fast` (coordinate-aware
  post SP-B) le emette a costo basso? Se fast le dà → Opzione A senza il nodo-timeout. *(Verificare: rieseguire il
  confronto categorie fast-vs-hi_res guardando `Image`/`FigureCaption`, non solo `Table`.)*
- **DC-B** — Router "PDF table-heavy": euristica (densità immagini via `pdfimages`, o `has_tables` da un pass VLM
  campione) vs flag manuale per-gioco. Serve per non pagare il VLM ovunque.
- **DC-C** — SmolDocling DocTags espone **location** (`<loc_*>` tokens) usabili come bbox? Se sì → Opzione C dà
  contenuto+regione insieme; se no → serve Opzione B (regione da hi_res, contenuto da VLM sul crop).
- **DC-D** — Timeout: alzare il timeout client Unstructured per hi_res (>223s) **oppure** rendere l'estrazione
  tabella-immagine un **job asincrono separato** (non nel path di ingest sincrono). Raccomandato: job asincrono
  (Metà C è batch, non deve bloccare il Ready del documento).
- **DC-E** — Deploy SmolDocling su staging (profilo compose + risorse sul box 8GB CPU-only): fattibilità e impatto
  memoria/latenza da validare prima di committare l'Opzione B/C.

---

## 7. Decomposizione in sub-progetti

| SP | Cosa | Metà | Costo | Dipende da |
|----|------|------|-------|-----------|
| **SP1 — Image-region capture** | Estendere il capture bbox #3403 alle categorie `Image`/`FigureCaption`; persistere come `bounding_boxes_json` su un chunk "regione-immagine" (o metadato pagina); FE disegna la regione | R | S | #3403 SP-B/SP-D deployati |
| **SP2 — Table-region router** | Euristica DC-B per marcare i PDF/pagine con tabelle-immagine candidate; flag persistito | R+C | S | SP1 |
| **SP3 — SmolDocling deploy + crop-extract** | Deploy SmolDocling staging; endpoint "estrai contenuto da crop-immagine"; job asincrono che ritaglia le regioni SP1 e ne estrae il contenuto (markdown/HTML) | C | L | SP2, DC-E |
| **SP4 — Table-chunk indexing** | Indicizzare il contenuto-tabella come chunk retrievabile con `bounding_boxes_json` = regione SP1; gating copyright | C | M | SP3 |
| **SP5 — Answerability + citazione tabella** | La citazione di un chunk-tabella apre pagina + evidenzia regione; test end-to-end di risposta a domanda tabellare | C | M | SP4 |
| **SP6 — Rollout/re-extract** | Re-estrazione mirata (ReindexDocumentCommand) dei PDF table-heavy per-env; runbook | R+C | M | SP4 deployato |

SP1-SP2 (Metà R) spedibili subito e danno valore. SP3-SP5 (Metà C) sono il grosso, sviluppabili con contratto VLM mockato.

---

## 8. Rischi e mitigazioni

| Rischio | Mitigazione |
|---|---|
| **hi_res troppo lento** (>120s) per il path sincrono | DC-D: estrazione tabella-immagine come **job asincrono** post-Ready; oppure verificare se `fast` dà già le bbox `Image` (DC-A) |
| **SmolDocling degrada il testo narrativo** (quality 0.70 vs 0.88) se usato primario (Opzione C) | Preferire Opzione B (VLM solo sui crop-tabella; Unstructured resta primario per il testo) |
| **False positive** (illustrazioni marcate come tabelle) | IA-1: conferma a due segnali (regione hi_res + `has_tables` VLM); scartare crop dove il VLM non trova struttura tabellare |
| **Correlazione crop↔contenuto** fragile (Opzione B) | bbox della regione = chiave; il crop è definito dalla bbox → contenuto e regione condividono la stessa geometria per costruzione |
| **Copyright leak** (contenuto tabella verbatim) | IA-4: gating `CopyrightTier=Full`; test che asserisce assenza contenuto/regione su Protected |
| **Costo VLM sul corpus** | IA-5: VLM solo su PDF con regioni candidate (SP2 router); job batch, non ingest sincrono |
| **SmolDocling non deployabile su 8GB CPU** con latenza accettabile | DC-E: validare deploy + benchmark PRIMA di committare SP3; fallback: OCR+table-transformer o rinvio |
| **DocTags location assenti** (DC-C) | Se SmolDocling non dà location → Opzione B (regione da hi_res) invece di C |

---

## 9. Criteri di accettazione

- **AC1 (R)** — Per un rulebook con tabelle-immagine (es. agricola), il FE evidenzia la **regione** della tabella
  quando una citazione la tocca; nessun errore se la regione non c'è.
- **AC2 (C)** — Il contenuto di ≥1 tabella-immagine è estratto (celle/valori) e presente come chunk retrievabile con
  la bbox della regione.
- **AC3 (Answerability)** — Una domanda su un valore tabellare noto ritorna la risposta corretta con citazione che
  apre la pagina + evidenzia la regione.
- **AC4 (Copyright)** — Per tier `Protected`: nessun contenuto-tabella verbatim né regione geometrica.
- **AC5 (No regressione)** — PDF senza tabelle-immagine: latenza/costo di ingest invariati; baseline unit-test = 0 fail.

---

## 10. Domande aperte per lo spec-panel / owner

1. **Priorità Metà R vs Metà C**: la verificabilità visiva (R, economica) è sufficiente per il valore-utente
   immediato, o serve subito l'answerability (C, costosa)?
2. **DC-A/DC-C/DC-E** sono i tre nodi tecnici che decidono tra Opzione A/B/C — vanno risolti con 3 spike mirati prima
   di stimare l'effort reale.
3. **Scope corpus**: solo board-game rulebook (tabelle-immagine) o anche PDF con tabelle-testo (dove DC-2 #3419
   *avrebbe* senso, ma il corpus non ne ha)? Il router SP2 può gestire entrambi.
4. **Rapporto con #3419**: #3419 resta latente (si attiverebbe su tabelle-testo). Va tenuto, deprecato, o assorbito
   nel router SP2 di questo spec?

---

## 11. Riferimenti

- Investigazione #3419 su staging (2026-08-01): fast/hi_res 0 `Table`, tabelle=immagini, hi_res >120s, SmolDocling non triggerato.
- Epic #3403 — RAG citation region grounding (`docs/superpowers/specs/2026-07-30-rag-citation-region-grounding-design.md`),
  in particolare non-goal §88 ("Livello 2 separato") + infra `bounding_boxes_json`/`regions[]`/`PdfBBoxOverlay` + DA-4 copyright.
- `apps/smoldocling-service/` (VLM image-based, DocTags/Markdown, `has_tables`), Stage 2 pipeline (BGAI-005 #945).
- `apps/unstructured-service/src/infrastructure/unstructured_adapter.py:41-47` (`infer_table_structure=True`, hi_res).
- ADR-059 (copyright posture), #447 (copyright leak guard), ADR-060 (persistence), `ReindexDocumentCommand`/`IndexerVersionRegistry` (re-index).
