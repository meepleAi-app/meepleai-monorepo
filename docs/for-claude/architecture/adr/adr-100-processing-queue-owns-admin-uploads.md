# ADR-100 — La coda è l'unica proprietaria dell'elaborazione degli upload admin

**Date**: 2026-10-18
**Status**: Accepted. È la decisione A di #4443, presa il 2026-10-15 dopo una revisione a pannello, con la decisione 1a del 2026-10-16 sul tetto della coda.
**Related**: #4442 (prima consegna: il caricamento a pezzi) · #4443 (il resto) · #4395 (due copie dell'elaborazione) · #4445, #4446 (separate durante la revisione)

> Numerato fuori fascia: 080–099 è piena.

## Decisione

1. **Un documento ha al massimo una pipeline attiva.** Ogni percorso che elabora un PDF lo prende in carico in modo atomico **prima** di lavorarlo, con `IPdfClaimService.TryClaimPendingAsync` (`Pending → Extracting` in un solo `UPDATE`). Vale per il worker della coda, il task in background di `/ingest/pdf`, lo sblocco admin `process-pending` e il recupero all'avvio. Chi perde la presa in carico non fa nulla.
2. **Un job che trova il documento già preso non è un errore.** Il worker lo chiude `Cancelled`, con il motivo, non `Failed`. Lo schema degli stati non cambia.
3. **Chi elabora un upload dipende dal ruolo di chi carica**, letto dal database come per la quota:
   - **Editor e sopra**: un job in coda, mai un task in background. Vale per `/ingest/pdf`, il caricamento a pezzi, `AddRagToSharedGame`, l'upload in blocco, `UploadSharedGamePdf` e il `launch-processing` del wizard, che **accoda soltanto**.
   - **Gli altri utenti** (catalogo o gioco privato): il task in background, con la presa in carico del punto 1.
4. **La priorità si sceglie all'upload e vale davvero**: `normal` 10, `urgent` 30, come i valori del pannello della coda. È un parametro di `/ingest/pdf` (query) e del completamento a pezzi (body). Sotto Editor la richiesta riceve **403**. Il wizard accoda a 100.
5. **Un upload interattivo di un admin scavalca il tetto della coda** (`ProcessingJob.MaxQueueSize`), come già `PdfSeeder`: una reindicizzazione di massa o un blocco del seed non devono bloccare chi sta caricando.
6. **La coda piena ha un'eccezione propria**, `ProcessingQueueFullException` (**429**, `processing_queue_full`), distinta dalla `ConflictException` (409) di «il PDF ha già un job attivo». Chi considera il secondo un successo non deve inghiottire il primo.

## Contesto

Prima di questa decisione lo stesso documento poteva avere più proprietari:

| Percorso | Cosa faceva |
|---|---|
| Pannello di upload → `/ingest/pdf` | l'endpoint avviava un task in background, poi il pannello accodava lo stesso PDF |
| Wizard admin, `launch-processing` | accodava il job **e** eseguiva estrazione e indice nella richiesta |
| Caricamento a pezzi | elaborava con una propria copia della pipeline (#4442) |
| Task in background di `/ingest/pdf` | controllava `Pending` e poi scriveva `Uploading` in un passo separato |

Dal vivo (#4443, 2026-10-15): il job di un upload normale finiva `Failed` («SkippedNotClaimed») mentre il documento arrivava a `Ready`; un caricamento a pezzi è stato elaborato da due pipeline per circa cinque minuti, e una delle due ha perso la scrittura finale per un conflitto di concorrenza. Il quarto caso è il più insidioso: un altro proprietario che prendeva il documento nella finestra fra controllo e scrittura lasciava l'aggregato in `Extracting`, la transizione verso `Uploading` falliva, e il task marcava `Failed` un documento che qualcun altro stava elaborando.

## Alternative scartate

- **La coda anche per gli upload degli utenti.** Il task in background degli utenti gestisce prenotazione e rilascio della quota e il progresso mostrato in pagina. Portarlo sulla coda significa avere una sola pipeline, che è il lavoro di #4395. Per questa decisione basta che prenda in carico in modo atomico.
- **Lasciare l'accodamento al client.** Era il pannello ad accodare dopo l'upload, e così la coda diventava il secondo proprietario di un documento già in elaborazione.
- **Il tetto della coda anche per gli admin.** La transazione si annullerebbe dopo che il file è già nello storage, con un oggetto orfano e un messaggio incomprensibile per chi carica.

## Conseguenze

- Per un upload admin, `Ready` richiede che il worker della coda sia vivo. Il pannello della coda lo mostra, ordinato come il worker preleva: priorità decrescente, a parità i più recenti in cima.
- La presa in carico è un `UPDATE` diretto, quindi l'aggregato non emette gli eventi `Pending → Uploading → Extracting`. Il task in background pubblica in-process la transizione della presa in carico, per la cache della lista documenti di chi carica. La metrica della fase di upload non si registra più, come già non si registrava per la coda.
- I job chiusi `Failed` con «SkippedNotClaimed» prima di questa decisione restano nella coda. `POST /api/v1/admin/queue/close-not-claimed` li chiude `Cancelled`, solo su documenti `Ready`, con prova a vuoto di default. Nessuna migrazione di dati.
- `AddRagToSharedGame` non accoda più per conto suo: usa il job dell'upload, a priorità normale invece di 20.
- Il retry di un PDF con la coda piena lascia ancora il documento `Pending` senza job, come prima; ora il log lo dice invece di parlare di «job già attivo».

## Implementazione

#4442 (caricamento a pezzi) e #4443: `UploadPdfCommandHandler` (instradamento per ruolo, priorità, presa in carico nel task), `PdfProcessingQuartzJob` (esito `Cancelled`), `ProcessingQueueFullException`, `LaunchAdminPdfProcessingCommandHandler`, `GetProcessingQueueQueryHandler` (ordinamento), `CloseNotClaimedJobsCommand` e il pannello `upload-zone.tsx`.
