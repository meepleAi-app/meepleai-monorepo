# Ricerca — Migliorare l'estrazione di testo, tabelle e immagini dai PDF dei regolamenti

**Data**: 2026-10-15 · **Tipo**: ricerca (non è una decisione; le decisioni vanno in ADR) · **Profondità**: standard (inventario del codice + misure sul DB locale + web)
**Seguito**: epic [#4433](https://github.com/meepleAi-app/meepleai-monorepo/issues/4433), con i passi 0–3 di §7 nelle issue #4434–#4440; i passi 4–7 aspettano le decisioni di §10.
**Codice di riferimento**: `origin/main-dev` @ `306a3eb83`. Il `main-dev` locale era indietro di diversi commit (fra cui la rimozione di `VisionOcrAdapter`), quindi l'inventario è letto con `git show origin/main-dev:<file>`.
**Convenzione**: i numeri sul corpus sono **esiti di questo passaggio** sul DB locale (`meepleai_staging` dello stack di sviluppo, non il server di staging) e hanno il comando in appendice A. I prezzi sono listino USD di ottobre 2026; `(sec.)` = fonte secondaria, perché la pagina ufficiale non espone il valore. La confidenza è indicata dove conta.

---

## 1. Sommario esecutivo

1. **Il problema più grosso non è la scelta del parser, è che la struttura che già calcoliamo va persa.** Il passaggio `hi_res` di Unstructured, che gira già una volta per PDF per i ritagli immagine-tabella (#3435, ~200 s a PDF), calcola le tabelle con `infer_table_structure=True`. Ma il servizio Python non serializza `text_as_html`, e del passaggio si conservano solo le bbox. La struttura delle tabelle si paga e si butta. (Confidenza alta: verificato nel codice.)
2. **In ingestione nessuna tabella arriva strutturata all'indice.**
   - Staging usa Unstructured in modalità `fast`, che non fa analisi di layout. `infer_table_structure` vale «only if strategy=hi_res» (docstring di `partition_pdf`).
   - L'instradamento verso `hi_res` per i PDF con tabelle (#3419) è **inerte**: decide guardando se l'estrazione *precedente* contiene elementi `Table`, che `fast` non produce. Il commento nel codice lo chiama «(inert-on-corpus) selector».
   - L'estrattore iText produce tabelle che **nessuno legge**. Nel DB locale il campione è prosa su due colonne scambiata per tabella.
3. **Codice morto e dipendenze a rischio sul percorso di ingestione**:
   - l'OCR di ripiego di Docnet non è registrato nel DI, quindi non parte mai;
   - la lingua inviata a Unstructured è fissata a `"ita"`, mentre il corpus locale è quasi tutto inglese;
   - `itext7` (licenza **AGPLv3**) serve solo a percorsi senza lettori: tabelle, «diagrammi» segnaposto, metadati non iniettati da nessuno;
   - `unstructured` è fermo a 0.18.18, contro 0.27.27 del giorno della ricerca.
4. **Lo stato dell'arte 2026 rende sostituibili sia Unstructured sia SmolDocling** con un solo servizio **Docling**: MIT, arm64, layout Heron, TableFormer per le tabelle, chunker con heading e didascalie, classificazione delle immagini. Il successore di SmolDocling è `granite-docling-258M` (Apache 2.0, struttura tabelle TEDS 0,97 contro 0,82). I modelli in testa a OmniDocBench (PaddleOCR-VL, MinerU 2.5) sono più precisi ma pensati per GPU; MinerU ha cambiato licenza.
5. **Non esiste un banco che misuri l'estrazione.** Il gate RAG gira su un corpus estratto con Docnet, mentre staging indicizza con Unstructured: un miglioramento del parser oggi **non è misurabile**. Va costruito per primo (§9).

---

## 2. Come funziona oggi

### 2.1 Il percorso dipende dall'ambiente

| Ambiente | Estrattore effettivo | Heading | Tabelle | OCR | Evidenza |
|---|---|---|---|---|---|
| Sviluppo locale | **Docnet** (default di `appsettings.json`) | no | no | no (ripiego morto, §3 D2) | `appsettings.json` → `PdfProcessing:Extractor:Provider: "Docnet"`; `compose.dev.yml` non lo sovrascrive |
| Staging | **Unstructured, `fast`** | sì (`Title` dai raw elements, SP1 #3267) | appiattite in prosa | no | `compose.staging.yml`: `PdfProcessing__Extractor__Provider: "Unstructured"`; `UnstructuredPdfTextExtractor` invia `fast` salvo override |
| Gate RAG / E2E | Docnet | no | no | no | `e2e-pdf-s3-upload.yml`: `PdfProcessing__Extractor__Provider=Docnet`; CLAUDE.md #3740 (gate su chunk Docnet, staging heading-aware) |
| Cascata `Orchestrator` | Unstructured ≥0,80 → SmolDocling ≥0,70 → Docnet | — | — | — | `EnhancedPdfProcessingOrchestrator`; **non usata da nessun ambiente** |

### 2.2 La pipeline asincrona immagini-tabelle (#3435)

Esiste, è completa e **spenta per default** (runbook `docs/for-developers/operations/2026-08-04-image-table-vlm-activation-runbook.md`):

- `SeedImageRegionsJob` lancia un passaggio `hi_res` per PDF (~200 s) e salva le bbox `Image`/`FigureCaption`/`Table` in `pdf_image_regions`;
- `RunTableExtractionJob` ritaglia la regione e la manda a SmolDocling (`/api/v1/extract-image`). Se l'output contiene `<otsl>` è una tabella e diventa un chunk `ElementType='Table'` con la bbox per la citazione.
- **Precondizione dichiarata**: GPU. SmolDocling-256M su CPU supera i 95 s/pagina e il server di staging non basta.

### 2.3 Cosa c'è nel DB locale (misure di questo passaggio, appendice A)

| Misura | Valore |
|---|---|
| PDF `Ready` | 146 (più 1 in `Embedding` e 1 in `Pending`) |
| Lingua dei `Ready` | en 132 · it 13 · de 1 |
| Pagine dei `Ready` | 2.260 in totale; mediana 15, massimo 84 |
| Caratteri per pagina | < 100 (probabile scansione): 1 PDF, 2 pagine · 100–499: 1 PDF · 500–1999: 19 PDF · ≥ 2000: 116 PDF (gli altri non hanno `PageCount`) |
| `structured_elements_json` valorizzato | 0 PDF: tutto il corpus locale è passato da Docnet |
| `text_chunks` | 11.015, tutti `ElementType='text'`, nessuno con `Heading` |
| `ExtractedTables` non vuoto | 125 PDF su 146; nel campione letto le due «intestazioni» sono le prime righe di due colonne di prosa |
| `ExtractedDiagrams` | segnaposto «Image N on page N» con le dimensioni, nessun contenuto |
| `pdf_image_regions` | 609 `Image` + 4 `FigureCaption`, tutte da **un solo** PDF |
| `pdf_table_extractions` | 0 righe |

Due letture, con il loro limite. (a) Le scansioni sono rare in questo corpus: l'OCR non è la priorità, a meno che gli utenti non carichino foto o scansioni. (b) 609 regioni immagine da un solo regolamento fanno pensare che `hi_res` segmenti anche le decorazioni. È un PDF solo: prova debole, da misurare sul banco.

---

## 3. Difetti e sprechi trovati nel codice

| # | Difetto | Evidenza | Effetto |
|---|---|---|---|
| D1 | La struttura delle tabelle calcolata da `hi_res` non attraversa il servizio: il campo `text_as_html` non è serializzato | `apps/unstructured-service/src/main.py` (filtro e schema degli elementi); `git grep text_as_html` nel servizio e nell'API → nessun risultato | il costo di `hi_res` si paga e le tabelle arrivano come testo piatto |
| D2 | L'OCR di ripiego di Docnet non parte mai: `DocumentProcessing…External.IOcrService` non ha registrazioni. L'unica `IOcrService` registrata è un'altra interfaccia, di `SessionTracking` | `DocnetPdfTextExtractor(…, IOcrService? ocrService = null)`; `git grep TesseractOcrAdapter` trova solo la classe | una scansione passa da Docnet senza testo e senza errore esplicito |
| D3 | La lingua inviata a Unstructured è il letterale `"ita"`; l'opzione `PdfProcessing:Extractor:Unstructured:Language` è ignorata | `UnstructuredPdfTextExtractor.cs` (`new StringContent("ita")`) | nel passaggio `hi_res` l'OCR legge in italiano un corpus quasi tutto inglese |
| D4 | L'instradamento `fast` → `hi_res` per tabelle è inerte | `ExtractionStrategyDecider.FromStructuredElements` cerca `Table` nell'estrazione precedente, che `fast` non produce; commento «(inert-on-corpus) selector» in `UnstructuredPdfTextExtractor` | nessun PDF con tabelle viene mai estratto in `hi_res` sul percorso principale |
| D5 | iText (AGPLv3) alimenta solo percorsi senza lettori: `ExtractedTables` e `ExtractedDiagrams` hanno solo scritture; `IPdfMetadataExtractor` è registrato ma non iniettato | `git grep '\.ExtractedTables\|\.ExtractedDiagrams'` → solo assegnazioni; `itext7` 9.4.0 in `Api.csproj`; i file che importano `iText.` sono elencati in appendice A | calcolo sprecato in ogni ingestione e una dipendenza AGPL in un prodotto proprietario. iText dichiara che sotto AGPL «you may not deploy it on a network without disclosing the full source code». ADR-003b aveva scartato `pymupdf4llm` proprio per l'AGPL. **Non è un parere legale** |
| D6 | `unstructured` fermo a 0.18.18; su PyPI l'ultima è 0.27.27 (2026-10-15), con rilasci settimanali | `apps/unstructured-service/requirements.txt`; PyPI | correzioni e modelli persi; il servizio non è in nessuna pipeline CI e si ricostruisce a mano sul server |
| D7 | Nel punteggio di qualità la componente tabelle vale 0,3 quando non ci sono tabelle, e `fast` non ne trova mai: il punteggio pesato raggiunge la soglia di 0,80 solo con le altre componenti al massimo | `quality_calculator.py` (pesi 0,4/0,2/0,2/0,2) | conta solo per la cascata, che oggi nessun ambiente usa; da ricordare se la si riaccende |
| D8 | Il modello del servizio VLM è `SmolDocling-256M-preview`, sostituito dal suo successore il 2025-09-17 | `apps/smoldocling-service/src/config/settings.py`; scheda HF di granite-docling | precisione sulle tabelle più bassa del necessario (§4) |

---

## 4. Stato dell'arte 2026 (self-hosted)

| Strumento | Licenza | Cosa porta | Hardware | Lingue | Adatto a noi |
|---|---|---|---|---|---|
| **Docling** (pipeline standard) | **MIT** | layout Heron (RT-DETRv2, default da v2.50), reading order, tabelle con TableFormer, formule, classificazione immagini, didascalie con VLM opzionale, chunker gerarchico e ibrido con heading e didascalie e ripetizione dell'intestazione di tabella | CPU e **arm64** supportati. Report tecnico 2024 senza OCR: Xeon E5-2690, backend pypdfium, 0,94 pag/s con 4 thread e 1,57 pag/s con 16. Heron ha runtime «comparabile o migliore» del modello precedente | OCR con EasyOCR o Tesseract | **sì**: candidato a sostituire Unstructured e SmolDocling |
| **granite-docling-258M** | Apache 2.0 | VLM end-to-end in DocTags/OTSL, stesso formato di SmolDocling. Contro SmolDocling: TEDS struttura tabelle 0,97 vs 0,82, OCR F1 0,84 vs 0,80, layout mAP 0,27 vs 0,23 | GPU, vLLM, MLX; su CPU nessun dato ufficiale | **inglese**; giapponese, arabo e cinese sperimentali | sì, come sostituto diretto di SmolDocling nel percorso dei ritagli (#3435), sempre su GPU |
| **Unstructured OSS** | Apache 2.0 | `fast` / `hi_res` / `ocr_only` / `auto`; i modelli migliori sono nell'API a pagamento «Transform v2» | `hi_res` lento su CPU: ~200 s a PDF nel repo | Tesseract | sì, se si resta: aggiornare, usare `auto` e serializzare `text_as_html` |
| **PaddleOCR-VL** (0.9B, 1.6) | Apache 2.0 | primo o fra i primi su OmniDocBench v1.6 (96,34 `(sec.)`); tabelle, grafici, formule | GPU (CUDA, vLLM) | 109 lingue | come esperimento GPU per le tabelle-immagine multilingue |
| **MinerU** 2.5 / 4.0 | **MinerU Open Source License** (Apache 2.0 con condizioni aggiuntive e soglie d'uso commerciale), da AGPL | 95,75 su OmniDocBench v1.6 `(sec.)`; livelli Flash/Basic/Standard/Advanced, anche su CPU (ONNX) | da 2 GB (base) a 8 GB (standard/avanzato) | — | **solo dopo una revisione legale** della licenza |
| PyMuPDF / pymupdf4llm | AGPL | veloce | CPU | — | no: già scartato da ADR-003b per l'AGPL |

Nota di metodo: i punteggi OmniDocBench vengono da classifiche secondarie e misurano documenti generici (articoli, report, giornali), non regolamenti da tavolo con icone e impaginazione a colonne. Ordinano i candidati, non sostituiscono il banco di §9.

---

## 5. Opzioni gestite (file intero all'esterno: vale ADR-093)

ADR-093 permette di inviare il file intero solo a fornitori in **allowlist**, che oggi è vuota, con no-training, cancellazione, regione UE e DPIA. Uno stage esterno è sempre opzionale e spento per default.

| Fornitore | Prezzo | Dati | Note |
|---|---|---|---|
| **Mistral OCR 4.1** (`mistral-ocr-4-1`) | ~$4 per 1.000 pagine, batch ~$2 `(sec.)`; la pagina prezzi ufficiale non espone il valore | società UE, endpoint `api.eu.mistral.ai` `(sec.)`. **La ZDR non è attiva per default**: va richiesta, su piano a pagamento, con motivazione. No-training e ZDR sono **controlli separati** (documentazione Mistral) | il più vicino alla fase due di residenza UE (decisione dell'utente del 2026-10-13); restituisce bbox e etichette di blocco |
| **Azure Document Intelligence** (Layout) | ~$10 per 1.000 pagine `(sec.)` (Microsoft Q&A; la pagina ufficiale non rende la cifra); F0 500 pagine al mese | dipende dal contratto Azure | esercizio AI-200 secondo la ricerca del 2026-10-08, che segnala anche il limite di 2 pagine per documento sul piano gratuito `(sec.)` |
| LlamaParse | a crediti | — | già citato in ADR-093 |

Ordine di grandezza: il corpus locale (2.260 pagine) costa ~$9 con Mistral e ~$23 con Azure Layout, per un'indicizzazione completa. **Il vincolo è ADR-093, non il costo.**

---

## 6. Immagini e diagrammi

Oggi dalle immagini non arriva nulla di utile all'indice. `ExtractedDiagrams` contiene solo segnaposto, e le regioni `Image` servono solo a cercare tabelle fatte di grafica.

| Approccio | Come | Pro | Contro |
|---|---|---|---|
| **Classificare prima** | classificatore di figure di Docling (`DocumentFigureClassifier-v2.5`) per separare decorazioni, diagrammi, tabelle e icone | economico, filtra le 609 regioni del caso sopra | da solo non rende nulla cercabile |
| **Didascalia + indicizzazione** | un VLM piccolo e locale (SmolVLM-256M, Granite Vision 2B) descrive i diagrammi; la descrizione diventa un chunk con ruolo proprio, pagina e bbox | riusa tutto il RAG testuale | le didascalie inventate inquinano l'indice: servono un ruolo distinto, peso minore e citazione della pagina. Non è la scansione della scatola, che resta esclusa (decisione del 2026-10-13), ma è un nuovo uso di un VLM e va deciso dall'utente |
| **Pagina come immagine** (ColPali, ColQwen) | si indicizzano le pagine renderizzate con embedding multi-vettore | il richiamo migliore sui documenti pieni di figure `(sec.)` | da 100 a 1000 volte più vettori per pagina `(sec.)`, GPU, secondo spazio vettoriale accanto a `vector(768)`: non adatto al server attuale |

Un'ipotesi specifica del dominio, **da misurare e non verificata**: nei regolamenti le icone in linea («guadagna 2 [icona legno]») sono immagini dentro la riga di testo. Un estrattore testuale le perde e lascia frasi monche. Il lavoro sulla legenda (#3243, «legend-demotion») è il punto d'aggancio naturale per ricollegare icona ed etichetta.

---

## 7. Proposta in ordine di valore per costo

Ogni passo lascia il prodotto funzionante, ed è misurabile solo dopo il passo 0.

| # | Cosa | Costo | Rischio | Misura di successo |
|---|---|---|---|---|
| 0 | **Banco di estrazione** (§9) e gate RAG eseguito con l'estrattore in prova | medio | basso | il banco gira in CI e riporta le metriche per estrattore |
| 1 | **Correzioni senza nuove dipendenze**: lingua dalla configurazione o dal rilevamento (D3); `strategy=auto` ammesso, così le pagine senza testo passano da sole a `hi_res` (D2/D4); registrare o eliminare l'OCR di Docnet (D2); `unstructured` aggiornato (D6) | basso | basso | sul banco: nessuna scansione con testo vuoto; heading invariati o migliori |
| 2 | **Non buttare le tabelle già calcolate**: serializzare `text_as_html` (D1) e trasformare le tabelle del passaggio `hi_res` in chunk `Table` (Markdown o HTML, con l'intestazione ripetuta per riga) | medio | medio (cambiano i chunk, quindi #3737 e fusione da rimisurare) | F1 a livello di cella sul banco; richiamo@k sul gate per domande «quanto costa / quanti punti» |
| 3 | **Togliere iText** (D5): eliminare i percorsi senza lettori e il pacchetto | basso | basso | `itext7` assente da `Api.csproj`; nessuna regressione |
| 4 | **Prova di Docling in ombra**: un `docling-service` (CPU, arm64) che gira accanto all'estrattore attuale e scrive gli elementi in una colonna separata, confrontati sul banco | medio-alto | basso finché è in ombra | heading, tabelle e ordine di lettura sul banco contro Unstructured `hi_res`; secondi per pagina sul server di staging |
| 5 | **granite-docling al posto di SmolDocling** nel percorso dei ritagli (#3435), quando ci sarà una GPU | basso (stesso formato DocTags/OTSL) | basso | precisione tabella sì/no sui ritagli del banco |
| 6 | **Immagini**: prima classificazione, poi, se l'utente lo decide, didascalie locali dei soli diagrammi | medio | medio (didascalie inventate) | richiamo sulle domande legate a figure; tasso di didascalie sbagliate su un campione a mano |
| 7 | **Stage esterno opzionale** (Mistral OCR) dietro ADR-093, solo per i PDF che il banco segnala come difficili | basso in codice, alto in processo (allowlist e DPIA) | legale | pagine inviate e costo nella metrica `meepleai_external_parser_pages_total{provider}` |

Se la prova del passo 4 vince, Docling sostituisce sia `unstructured-service` (immagine da 10 GB, fuori da ogni pipeline CI) sia `smoldocling-service` con **un solo servizio**. Il formato `DoclingDocument` (titoli, sezioni, tabelle, immagini con pagina e bbox) si adatta a `ExtractedElement` e al chunker con heading esistente.

Ogni cambio di chunk richiede la reindicizzazione con le due guardie di ADR-086 e la nuova taratura **misurata** dei pesi di #3737 con l'artifact `rag-fusion-tuning`, non a mente.

---

## 8. Vincoli da rispettare

- **ADR-093**: niente file intero all'esterno senza una riga in allowlist e la DPIA.
- **ADR-051 / ADR-059**: le citazioni restano entro 25 parole. Le fixture del banco **non** devono contenere testo verbatim dei regolamenti: usare PDF generati da noi, come `tests/fixtures/make-text-pdf.py`, o salvare solo struttura e hash attesi.
- **Server di staging**: arm64, niente GPU. Il servizio Unstructured ha un limite di 1 CPU e 3 GB. Il disco è sotto pressione: non ricostruire immagini torch sul server (memorie `vps-torch-rebuild-diskfull`, `staging-vps-disk-pressure`).
- **Decisione del 2026-10-13**: nessun riconoscimento per immagini per identificare un gioco. Le didascalie dei diagrammi del regolamento sono un uso diverso, ma vanno confermate.

---

## 9. Banco di estrazione (prerequisito)

Oggi il gate RAG misura un corpus estratto con Docnet, mentre staging indicizza con Unstructured. Un banco minimo:

- **Fixture**: una dozzina di PDF che coprono i casi difficili dei regolamenti: due colonne, tabelle di testo, tabelle disegnate, icone in linea, una scansione, e lingue en, it e de. Testo generato o con licenza libera.
- **Atteso per ogni fixture**: titoli con livello, tabelle a livello di cella, ordine di lettura delle colonne, pagine senza testo.
- **Metriche**:
  - richiamo dei titoli (sul modello del controllo 6/6 di SP1);
  - F1 delle celle di tabella (una versione semplificata di TEDS);
  - ordine di lettura corretto;
  - secondi per pagina;
  - a valle, richiamo@k del gate RAG eseguito con quell'estrattore.
- **Dove**: un test L3 con prefisso `PREVISTO:`, che gira quando i servizi di estrazione sono attivi (convenzione di #4021).

---

## 10. Decisioni aperte (servono all'utente)

1. **iText**: rimuoverlo (consigliato: i suoi percorsi non hanno lettori) o acquistare la licenza commerciale.
2. **Parser di riferimento**: restare su Unstructured, aggiornato e usato meglio (passi 1–2), oppure provare Docling in ombra (passo 4). Si può fare prima l'uno e poi l'altro.
3. **Didascalie dei diagrammi con un VLM locale**: sì o no.
4. **Stage esterno**: compilare la prima riga dell'allowlist di ADR-093, e con quale fornitore. Mistral si allinea alla futura residenza UE, Azure all'esercizio AI-200.
5. **GPU** per i ritagli tabella (#3435): un nodo o una finestra batch su una GPU locale, come prevede il runbook.

---

## 11. Fonti

- PyPI, `unstructured`: https://pypi.org/project/unstructured/
- Unstructured, docstring di `partition_pdf` (`infer_table_structure` «Only applicable if strategy=hi_res»): https://raw.githubusercontent.com/Unstructured-IO/unstructured/main/unstructured/partition/pdf.py
- Unstructured, strategie: https://docs.unstructured.io/open-source/concepts/partitioning-strategies
- Docling, repository (licenza MIT, arm64): https://github.com/docling-project/docling
- Docling, arricchimenti (classificazione e descrizione immagini): https://docling-project.github.io/docling/usage/enrichments/
- Docling, chunking (`HierarchicalChunker`, `HybridChunker`, `repeat_table_header`): https://docling-project.github.io/docling/concepts/chunking/
- Docling Technical Report (prestazioni su CPU, tabella 1): https://arxiv.org/html/2408.09869
- Advanced Layout Analysis Models for Docling (Heron): https://arxiv.org/abs/2509.11720
- granite-docling-258M (licenza, data, confronto con SmolDocling, lingue): https://huggingface.co/ibm-granite/granite-docling-258M
- PaddleOCR-VL: https://huggingface.co/PaddlePaddle/PaddleOCR-VL
- MinerU (licenza): https://github.com/opendatalab/MinerU
- Classifica OmniDocBench `(sec.)`: https://www.codesota.com/ocr/benchmark/omnidocbench · https://roboflow.com/blog/best-open-source-ocr-models
- Mistral, Zero Data Retention: https://docs.mistral.ai/admin/monitor-comply/zero-data-retention
- Mistral, modelli OCR: https://docs.mistral.ai/getting-started/models/models_overview/
- Mistral OCR, prezzi `(sec.)`: https://www.datastudios.org/post/mistral-ocr-4-pricing-bounding-boxes-and-170-language-benchmarks-explained
- Azure Document Intelligence, prezzi (F0): https://azure.microsoft.com/en-us/pricing/details/document-intelligence/ · Layout ~$10/1.000 pagine `(sec.)`: https://learn.microsoft.com/en-us/answers/questions/5665475/what-is-the-cost-of-document-intellegence-service
- iText, licenza AGPLv3: https://itextpdf.com/how-buy/AGPLv3-license
- RAG multimodale, confronto fra approcci `(sec.)`: https://bigdataboutique.com/blog/multimodal-rag-retrieval-over-images-pdfs-and-text

---

## Appendice A — Comandi

Misure sul DB locale (solo lettura; Windows, PowerShell):

```powershell
$q = @'
select processing_state, count(*) from pdf_documents group by 1 order by 2 desc;
select coalesce(language_override,"Language") lang, count(*) from pdf_documents where processing_state='Ready' group by 1 order by 2 desc;
select case when "CharacterCount"::float/"PageCount" < 100 then 'a <100' when "CharacterCount"::float/"PageCount" < 500 then 'b 100-499'
            when "CharacterCount"::float/"PageCount" < 2000 then 'c 500-1999' else 'd >=2000' end fascia, count(*), sum("PageCount")
  from pdf_documents where processing_state='Ready' and "PageCount">0 group by 1 order by 1;
select count(*), sum("PageCount"), percentile_cont(0.5) within group (order by "PageCount"), max("PageCount") from pdf_documents where processing_state='Ready';
select count(*) filter (where structured_elements_json is not null) from pdf_documents where processing_state='Ready';
select coalesce("ElementType",'(null)'), count(*), count(*) filter (where "Heading" is not null and "Heading"<>'') from text_chunks group by 1;
select count(*) filter (where coalesce("ExtractedTables",'') not in ('','[]')) from pdf_documents where processing_state='Ready';
select element_type, count(*), count(distinct pdf_document_id) from pdf_image_regions group by 1;
select count(*) from pdf_table_extractions;
'@
$q | docker exec -i meepleai-postgres psql -U meepleai -d meepleai_staging -A -F ' | '
```

Evidenze nel codice (dal repo, Git Bash):

```bash
R=origin/main-dev
git grep -n 'text_as_html' $R -- apps/unstructured-service/src 'apps/api/src/Api/**/*.cs'          # D1: nessun risultato
git grep -n 'TesseractOcrAdapter' $R -- 'apps/api/src/Api/**/*.cs' | grep -v Tests                # D2: solo la classe
git grep -n 'StringContent("ita")' $R -- 'apps/api/src/Api/**/*.cs'                                # D3
git grep -n 'inert-on-corpus' $R -- 'apps/api/src/Api/**/*.cs'                                     # D4
git grep -n -E '\.ExtractedTables|\.ExtractedDiagrams' $R -- 'apps/api/src/Api/**/*.cs' | grep -v Tests   # D5: solo scritture
git grep -l -E '^using iText\.' $R -- 'apps/api/src/Api/**/*.cs' | grep -v Tests                   # D5: file che usano iText
git show $R:apps/unstructured-service/requirements.txt | grep '^unstructured'                      # D6
```
