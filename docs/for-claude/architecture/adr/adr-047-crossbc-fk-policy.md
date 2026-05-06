# ADR-047: Cross-BC Foreign Key Policy — Monolith Modulare

**Status**: Accepted
**Date**: 2026-03-30
**Deciders**: Tech Lead, Backend Team
**Spec-panel ref**: Spec-panel recommendation Sam Newman M-4

## Context

`Game` (GameManagement) ha un FK diretto verso `SharedGame` (SharedGameCatalog):
`games.shared_game_id → shared_games.id`.

`SharedGame` ha un FK verso `AgentDefinition` (KnowledgeBase):
`shared_games.agent_definition_id → agent_definitions.id`.

Questi FK cross-BC accoppiano gli schemi di database di bounded contexts separati.
In un'architettura a microservizi, questo sarebbe un anti-pattern.

## Decision

**Monolith modulare: FK cross-BC accettati a livello schema, vietati a livello codice**

Questo sistema è un **monolith modulare** con un singolo database condiviso. La separazione tra BC è
logica (namespace, cartelle, visibilità `internal`) — non fisica (database separati, API HTTP).

Regole:
1. FK cross-BC sono accettati nel database per performance e integrità referenziale.
2. I BC NON espongono repository o service direttamente agli altri BC. La comunicazione avviene via:
   - Domain events (MediatR `INotificationHandler`)
   - Query dirette al DB tramite il proprio `DbContext` (read-only)
3. Nessun microservizio è previsto nei prossimi 18 mesi.

## Consequences

- **Positivo**: Integrità referenziale garantita dal DB. Join efficienti.
- **Positivo**: Nessun overhead di comunicazione inter-servizio.
- **Negativo**: Se si decidesse di separare i BC in microservizi, i FK diventano blocking dependency.
- **Mitigazione**: Documentare ogni FK cross-BC con il tag `// cross-BC FK: [Motivo]` nel codice.

## Review Trigger

Riesaminare se: team supera 8 sviluppatori backend, o latenza DB > 50ms p95, o > 3 BC hanno FK tra loro.
