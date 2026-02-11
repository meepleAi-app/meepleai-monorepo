# GameManagement Bounded Context

**Catalogo giochi, sessioni di gioco, FAQ, e regole**

---

## 📋 Responsabilità

- Gestione catalogo giochi (CRUD operations)
- Sessioni di gioco (tracking partite)
- FAQ per giochi (domande frequenti)
- Integrazione con BoardGameGeek API
- Gestione regole e PDF regolamenti

---

## 🏗️ Domain Model

### Aggregates

**Game** (Aggregate Root):
```csharp
public class Game
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string? NameIt { get; private set; }
    public int MinPlayers { get; private set; }
    public int MaxPlayers { get; private set; }
    public int PlayingTime { get; private set; }
    public int MinAge { get; private set; }
    public int? BggId { get; private set; }          // BoardGameGeek ID
    public string? ImageUrl { get; private set; }
    public List<GameFaq> Faqs { get; private set; }
    public List<PlaySession> PlaySessions { get; private set; }

    public void AddFaq(string question, string answer) { }
    public void RecordPlaySession(int playerCount, DateTime date) { }
}
```

**PlaySession**:
```csharp
public class PlaySession
{
    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }
    public Guid UserId { get; private set; }
    public int PlayerCount { get; private set; }
    public DateTime PlayedAt { get; private set; }
    public int DurationMinutes { get; private set; }
}
```

**GameFaq**:
```csharp
public class GameFaq
{
    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }
    public string Question { get; private set; }
    public string Answer { get; private set; }
    public int ViewCount { get; private set; }

    public void IncrementViews() { }
}
```

---

## 📡 Application Layer

### Commands

| Command | Description | Endpoint |
|---------|-------------|----------|
| `CreateGameCommand` | Crea nuovo gioco | `POST /api/v1/games` |
| `UpdateGameCommand` | Aggiorna gioco | `PUT /api/v1/games/{id}` |
| `DeleteGameCommand` | Elimina gioco (soft-delete) | `DELETE /api/v1/games/{id}` |
| `ImportFromBggCommand` | Importa da BoardGameGeek | `POST /api/v1/games/import-bgg` |
| `RecordPlaySessionCommand` | Registra partita | `POST /api/v1/games/{id}/sessions` |
| `AddGameFaqCommand` | Aggiungi FAQ | `POST /api/v1/games/{id}/faqs` |

### Queries

| Query | Description | Endpoint |
|-------|-------------|----------|
| `GetAllGamesQuery` | Lista giochi (paginated) | `GET /api/v1/games` |
| `GetGameByIdQuery` | Dettagli gioco | `GET /api/v1/games/{id}` |
| `SearchGamesQuery` | Ricerca giochi (full-text) | `GET /api/v1/games/search?q=catan` |
| `GetGameFaqsQuery` | FAQ gioco | `GET /api/v1/games/{id}/faqs` |
| `GetPlaySessionsQuery` | Storico partite | `GET /api/v1/games/{id}/sessions` |

---

## 🔄 Integration Points

### Inbound Dependencies

**KnowledgeBase Context**:
- Fetch game metadata per chat context

**UserLibrary Context**:
- Game references for user collections

**DocumentProcessing Context**:
- Associate PDF rulebooks with games

### Outbound Events

**GameCreatedEvent**:
```csharp
public class GameCreatedEvent : INotification
{
    public Guid GameId { get; init; }
    public string Name { get; init; }
}
```

**GameDeletedEvent** (soft-delete):
```csharp
public class GameDeletedEvent : INotification
{
    public Guid GameId { get; init; }
    public DateTime DeletedAt { get; init; }
}
```

---

## 🎲 BoardGameGeek Integration

**BGG API Client**:
```csharp
public interface IBggClient
{
    Task<BggGameDto> GetGameAsync(int bggId);
    Task<List<BggGameDto>> SearchAsync(string query);
}
```

**Import Workflow**:
1. Search BGG by game name
2. Fetch game details (players, time, age, image)
3. Map BGG data to Game entity
4. Save with BggId reference
5. Schedule periodic updates (optional)

**Rate Limiting**: 1 request/2 seconds (BGG policy)

---

## 📊 Database Schema

**Tables**:
- `Games` - Game catalog
- `GameFaqs` - Game-specific FAQs
- `PlaySessions` - Partite registrate
- `GameRules` - PDF regolamenti metadata

**Full-Text Search**:
```sql
CREATE INDEX idx_games_fts ON Games
USING gin(to_tsvector('italian', Name || ' ' || COALESCE(Description, '')));
```

---

## 🧪 Testing

**Location**: `tests/Api.Tests/BoundedContexts/GameManagement/`

**Coverage**: 88%

**Key Tests**:
```csharp
CreateGame_ShouldValidateMinPlayers()
ImportFromBgg_ValidId_ShouldCreateGame()
SearchGames_FullText_ShouldFindMatches()
RecordPlaySession_ShouldIncrementPlayCount()
```

---

## 📂 Code Location

`apps/api/src/Api/BoundedContexts/GameManagement/`

---

## 📖 Related Documentation

- [Shared Catalog Context](./shared-game-catalog.md) - Community game database
- [User Library Context](./user-library.md) - User collections
- [BGG API Setup](../04-deployment/boardgamegeek-api-setup.md)

---

**Last Updated**: 2026-01-18
**Status**: ✅ Production
**External Integration**: BoardGameGeek API
