# Happy Path — Template scenario & legenda

> Ogni catalogo (`U1…A5`) copia questo formato. Solo **happy path** (percorso di successo). Nessuno scenario negativo/errore/edge.

## Struttura di un catalogo

1. **Intestazione**: area, prerequisiti dati (seed), utente/i usati.
2. **Matrice di copertura**: ogni route dell'area → scenario ID · oppure `smoke-aggregato` (coperta dentro uno smoke cumulativo) · oppure `skip: <motivo>`.
3. **Scenari** in ordine, ognuno col template sotto.

## Template scenario

```gherkin
Scenario <AreaID>-NN [Flow|Smoke]: <titolo breve>
  Given <stato/precondizione con dati concreti dal seed>
    And <…>
  When <azione utente nel browser>
    And <…>
  Then <esito atteso osservabile>
    And <…>
  Osservabile ✅: <lista concreta di ciò che a schermo conferma il pass>
  Route: <path/i coinvolti>
  Utente: <admin | marco | sara | multi-utente …>
```

## Due livelli

- **Flow** — flusso transazionale multi-step; l'happy path è l'intero flusso end-to-end.
- **Smoke** — vista prevalentemente read-only. Criterio: la pagina **carica senza errori 4xx/5xx non attesi (Network) né errori JS (Console)** · **skeleton → contenuto reale (o empty-state legittimo)** · **l'azione primaria (tab, filtro, apertura dettaglio) produce un effetto visibile a schermo**.

## Criterio di pass

- **✅ pass** — tutti gli `Osservabile` sono veri a schermo, nessun errore Console/Network non atteso.
- **❌ fail** — un osservabile è falso o compare un errore non atteso.
- **⚠️ blocked-env** — l'ambiente impedisce l'esecuzione (dato mancante, servizio giù, gate ambientale). Distinto da fail; **non** blocca il gate locale→staging.
- **⏭️ pending** — non ancora eseguito.

## Convenzioni dati

- Dati di seed: nomi giochi/utenti reali da `infra/scripts/seed-sp4/data.json` (giochi: Azul, Catan, …; utenti: `marco|sara|luca|giulia|andrea@meepleai.test`, admin da `admin.secret`).
- Entità **create** da uno scenario Flow: marcatore `HP-TEST-<data>` nel titolo/nome (es. `HP-TEST-2026-07-10 Serata Azul`), per ripetibilità e cleanup.
- Osservabili basati su **struttura** (presenza elemento, navigazione, chip, empty-state), non su testo letterale generato da LLM.

## Esempio (U4)

```gherkin
Scenario U4-03 [Flow]: Risposta citata su una regola di gioco
  Given sono loggato come marco@meepleai.test (premium, verificato)
    And il gioco "Azul" ha un PDF regole indicizzato (seed KB)
  When apro /library/{azulId}/agent e invio "Quanti punti vale una riga completa?"
  Then entro ~10s vedo la risposta in streaming (SSE)
    And contiene ≥1 citazione cliccabile tipo [Azul, p.N]
    And il click sulla citazione apre il PDF alla pagina citata
  Osservabile ✅: testo risposta non-vuoto + ≥1 chip citazione + apertura PDF
  Route: /library/[gameId]/agent
  Utente: marco
```

## Pattern ciclo CRUD (crea/salva/edita/cancella)

Per le entità gestibili, uno scenario Flow di **ciclo di vita** verifica la persistenza reale (spec §3.1). La chiave è il **reload** dopo ogni operazione:

```gherkin
Scenario <AreaID>-NN [Flow]: Ciclo CRUD <entità> (crea → edita → cancella)
  Given sono loggato come <utente>
  When creo <entità> "HP-TEST-<data> …" (compilo il form → Salva)
  Then l'entità appare in lista
    And dopo reload della pagina l'entità è ancora presente (persistita)
  When apro l'entità e modifico un campo → Salva
  Then il valore aggiornato è a schermo
    And dopo reload il nuovo valore persiste
  When cancello l'entità (Elimina → conferma)
  Then l'entità sparisce dalla lista
    And dopo reload resta assente
  Osservabile ✅: entità presente post-create+reload · valore modificato post-edit+reload · assente post-delete+reload
  Route: <path>
  Utente: <utente>
  Dati creati: "HP-TEST-<data> …" (rimossa a fine ciclo)
```

Se la UI **non** espone tutte le operazioni (es. solo create+edit, nessun delete), lo scenario copre ciò che esiste e annota esplicitamente le operazioni assenti (niente Delete inventati).
