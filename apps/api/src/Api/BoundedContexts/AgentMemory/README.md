# AgentMemory Bounded Context

## Responsabilita

Gestisce la memoria persistente degli agenti AI relativa ai giochi, ai gruppi di gioco e ai giocatori. Fornisce il contesto necessario affinche gli agenti possano personalizzare le risposte in base alla storia di gioco, alle regole della casa e alle preferenze del gruppo.

## Funzionalita Principali

- **Game Memory**: Memoria per-gioco e per-utente contenente house rules, checklist di setup personalizzate e note
- **Group Memory**: Memoria collettiva di un gruppo di gioco con membri (registrati o guest), preferenze e statistiche aggregate
- **Player Memory**: Statistiche individuali per giocatore (vittorie, sconfitte, punteggi migliori) per ogni gioco
- **House Rules**: Regole della casa aggiunte manualmente o generate automaticamente dalla risoluzione di dispute
- **Guest Claim**: Possibilita per un utente registrato di reclamare un'identita guest e unificarne le statistiche
- **Event-Driven Stats**: Aggiornamento automatico delle statistiche alla conclusione di sessioni di gioco live
- **Feature Flag**: Il contesto e protetto dal feature flag `Features:AgentMemory.Enabled`

## Struttura

### Domain/

Logica di business pura e modelli di dominio:

