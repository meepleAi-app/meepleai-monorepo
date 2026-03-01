# EntityRelationships Bounded Context

## Responsabilità

Gestisce i collegamenti espliciti tra entità eterogenee del sistema (EntityLink).
Permette agli utenti di creare relazioni semantiche tra giochi, agenti, sessioni, eventi e altri tipi di entità.

## Funzionalità Principali

- **EntityLink**: Creazione, eliminazione e consultazione di link tra entità
- **Tipologia Link**: 8 tipi semantici (expansion_of, sequel_of, reimplements, companion_to, related_to, part_of, collaborates_with, specialized_by)
- **Bidirezionalità**: Supporto a link directed e bilateral
- **Approvazione Admin**: Link scope=shared richiedono approvazione admin
- **Auto-approvazione**: Link scope=user approvati automaticamente (BR-04)
- **BGG Import**: Auto-creazione link da dati BGG expansion

## Struttura

### Domain/
Logica di business pura e modelli di dominio:
- **Aggregates/**: `EntityLink` aggregate root
- **Enums/**: `EntityLinkType`, `EntityLinkScope`, `MeepleEntityType`
- **Events/**: `EntityLinkCreated`, `EntityLinkDeleted`
- **Exceptions/**: `EntityLinkNotFoundException`, `DuplicateEntityLinkException`
- **Repositories/**: `IEntityLinkRepository`
- **Constants/**: `EntityRelationshipsConstants`

### Application/
Orchestrazione e casi d'uso (CQRS pattern con MediatR):
- **Commands/**: `CreateEntityLinkCommand`, `DeleteEntityLinkCommand`
- **Queries/**: `GetEntityLinksQuery`, `GetEntityLinkCountQuery`
- **Handlers/**: Handler per ogni command/query
- **Validators/**: FluentValidation validators
- **DTOs/**: `EntityLinkDto`, `EntityLinkCountDto`

### Infrastructure/
Implementazioni concrete e accesso ai dati:
- **Persistence/**: `EntityLinkRepository`, `EntityLinkEntityConfiguration`

## Business Rules

```
BR-01: Game auto-crea Toolkit 1:1 quando aggiunto alla library
BR-04: EntityLink scope=user → IsAdminApproved = true automaticamente
BR-05: EntityLink scope=shared → richiede ruolo Admin
BR-08: EntityLink è unico per (sourceType, sourceId, targetType, targetId, linkType)
```

## Link Type Taxonomy

| LinkType | Colore | Direzionalità |
|---|---|---|
| `expansion_of` | Amber | → directed |
| `sequel_of` | Blue | → directed |
| `reimplements` | Orange | → directed |
| `companion_to` | Green | ↔ bilateral |
| `related_to` | Slate | ↔ bilateral |
| `part_of` | Purple | → directed |
| `collaborates_with` | Indigo | ↔ bilateral |
| `specialized_by` | Violet | → directed |

## API Endpoints

- `GET/POST/DELETE /api/v1/library/entity-links` — User endpoints
- `GET/POST/DELETE /api/v1/admin/entity-links` — Admin endpoints

## Dipendenze

- `MeepleAiDbContext` (Infrastructure/Persistence)
- `MediatR` (Application/CQRS)
- `FluentValidation` (Application/Validators)
