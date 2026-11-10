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
| `wordRecall` | quota delle parole distinte attese ritrovate a parola intera | maiuscole, tipografia — **vede le parole fuse** (le fondeva il passo 3 di `NormalizeText`, tolto in #4932; vedi sotto) |
| `readingOrder` | quota delle coppie consecutive di indicatori nell'ordine giusto | spazi |
| `tableCellRecall` | quota delle celle attese presenti nel testo (contenuto, non struttura) | spazi |
| `tableCellF1` | F1 sulle celle come multiinsieme (struttura) | — |
| `headingRecall` | quota dei titoli attesi fra i titoli riconosciuti | spazi |
| `scanWordRecall` | quota delle parole della pagina scansionata restituite | maiuscole, tipografia |
| secondi per pagina | solo nell'artifact; **mai asseriti** (precedenti #3601, #4274) | |

### Criterio di match

Uguale per tutti gli estrattori (`ExtractionTextMatcher`): NFKC (le ligature tornano lettere), minuscole, trattino morbido tolto, trattini e apici tipografici ricondotti ai semplici. **Le diacritiche restano**: «über»/«uber» e «perché»/«perche» sono parole diverse. Per `paragraphRecall`, `readingOrder`, `tableCellRecall` e `headingRecall` anche gli spazi sono ignorati: fino a #4932 Docnet restituiva il testo dopo `PdfTextProcessingDomainService.NormalizeText`, il cui **passo 3** (`([a-z])\n([a-z])` → `$1$2`) incollava due righe consecutive quando la prima finiva e la seconda cominciava con una minuscola ASCII («die folgende» → «diefolgende»). Non lo faceva pdfium: letto il testo grezzo sulla stessa fixture, le righe arrivano separate (`\r\n`) e le fondeva la normalizzazione. Il passo è tolto, ma un confronto sensibile agli spazi darebbe ancora zero a un testo che c'è tutto se un estrattore o una normalizzazione tornassero a perderne. Le parole fuse si misurano a parte con `wordRecall`.

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

### Se un run su Linux (o un nuovo runner) sposta un numero

La tolleranza è zero e la baseline di Docnet è nata su Windows; il primo run di CI su Linux ha dato valori identici, ma un cambio di versione di Docnet, di QuestPDF o dell'immagine del runner può cambiarli. **Non si allarga la tolleranza**: si rigenera la baseline con i valori misurati su Linux, che l'artifact `extraction-bench-results` del run rosso contiene già:

```bash
# dal run rosso: scarica l'artifact, poi
jq -S '{extractor, fixtures: (.fixtures | map_values(.metrics))}' extraction-bench-docnet.json \
  > apps/api/tests/Api.Tests/TestData/extraction-bench/baseline.docnet.json
```

La conversione riproduce la baseline committata (verificata sui valori; `-S` ordina le chiavi, quindi il diff può mostrare anche un riordino). Il diff è un cambio di requisito e si tratta come tale: la descrizione della PR dice quale metrica si è mossa, di quanto, e perché non è una regressione; **l'approvazione è del revisore della PR**, che deve leggere quel diff. Una baseline rigenerata per far tornare verde un rosso non spiegato è il modo di far smettere di funzionare il banco.

## Dove gira

| Estrattore | Test | `Category` | Gate | Bloccante |
|---|---|---|---|---|
| Docnet | `DocnetExtractionBenchTests` | `Unit` + `PDF` | `dev-fast.yml` e `ci.yml` (deny-list: ciò che non è esplicitamente lento) | sì |
| Unstructured `fast` | `UnstructuredExtractionBenchTests` | `Integration` + `PDF` | `dev-async.yml` (shard `DocProcessing`) e `ci.yml` | sì; **con il servizio acceso e senza baseline fallisce** (vedi sotto) |
| Unstructured `hi_res` | idem | idem | idem | no (informativo) |

Le colonne Unstructured sono **L2/L3**: servizio opzionale. Senza `TEST_UNSTRUCTURED_URL` saltano con un motivo `PREVISTO:` che dice come accenderle. Con la variabile impostata e `/health` che non risponde, **falliscono**: l'assenza si accerta con una sonda, non si deduce.

**Senza baseline.** Con `TEST_UNSTRUCTURED_URL` impostata e il servizio sano, `fast` (bloccante) **fallisce** finché la sua baseline non esiste, con l'istruzione per crearla (`ExtractionBaselineGate.Enforce`): un banco bloccante senza baseline non misurerebbe niente. Chi ha già la variabile per altri test (`PdfExtractionRealBackendValidationTests` la legge dallo stesso nome) vedrà `fast` rosso fino a quella creazione. `hi_res`, informativo, senza baseline stampa un avviso e passa. Senza la variabile, entrambe le righe saltano.

**Salti dichiarati in `dev-async`.** Le due righe stanno nello shard `DocProcessing` di `dev-async.yml` (filtro `FullyQualifiedName~DocumentProcessing`), che nessun workflow configura con `TEST_UNSTRUCTURED_URL`: fanno quindi 2 salti `PREVISTO:`. `infra/fixtures/dev-async-shard-baseline.json` (voce `DocProcessing`) li dichiara per nome e `infra/scripts/shard-skip-assert.sh` fallisce su un salto non dichiarato, anche a conteggio invariato. `dev-async` non gira sulle PR, quindi l'omissione si sarebbe vista solo al primo run dopo il merge. Quando esisterà un host che le esegue, i due nomi si tolgono dalla baseline con `--update-baseline`.

Per eseguirle:

```bash
cd infra && make dev                    # accende unstructured-service
export TEST_UNSTRUCTURED_URL=http://localhost:8001
EXTRACTION_BENCH_UPDATE_BASELINE=1 dotnet test --filter "FullyQualifiedName~UnstructuredExtractionBenchTests"   # prima volta: crea la baseline
```

> **Stato**: le colonne Unstructured non sono mai state eseguite (nessun host con `unstructured-service`, decisioni dell'epic #4433). Non hanno baseline: la crea chi ha il servizio. Il loro comportamento di salto è verificato.

## Limiti noti

Dichiarati, non corretti: o servirebbe una decisione, o un lavoro più grande di questo banco. Dove la correzione era di poche righe è stata fatta (una metrica in baseline che il run non produce ora fa fallire il confronto; il sabotaggio permanente dell'atteso muove anche `wordRecall` e `tableCellRecall`).

- **La baseline non è un cricchetto.** Un miglioramento non alza il pavimento: il confronto è solo «non scendere», quindi un vantaggio guadagnato si può perdere fino al vecchio valore senza un rosso. E una metrica con `null` in baseline che diventa misurabile (per esempio `tableCellF1` quando #4439 farà arrivare le celle) passa per sempre senza forzare la rigenerazione, perché `ExtractionBaseline.Regressions` guarda il pavimento solo se non è `null`. Rimedio manuale: rigenerare la baseline quando una metrica migliora o diventa applicabile.
- **Cosa sorveglia davvero il sabotaggio dell'atteso.** `TheBench_SeesAWrongExpectation` verifica, per Docnet, `paragraphRecall`, `readingOrder`, `wordRecall` e il `tableCellRecall` di `text-table-it`. `scanWordRecall` e il `tableCellRecall` di `graphic-table-de` valgono 0 in baseline e **non possono scendere**: se Docnet cominciasse a leggere la scansione o la tabella disegnata il banco non fallirebbe, salirebbe soltanto (e il pavimento non si alza: vedi sopra). `headingRecall` e `tableCellF1` sono `null` per Docnet, quindi non sorvegliano niente.
- **Il match è permissivo.** Titoli e celle si cercano come sottostringa su testo senza spazi: «Oste» è stata trovata dentro «nello stesso turno» (di qui il test-guardia sulle fixture). `headingRecall` non ha precisione: un estrattore che etichetta tutto come `Title` ottiene 1. Il controllo d'inchiostro delle fixture (almeno 500 pixel scuri) non distingue il testo dai bordi.
- **Tolleranza zero, e il primo run su Linux.** Una frazione fra interi non cambia da una macchina all'altra; il rischio sta nei ritorni a capo, che dipendono dal layout di QuestPDF (font incorporato, versione). Il primo run su Linux è stato identico alla baseline di Windows. Se un domani si rompesse, il rimedio è la rigenerazione descritta sopra, non una tolleranza.
- **`wordRecall` misura il testo che arriva all'indice con Docnet, non pdfium.** Lo ha mostrato il passo 3 di `NormalizeText`, che incollava le parole a ogni ritorno a capo fra due minuscole ASCII, anche senza trattino (sulla fixture a due colonne «centre of / the table» diventava «ofthe»): `wordRecall` di Docnet stava fra 0,78 e 0,95. Con il passo tolto (#4932) vale 1 su tutte le fixture, e la baseline è a 1. Resta vero che la metrica sorveglia l'estrattore **insieme alla normalizzazione** che gli sta dietro: un passo che incolli di nuovo le parole la fa scendere. Una parola spezzata a metà senza trattino non si ricompone più (non c'è un indizio che la distingua da un confine di parola): è un limite dichiarato, non misurato dalle fixture.
- **I PDF si generano un render alla volta.** Due render QuestPDF sovrapposti nello stesso processo possono produrre un PDF con la mappa `ToUnicode` del font incorporato sbagliata: il file si apre e sembra giusto, ma pdfium ne rilegge una frazione delle parole, a volte nessuna. Era la causa del banco rosso a intermittenza in locale su Windows (#4933), non un difetto di Docnet: gli stessi byte, estratti due volte, davano lo stesso testo corto; lo stesso documento ri-renderizzato da solo dava tutto il testo. Le classi di test che chiamano QuestPDF, direttamente o tramite un formatter del prodotto, stanno nella collezione `QuestPdfRendering` (`TestHelpers/QuestPdfRenderingCollection.cs`, non parallela). **Una classe nuova che genera PDF va aggiunta alla collezione**, altrimenti il banco torna instabile senza che nessuna asserzione dica perché. Se in un run rosso vedi `wordRecall` molto basso su una fixture di testo, rigenera quel PDF da solo e confronta: è questo il sintomo.

  Lo stesso difetto è nel **prodotto** (#4950): `PdfExportFormatter`, `PdfReportFormatter` ed `ExportLedgerQueryHandler` renderizzano su thread di richiesta, e misurati a quattro thread perdevano tutto il testo di circa un documento su dieci, ciascuno dei tre (un thread: mai). Lì non c'è una collezione xUnit: i render passano da `Api.Infrastructure.QuestPdfRenderGate`, che li mette in fila, e `QuestPdfRenderGateArchitectureTests` vieta un `GeneratePdf` (o `GenerateImages`, `GenerateSvg`, `GenerateXps`) fuori da quel file. Un renderer di prodotto nuovo chiama il cancello, non `GeneratePdf`.

## L'artifact

Ogni run scrive `extraction-bench-<estrattore>.json` in `EXTRACTION_BENCH_OUTPUT_DIR` (in locale, in `extraction-bench-results/` accanto ai binari di test). In CI `dev-fast.yml` imposta la cartella e carica l'artifact `extraction-bench-results`. Contiene, per fixture, le metriche, le pagine, i secondi e i secondi per pagina.

## Dopo questo banco

La parte (b) di #4434 (gate RAG con l'estrattore in prova) è sotto condizione: serve un host con `unstructured-service` e un nuovo bake dello snapshot con una baseline dorata separata. Vedi il corpo di #4434.
