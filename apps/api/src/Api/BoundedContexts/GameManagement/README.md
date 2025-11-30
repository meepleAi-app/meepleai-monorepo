# GameManagement Bounded Context

## Responsabilità

Gestisce il catalogo dei giochi da tavolo e le sessioni di gioco degli utenti.

## Funzionalità Principali

- **Catalogo Giochi**: Gestione completa dei giochi da tavolo
- **Metadati Giochi**: Nome, publisher, anno, numero giocatori, durata, complessità
- **Sessioni di Gioco**: Tracciamento delle partite giocate dagli utenti
- **Ricerca e Filtri**: Ricerca per nome, publisher, complessità, numero giocatori
- **BoardGameGeek Integration**: Import dati da BGG (opzionale)

## Struttura

### Domain/
Logica di business pura e modelli di dominio:
- **Entities/**: Aggregates principali (Game, PlaySession)
- **ValueObjects/**: Oggetti valore immutabili (GameTitle, PlayerCount, ComplexityRating)
- **Services/**: Domain services per logica complessa
- **Events/**: Domain events (GameCreated, PlaySessionStarted, etc.)

### Application/
Orchestrazione e casi d'uso (CQRS pattern con MediatR):
- **Commands/**: Operazioni di scrittura
  - CreateGame, UpdateGame, DeleteGame
  - StartPlaySession, EndPlaySession
  - ImportFromBGG
- **Queries/**: Operazioni di lettura
  - GetAllGames, GetGameById, SearchGames
  - GetGamesByFilters (players, complexity, duration)
  - GetUserPlaySessions, GetGamePlayHistory
- **DTOs/**: Data Transfer Objects per le risposte
- **Validators/**: FluentValidation validators per validare i comandi
- **EventHandlers/**: Gestori di domain events
- **Interfaces/**: Contratti per repositories e servizi
- **Services/**: Application services per orchestrazione

### Infrastructure/
Implementazioni concrete e adattatori:
- **Repositories/**: Implementazioni EF Core dei repository (GameRepository, PlaySessionRepository)
- **Services/**: Implementazioni concrete di servizi esterni (BGG API client)
- **Adapters/**: Adattatori per servizi di terze parti

## Pattern Architetturali

- **CQRS**: Separazione tra Commands (scrittura) e Queries (lettura)
- **MediatR**: Tutti gli endpoint HTTP usano `IMediator.Send()` per invocare handlers
- **Domain-Driven Design**: Aggregates, Value Objects, Domain Events
- **Repository Pattern**: Astrazione dell'accesso ai dati

## API Endpoints

```
GET    /api/v1/games                   → GetAllGamesQuery
GET    /api/v1/games/{id}              → GetGameByIdQuery
POST   /api/v1/games                   → CreateGameCommand
PUT    /api/v1/games/{id}              → UpdateGameCommand
DELETE /api/v1/games/{id}              → DeleteGameCommand
GET    /api/v1/games/search            → SearchGamesQuery
POST   /api/v1/games/import-bgg        → ImportFromBGGCommand
GET    /api/v1/play-sessions           → GetUserPlaySessionsQuery
POST   /api/v1/play-sessions           → StartPlaySessionCommand
PUT    /api/v1/play-sessions/{id}/end  → EndPlaySessionCommand
```

## Database Entities

Vedi `Infrastructure/Entities/GameManagement/`:
- `Game`: Gioco da tavolo con tutti i metadati
- `PlaySession`: Sessione di gioco con data, durata, giocatori, note

## Modelli di Dominio

### Game Aggregate
- **Identità**: GameId (GUID)
- **Proprietà**: Title, Publisher, Year, MinPlayers, MaxPlayers, PlayingTime, Complexity
- **Invarianti**:
  - MinPlayers ≤ MaxPlayers
  - PlayingTime > 0
  - Year deve essere ragionevole (es. 1900-presente)

### PlaySession Aggregate
- **Identità**: SessionId (GUID)
- **Proprietà**: GameId, UserId, StartDate, EndDate, PlayerCount, Notes
- **Invarianti**:
  - EndDate ≥ StartDate
  - PlayerCount entro i limiti del gioco

## Testing

- Unit tests per domain logic e invarianti
- Integration tests con Testcontainers (PostgreSQL)
- Test coverage: >90%

## Dipendenze

- **EF Core**: Persistence
- **MediatR**: CQRS orchestration
- **FluentValidation**: Input validation
- **AutoMapper**: DTO mapping (se usato)

## Note di Migrazione

Questo context è stato completamente migrato alla nuova architettura DDD/CQRS. Il legacy `GameService` (181 linee) è stato rimosso e sostituito con handlers specializzati.

## Related Documentation

- `docs/01-architecture/overview/system-architecture.md`
- `docs/03-api/board-game-ai-api-specification.md`