- **Entities/**: Aggregate roots principali
  - `GameMemory`: Memoria per-gioco e per-owner con house rules, setup personalizzato e note
  - `GroupMemory`: Memoria collettiva di un gruppo con membri, preferenze e statistiche
  - `PlayerMemory`: Statistiche di un giocatore (utente registrato o guest), con supporto claim guest-to-user
- **Models/**: Modelli di dominio (owned types)
  - `HouseRule`: Regola della casa con descrizione, data e sorgente (UserAdded | DisputeOverride)
  - `MemoryNote`: Nota testuale con data e autore opzionale
  - `GroupMember`: Membro del gruppo (UserId per registrati, GuestName per ospiti)
  - `GroupPreferences`: Preferenze del gruppo (durata massima, complessita preferita, note)
  - `GroupStats`: Statistiche aggregate (sessioni totali, conteggio partite per gioco, ultima sessione)
  - `PlayerGameStats`: Statistiche per giocatore per singolo gioco (vittorie, sconfitte, punteggio migliore)
- **Enums/**:
  - `HouseRuleSource`: Origine della house rule (UserAdded, DisputeOverride)
  - `PreferredComplexity`: Livello di complessita preferito (Light, Medium, Heavy)
- **Repositories/**: Interfacce repository
  - `IGameMemoryRepository`: Accesso per GameId+OwnerId o per Id
  - `IGroupMemoryRepository`: Accesso per Id o per CreatorId
  - `IPlayerMemoryRepository`: Accesso per UserId, GuestName, GroupId con supporto multi-risultato

### Application/

Orchestrazione e casi d'uso (CQRS pattern con MediatR):

- **Commands/**: Operazioni di scrittura
  - `AddHouseRuleCommand`: Aggiunge una house rule a un gioco (crea GameMemory se non esiste)
  - `AddMemoryNoteCommand`: Aggiunge una nota alla memoria di un gioco
  - `CreateGroupMemoryCommand`: Crea un gruppo di gioco con membri iniziali (utenti e/o guest)
  - `UpdateGroupPreferencesCommand`: Aggiorna le preferenze di un gruppo
  - `ClaimGuestPlayerCommand`: Reclama un'identita guest per un utente registrato
- **Queries/**: Operazioni di lettura
  - `GetGameMemoryQuery`: Recupera la memoria di un gioco per un owner specifico
  - `GetGroupMemoryQuery`: Recupera i dettagli di un gruppo
  - `GetPlayerStatsQuery`: Recupera le statistiche di un giocatore su tutti i giochi
  - `GetClaimableGuestsQuery`: Trova le identita guest reclamabili da un utente
- **DTOs/**: Data Transfer Objects per le risposte API
  - `GameMemoryDto`, `HouseRuleDto`, `MemoryNoteDto`
  - `GroupMemoryDto`, `GroupMemberDto`, `GroupPreferencesDto`, `GroupStatsDto`
  - `PlayerMemoryDto`, `PlayerGameStatsDto`, `ClaimableGuestDto`
- **Validators/**: FluentValidation per ogni command
- **EventHandlers/**: Gestori di domain events cross-context
  - `OnDisputeOverriddenAddHouseRuleHandler`: Alla risoluzione di una disputa con verdetto override, aggiunge automaticamente la regola come house rule
  - `OnSessionCompletedUpdateStatsHandler`: Alla conclusione di una sessione live, aggiorna statistiche giocatore e gruppo

### Infrastructure/

Implementazioni concrete e adattatori:

- **Persistence/**: Implementazioni EF Core dei repository
  - `GameMemoryRepository`, `GroupMemoryRepository`, `PlayerMemoryRepository`
- **Entities/**: Entita EF Core per il mapping database
  - `GameMemoryEntity`, `GroupMemoryEntity`, `PlayerMemoryEntity`
- **Configurations/**: Configurazioni EF Core (Entity Type Configuration)
  - `GameMemoryEntityConfiguration`, `GroupMemoryEntityConfiguration`, `PlayerMemoryEntityConfiguration`
- **DependencyInjection/**: Registrazione servizi
  - `AgentMemoryServiceExtensions.AddAgentMemoryContext()`: Registra i 3 repository nel DI container

## Pattern Architetturali

- **CQRS**: Separazione tra Commands (scrittura) e Queries (lettura)
- **MediatR**: Tutti gli endpoint HTTP usano `IMediator.Send()` per invocare handlers
- **Domain-Driven Design**: Aggregate roots con factory methods (`Create`), private setters, owned types per i modelli
- **Repository Pattern**: Astrazione dell'accesso ai dati con interfacce nel Domain e implementazioni in Infrastructure
- **Event-Driven Architecture**: Reazione a domain events di altri bounded contexts (GameManagement) per aggiornamento automatico
- **Feature Flags**: Protezione del contesto tramite `IFeatureFlagService` per abilitazione graduale

## API Endpoints

```
POST   /api/v1/agent-memory/groups                              → CreateGroupMemoryCommand
GET    /api/v1/agent-memory/groups/{groupId}                    → GetGroupMemoryQuery
PUT    /api/v1/agent-memory/groups/{groupId}/preferences        → UpdateGroupPreferencesCommand
GET    /api/v1/agent-memory/games/{gameId}/memory               → GetGameMemoryQuery
POST   /api/v1/agent-memory/games/{gameId}/memory/house-rules   → AddHouseRuleCommand
POST   /api/v1/agent-memory/games/{gameId}/memory/notes         → AddMemoryNoteCommand
GET    /api/v1/agent-memory/players/me/stats                    → GetPlayerStatsQuery
GET    /api/v1/agent-memory/players/me/claimable-guests         → GetClaimableGuestsQuery
POST   /api/v1/agent-memory/players/me/claim-guest              → ClaimGuestPlayerCommand
```

Tutti gli endpoint richiedono autenticazione (`RequireAuthenticatedUser`).

## Modelli di Dominio

### GameMemory Aggregate

- **Identita**: Id (GUID), chiave composta logica GameId + OwnerId
- **Proprieta**: GameId, OwnerId, HouseRules (collection), CustomSetup, Notes (collection), CreatedAt
- **Invarianti**:
  - GameId e OwnerId non possono essere vuoti
  - Descrizione house rule e contenuto nota non possono essere vuoti

### GroupMemory Aggregate

- **Identita**: Id (GUID)
- **Proprieta**: Name, CreatorId, Members (collection), Preferences, Stats, CreatedAt
- **Invarianti**:
  - CreatorId non puo essere vuoto
  - Name non puo essere vuoto o whitespace
  - UserId membri non puo essere vuoto, GuestName non puo essere vuoto

### PlayerMemory Aggregate

- **Identita**: Id (GUID)
- **Proprieta**: UserId (nullable), GuestName (nullable), GroupId (nullable), GameStats (collection), ClaimedAt, CreatedAt
- **Invarianti**:
  - UserId non puo essere vuoto se presente
  - GuestName non puo essere vuoto per giocatori guest
  - Claim possibile solo se non gia reclamato (`UserId == null`)

## Dipendenze

- **EF Core**: Persistence (JSONB per modelli complessi)
- **MediatR**: CQRS orchestration e domain events
- **FluentValidation**: Validazione input per tutti i commands
- **SharedKernel**: `AggregateRoot<T>`, `IUnitOfWork`
- **GameManagement BC**: Eventi `StructuredDisputeResolvedEvent` e `LiveSessionCompletedEvent`, `ILiveSessionRepository`
- **IFeatureFlagService**: Abilitazione condizionale del contesto

## Related Documentation

- `docs/01-architecture/overview/system-architecture.md`
- `docs/03-api/board-game-ai-api-specification.md`
- `apps/api/src/Api/Routing/AgentMemoryEndpoints.cs`
