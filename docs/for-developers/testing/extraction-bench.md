# Banco di qualità dell'estrazione PDF

**Misura cosa un estrattore restituisce di un PDF: titoli, tabelle, ordine di lettura, pagine senza testo.**

Riferimenti: [#4434](https://github.com/meepleAi-app/meepleai-monorepo/issues/4434) parte (a) · epic [#4433](https://github.com/meepleAi-app/meepleai-monorepo/issues/4433) · ricerca [`2026-10-15-pdf-extraction-improvements-research.md`](../research/2026-10-15-pdf-extraction-improvements-research.md) · livelli di dipendenza: [spec 2026-10-02](../specs/2026-10-02-test-dependency-tiers-and-observable-skips.md)

---

## Perché esiste

Il gate RAG non estrae: ripristina uno snapshot già indicizzato (con Docnet, senza heading). Un cambio di estrattore, o di versione di `unstructured`, non sposta nulla di ciò che misura. Questo banco è il posto dove l'estrazione si misura, a monte dell'indice.

## Cosa fa

1. **Genera i PDF** da descrizioni JSON (`TestData/extraction-bench/fixtures/*.json`) con QuestPDF, già dipendenza di `Api.Tests`. Nessun binario committato, nessun testo di regolamenti di terzi (ADR-051/059): il testo è inventato per il banco, in en, it e de.
2. **Ricava l'atteso dalla stessa descrizione** (`ExtractionExpected.From`): contenuto e atteso non possono divergere.
3. **Esegue un estrattore** e lo riduce a una *vista normalizzata* (`ExtractionObservation`).
4. **Calcola le metriche** (`ExtractionMetrics`, funzioni pure con test propri) e le confronta con la **baseline committata** (`TestData/extraction-bench/baseline.<estrattore>.json`).
5. **Scrive un artifact** con le metriche e i secondi per pagina.

## Le fixture

| Fixture | Mette alla prova |
|---|---|
| `two-columns-en` | due colonne: ordine di lettura colonna sinistra, poi destra |
| `text-table-it` | tabella di testo con intestazione, accenti italiani |
| `graphic-table-de` | tabella **disegnata come immagine** (nessun livello di testo), dieresi e ß |
| `inline-icons-en` | icone vettoriali dentro le frasi |
| `scanned-page-en` | una pagina di testo e una pagina **solo immagine** (scansione) |

**Regola di scrittura**: nessuna cella di tabella può comparire, nemmeno come sottostringa senza spazi, nel resto del testo della fixture (`TableCells_NeverAppearInTheRestOfTheFixtureText`). Altrimenti `tableCellRecall` misurerebbe il paragrafo e non la tabella: la prima stesura di `graphic-table-de` dava 0,17 a Docnet, che la tabella disegnata non la legge, perché «Kosten» stava nella frase sopra; e «Oste» si trovava dentro «nello stesso turno».

Le pagine «solo immagine» (la scansione e la tabella grafica) si ottengono componendo il contenuto come testo in un documento d'appoggio, rasterizzandolo con `GenerateImages` e incorporando il PNG nel PDF finale. Il PDF non ha livello di testo ma i pixel ci sono: `ExtractionFixtureTests` lo prova su entrambi i fronti (pixel scuri nel PNG, immagine incorporata nel PDF).

## Le metriche

Ogni metrica restituisce un numero in [0, 1] oppure **`null` = non applicabile**: mai uno zero fittizio. Le due cause sono distinte: la fixture non prevede la cosa, oppure l'estrattore non può darla.

| Metrica | Cosa dice | Insensibile a |
|---|---|---|
| `paragraphRecall` | quota dei paragrafi attesi ritrovati, contigui | spazi, maiuscole, tipografia |
| `wordRecall` | quota delle parole distinte attese ritrovate a parola intera | maiuscole, tipografia — **vede le parole fuse** |
| `readingOrder` | quota delle coppie consecutive di indicatori nell'ordine giusto | spazi |
| `tableCellRecall` | quota delle celle attese presenti nel testo (contenuto, non struttura) | spazi |
| `tableCellF1` | F1 sulle celle come multiinsieme (struttura) | — |
| `headingRecall` | quota dei titoli attesi fra i titoli riconosciuti | spazi |
| `scanWordRecall` | quota delle parole della pagina scansionata restituite | maiuscole, tipografia |
| secondi per pagina | solo nell'artifact; **mai asseriti** (precedenti #3601, #4274) | |

### Criterio di match

Uguale per tutti gli estrattori (`ExtractionTextMatcher`): NFKC (le ligature tornano lettere), minuscole, trattino morbido tolto, trattini e apici tipografici ricondotti ai semplici. **Le diacritiche restano**: «über»/«uber» e «perché»/«perche» sono parole diverse. Per `paragraphRecall`, `readingOrder`, `tableCellRecall` e `headingRecall` anche gli spazi sono ignorati: pdfium perde lo spazio ai ritorni a capo automatici dei PDF generati dal banco («die folgende» → «diefolgende»), e un confronto sensibile agli spazi darebbe zero a un testo che c'è tutto. Le parole fuse si misurano a parte con `wordRecall`.

Tolleranza OCR: rapporto massimo fra distanza di edit e lunghezza della parola, **zero** di partenza. Si alza solo con una misura di `hi_res` che la giustifichi.

## Semantica per estrattore

Gli estrattori non restituiscono la stessa cosa; la vista normalizzata tiene i campi che hanno senso per entrambi e mette `null` dove un estrattore non sa rispondere.

| Campo della vista | Docnet | Unstructured |
|---|---|---|
| testo completo | testo delle pagine, in ordine | testi degli elementi, nell'ordine del documento |
| testo per pagina | pagine reali, anche quelle vuote | pagine lette dagli elementi (`PageNumber`); nessuna riga per una pagina senza elementi |
| titoli | **`null`** (non classifica) → `headingRecall` non applicabile | elementi di categoria `Title`; lista vuota se nessuno → `headingRecall` = 0 (vero) |
| celle | **`null`** (solo testo) → `tableCellF1` non applicabile | **`null`** finché il servizio non serializza `text_as_html` (#4439) |

Quando #4439 farà arrivare le celle, la vista le popola e `tableCellF1` diventa applicabile **senza toccare le metriche**; il confronto con la baseline lo considera una capacità nuova, non un fallimento.

**Docnet sulla pagina scansionata** non restituisce testo: non fa OCR (#4437). È un risultato vero e sta nella baseline (`scanWordRecall` = 0). Se un giorno cambia, il banco deve accorgersene, e il test `Docnet_FindsNoTextOnTheScannedPage_ButFindsItOnThePageBefore` verifica direttamente che sia la pagina a essere priva di testo e non un guasto del renderer.

## Regola della baseline

**«Non peggiore» = metrica ≥ baseline.** Tolleranza **zero** per Docnet e per Unstructured `fast` (deterministici a parità di versione). Unstructured `hi_res` è **informativo** (non blocca) finché non se ne misura lo scarto massimo su più run, da scrivere qui e nel codice.

Il confronto fallisce anche per ciò che manca: una fixture o una metrica assente dalla baseline (sarebbe non sorvegliata), una fixture della baseline che il run non ha misurato, una metrica che era misurabile e non lo è più. Un miglioramento, o una metrica che diventa applicabile, non è un fallimento.

Un cambio di versione (di Docnet, di `unstructured`: #4438) che sposta un numero **aggiorna la baseline nella stessa PR**, col diff visibile. È l'unico modo legittimo di «peggiorare».

### Rigenerare la baseline

È un atto esplicito, mai implicito:

```bash
cd apps/api/tests/Api.Tests
EXTRACTION_BENCH_UPDATE_BASELINE=1 dotnet test --filter "FullyQualifiedName~DocnetExtractionBenchTests"
git diff TestData/extraction-bench/      # leggi il diff prima di committare
```

Riscrive `TestData/extraction-bench/baseline.<estrattore>.json` nel sorgente. La CI **rifiuta** la variabile (`CI=true`).

## Dove gira

| Estrattore | Test | `Category` | Gate | Bloccante |
|---|---|---|---|---|
| Docnet | `DocnetExtractionBenchTests` | `Unit` + `PDF` | `dev-fast.yml` e `ci.yml` (deny-list: ciò che non è esplicitamente lento) | sì |
| Unstructured `fast` | `UnstructuredExtractionBenchTests` | `Integration` + `PDF` | `dev-async.yml` (shard `DocumentProcessing`) e `ci.yml` | sì, **quando c'è la baseline** |
| Unstructured `hi_res` | idem | idem | idem | no (informativo) |

Le colonne Unstructured sono **L2/L3**: servizio opzionale. Senza `TEST_UNSTRUCTURED_URL` saltano con un motivo `PREVISTO:` che dice come accenderle. Con la variabile impostata e `/health` che non risponde, **falliscono**: l'assenza si accerta con una sonda, non si deduce.

Per eseguirle:

```bash
cd infra && make dev                    # accende unstructured-service
export TEST_UNSTRUCTURED_URL=http://localhost:8001
EXTRACTION_BENCH_UPDATE_BASELINE=1 dotnet test --filter "FullyQualifiedName~UnstructuredExtractionBenchTests"   # prima volta: crea la baseline
```

> **Stato**: le colonne Unstructured non sono mai state eseguite (nessun host con `unstructured-service`, decisioni dell'epic #4433). Non hanno baseline: la crea chi ha il servizio. Il loro comportamento di salto è verificato.

## L'artifact

Ogni run scrive `extraction-bench-<estrattore>.json` in `EXTRACTION_BENCH_OUTPUT_DIR` (in locale, in `extraction-bench-results/` accanto ai binari di test). In CI `dev-fast.yml` imposta la cartella e carica l'artifact `extraction-bench-results`. Contiene, per fixture, le metriche, le pagine, i secondi e i secondi per pagina.

## Dopo questo banco

La parte (b) di #4434 (gate RAG con l'estrattore in prova) è sotto condizione: serve un host con `unstructured-service` e un nuovo bake dello snapshot con una baseline dorata separata. Vedi il corpo di #4434.
