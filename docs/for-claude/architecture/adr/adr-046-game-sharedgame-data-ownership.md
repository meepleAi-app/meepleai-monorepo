# ADR-046: Game vs SharedGame — Source of Truth per Gameplay Data

**Status**: Accepted
**Date**: 2026-03-30
**Deciders**: Tech Lead, Backend Team
**Spec-panel ref**: Spec-panel recommendation M-1

## Context

`Game` (GameManagement BC) e `SharedGame` (SharedGameCatalog BC) hanno entrambi campi gameplay sovrapposti:
`MinPlayers`, `MaxPlayers`, `YearPublished`, `Publisher`, `ImageUrl`.

Quando un `Game` è linkato a un `SharedGame` via `Game.SharedGameId`, non esiste una strategia di
sincronizzazione esplicita. Se un admin aggiorna `SharedGame.MinPlayers`, il `Game` linkato non viene
aggiornato automaticamente.

## Decision

**Opzione scelta: B — One-time enrichment copy (indipendenza post-link)**

Al momento del link (`Game.LinkToSharedGame()`), i dati di `SharedGame` NON vengono copiati in `Game`.
I dati di `Game` rimangono quelli inseriti dall'utente al momento della creazione.

`Game` è un'entità **privata e operativa** dell'utente. I suoi dati gameplay (MinPlayers, ecc.) riflettono
la copia fisica posseduta dall'utente, che può differire dal catalogo community (edizioni diverse, house rules).

`SharedGame` è un **artefatto community** — aggiornamenti riflettono la versione canonica del gioco.

## Consequences

- **Positivo**: `Game` è indipendente. L'utente controlla i suoi dati.
- **Positivo**: Nessuna sincronizzazione asincrona da implementare.
- **Negativo**: Un Game linkato può mostrare `MinPlayers=2` mentre il SharedGame mostra `MinPlayers=1` (edizioni diverse).
- **Mitigazione**: Le UI che mostrano dati community leggono da `SharedGame` direttamente via `Game.SharedGameId`; le UI che mostrano dati privati leggono da `Game`.

## Alternatives Considered

- **A (delegazione)**: `Game` usa i dati di `SharedGame` per i campi gameplay. Richiederebbe join o API cross-BC.
- **C (merge con override)**: `Game` ha campi override che sovrascrivono `SharedGame`. Complessità eccessiva per il valore aggiunto.

## Future

Se emerge la necessità di aggiornamento automatico, usare un domain event `SharedGameUpdatedEvent` →
handler in GameManagement che aggiorna i Game linkati con una strategia esplicita.
