# ADR-096 — Il cancello RAG per possesso resta, e il diniego diventa una porta

**Date**: 2026-10-11
**Status**: Accepted. È la decisione D1 di #4155 (2026-10-10), integrata con gli emendamenti del 2026-10-11.
**Related**: [ADR-059](./adr-059-catalog-seed-legal-posture.md) (postura legale) · [`copyright-tier-rag.md`](../copyright-tier-rag.md) (livelli copyright) · [ADR-095](./adr-095-single-answer-pipeline-and-versioned-agent-profile.md) (la fetta 5 mette questo controllo al passo 1 della pipeline unica) · #443 (B5, il cancello) · #4137 (giochi privati) · #4155

## Decisione

1. **Il cancello resta.** Per un utente non admin, `RagAccessService.CanAccessRagAsync` concede la base di conoscenza di un gioco in quattro casi:
   - il gioco ha `IsRagPublic`;
   - l'utente ha dichiarato di possederlo (`UserLibraryEntry.OwnershipDeclaredAt`);
   - il gioco è un `PrivateGame` dell'utente;
   - l'utente è admin.

   In tutti gli altri casi la nega.
2. **Il diniego ha un tipo e un codice**, perché il client possa offrire la via d'uscita invece di mostrare un guasto:
   - `RagOwnershipRequiredException`, sottotipo di `ForbiddenException`, porta il `gameId`;
   - sulle risposte HTTP è un **403** con `error: "RAG_OWNERSHIP_REQUIRED"` e il `gameId`;
   - nello stream della chat è un evento `Error` con lo stesso codice e il `gameId`. Lì non può essere un 403, perché gli header SSE sono già partiti quando il cancello scatta;
   - c'è un solo punto che lo lancia, `RagAccessGuard.EnsureRagAccessAsync`. Lo usano gli otto handler che prima ripetevano lo stesso `throw`.
3. **La dichiarazione è esplicita, e raggiungibile da dove l'utente si trova:**
   - in chat, al primo diniego, con un riquadro che spiega il motivo e un pulsante; dichiarato il possesso, la domanda riparte da sola;
   - all'aggiunta di un gioco, dal catalogo o dall'onboarding, con una casella «Possiedo una copia» **spenta di default**;
   - nel dettaglio di un gioco in libreria che non ha accesso al manuale, con un invito. Il dettaglio porta `HasRagAccess`, calcolato fuori dalla sua cache.
4. **Un gioco posseduto ma senza manuale indicizzato** (`kbCardCount` 0 nella risposta della dichiarazione) **lo si dice**: non si rinvia una domanda che non può ricevere citazioni.

## Contesto

Il cancello esisteva (#443), ma la dichiarazione non era raggiungibile. L'unico pulsante stava in componenti che nessuna pagina montava. Un utente con un gioco in libreria riceveva, a ogni domanda, «An error occurred: Accesso RAG non autorizzato»: la funzione centrale dell'app non funzionava su nessun gioco del catalogo. La prova di controllo in #4155 ha mostrato che, dichiarato il possesso via API, la stessa domanda riceveva una risposta corretta con le citazioni di pagina. Mancava la porta d'ingresso, non la pipeline.

## Rapporto con i livelli copyright

Il cancello e i livelli di [`copyright-tier-rag.md`](../copyright-tier-rag.md) sono due difese diverse:

| | cancello (questo ADR) | livelli copyright |
|---|---|---|
| domanda | il recupero sulla KB di questo gioco può partire? | come si cita ciò che è stato recuperato? |
| granularità | per gioco | per frammento |
| chi decide | `RagAccessService` | `CopyrightTierResolver` |
| esito | si risponde, oppure diniego `RAG_OWNERSHIP_REQUIRED` | `Full` (citazione) oppure `Protected` (parafrasi) |

Superare il cancello non rende citabile tutto. Un frammento del manuale vale `Full` solo se l'utente l'ha caricato **e** possiede il gioco (regola 3 di `CopyrightTierResolver`). Il manuale caricato da altri resta `Protected` anche per chi ha dichiarato il possesso, e si parafrasa.

## Alternative scartate

- **Allentare il cancello e affidarsi alla parafrasi.** I livelli copyright difendono la risposta, non il recupero. Senza cancello, i manuali protetti entrerebbero nel prompt per chiunque, e l'unica difesa sarebbe ciò che il modello scrive. È una decisione di prodotto e legale separata, che richiederebbe un ADR proprio (vedi ADR-059).
- **Dichiarare il possesso automaticamente all'aggiunta in libreria.** Una wishlist o un gioco provato da un amico non sono un possesso. La dichiarazione resta un'azione dell'utente (decisione del 2026-10-11): la casella è spenta di default e spiega perché serve.

## Conseguenze

- **Positive**:
  - la chat funziona per chi possiede il gioco;
  - chi non l'ha dichiarato riceve una spiegazione e un pulsante, invece di un errore;
  - il diniego è un tipo, quindi la fetta 5 di ADR-095 lo riusa al passo 1 della pipeline unica.
- **Negative**:
  - un passo in più per l'utente;
  - la dichiarazione si basa sulla fiducia: non c'è prova d'acquisto.
- **Neutre**:
  - per un gioco con licenza libera o pubblico dominio l'admin può impostare `IsRagPublic`, e il cancello non scatta;
  - per il gioco privato di un altro utente il codice è lo stesso, e non c'è via d'uscita: quel gioco non è tuo.

## Implementazione

- #4196: backend. Eccezione, controllo unico, ramo del middleware, evento di stream.
- #4197: chat. Riquadro, dialogo, rinvio della domanda, caso «senza manuale».
- #4198: casella «Possiedo una copia» nel catalogo e nell'onboarding.
- Il dettaglio del gioco, ultimo passo di #4155: `HasRagAccess` nel DTO e invito alla dichiarazione. In più, l'evento `OwnershipDeclaredEvent` invalida la cache del dettaglio, che altrimenti mostrava lo stato vecchio della voce fino a 5 minuti dopo la dichiarazione.
