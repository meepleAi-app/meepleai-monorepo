# ADR-048: SharedGame Soft Delete — Comportamento FK e Visibilità

**Status**: Accepted
**Date**: 2026-03-30
**Deciders**: Tech Lead, Backend Team
**Spec-panel ref**: Spec-panel recommendation C-2, Adzic Scenario 3

## Context

`SharedGame` usa soft delete (`IsDeleted=true`, `HasQueryFilter(e => !e.IsDeleted)`).
Il FK `games.shared_game_id → shared_games.id` ha `OnDelete(SetNull)`.

**Problema**: `OnDelete(SetNull)` è un CASCADE DDL che si attiva solo su hard delete SQL.
Il soft delete (UPDATE `is_deleted=true`) NON triggera il cascade.
Risultato: `Game.SharedGameId` continua a puntare a un `SharedGame` soft-deleted,
che diventa "invisibile" alle query normali (global query filter lo esclude).

## Decision

**Cross-BC event handler per nullare SharedGameId su soft delete**

Al posto di usare un DB trigger o modificare il cascade policy, usiamo il domain event
`SharedGameDeletedEvent` (già esistente) + un handler in `GameManagement`
(`SharedGameSoftDeletedEventHandler`) che setta `Game.SharedGameId = null` su tutti i game linkati.
L'handler revoca anche l'approvazione se il game era stato pubblicato, mantenendo l'invariante
che un game approvato deve avere un riferimento a SharedGame.

Il hard-delete via FK `SetNull` rimane come safety net per eventuali hard-delete futuri.

## Consequences

- **Positivo**: Logica esplicita e testabile. Nessun trigger DB.
- **Positivo**: `Game.SharedGameId` è sempre `null` dopo che un SharedGame è deleted.
- **Positivo**: L'invariante "approved requires SharedGame link" è mantenuta anche nell'uscita.
- **Negativo**: Finestra temporale: tra il soft-delete e il dispatch dell'event handler, il FK è "fantasma".
  Questa finestra è < 1ms (stesso request context con MediatR in-process).
- **Negativo**: Se il domain event handler fallisce, il FK rimane "fantasma". Mitigazione: aggiungere
  una query di manutenzione periodica `SELECT * FROM games WHERE shared_game_id IN (SELECT id FROM shared_games WHERE is_deleted=true)`.

## Regola per il futuro

Ogni soft-delete di una entità con FK entranti deve avere un domain event handler che gestisce
i record "orfani" negli altri BC.
