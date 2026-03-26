# UserLibrary Bounded Context

## Responsabilita

Gestisce la libreria personale di giochi da tavolo di ogni utente: collezioni, wishlist, sessioni di gioco, etichette personalizzate, condivisione della libreria, suggerimenti di giochi, checklist di setup e giochi privati. E il bounded context piu ampio del sistema (244+ file) e funge da giunzione tra utenti (Authentication) e giochi (GameManagement/SharedGameCatalog) con metadati specifici della libreria.

## Funzionalita Principali

- **Libreria Personale**: Aggiunta/rimozione giochi, note personali, preferiti, stato del gioco (Nuovo, Owned, InPrestito, Wishlist)
- **Giochi Privati**: Giochi non presenti nel catalogo condiviso, creati manualmente o importati da BGG, con possibilita di proposta al catalogo
- **Collezioni Generiche**: Sistema polimorfico per collezionare qualsiasi tipo di entita (Player, Event, Session, Agent, Document, ChatSession)
- **Wishlist**: Lista desideri con priorita, prezzo obiettivo, visibilita (pubblica/privata) e highlights
- **Sessioni di Gioco**: Registrazione partite con data, durata, esito (vittoria/sconfitta), giocatori e note
- **Etichette (Labels)**: Sistema di tagging con etichette predefinite di sistema e custom dell'utente, assegnabili ai giochi
- **Condivisione Libreria**: Link di condivisione sicuri con token crittografico, livello di privacy, scadenza e conteggio visite
- **Suggerimenti Giochi**: Raccomandazioni da altri utenti (flusso inviti admin), accettabili o rifiutabili
- **Checklist di Setup**: Passi di preparazione per ogni gioco, ordinabili e resettabili prima di ogni sessione
- **Configurazione Agente AI**: Configurazione personalizzata dell'agente AI per gioco, con possibilita di override o reset al default
- **Gestione PDF**: Upload PDF regolamento personalizzato per gioco, con associazione documenti privati
- **Dichiarazione Proprieta**: Dichiarazione esplicita di possesso per accesso RAG alla knowledge base del gioco
- **Quote Libreria**: Limiti configurabili sul numero di giochi in libreria per utente
- **Migrazione Proposte**: Flusso post-approvazione per la migrazione da gioco privato a catalogo condiviso

## Struttura

### Domain/
Logica di business pura e modelli di dominio:
- **Entities/**: Aggregates e entita del dominio
  - `UserLibraryEntry`: Aggregate root principale, giunzione utente-gioco con sessioni, checklist, labels, stato e statistiche
  - `PrivateGame`: Aggregate root per giochi privati (manuali o da BGG) con soft-delete e promozione a catalogo
  - `UserCollectionEntry`: Aggregate root per collezioni generiche polimorfiche
  - `WishlistItem`: Aggregate root per wishlist con priorita, prezzo target e visibilita
  - `LibraryShareLink`: Aggregate root per link di condivisione con token sicuro e scadenza
  - `GameSuggestion`: Aggregate root per suggerimenti giochi da altri utenti
  - `ProposalMigration`: Aggregate root per scelta di migrazione post-approvazione proposta
  - `GameSession`: Entita per sessioni di gioco registrate (durata, esito, giocatori)
  - `GameChecklist`: Entita per passi della checklist di setup
  - `GameLabel`: Entita per etichette (predefinite o custom)
  - `UserGameLabel`: Entita junction tra UserLibraryEntry e GameLabel
- **ValueObjects/**: Oggetti valore immutabili
  - `GameState`: Stato del gioco (Nuovo, Owned, InPrestito, Wishlist) con transizioni valide
  - `GameStats`: Statistiche di gioco (partite, durata media, percentuale vittorie)
  - `AgentConfiguration`: Configurazione custom dell'agente AI
  - `CustomPdfMetadata`: Metadati del PDF regolamento personalizzato
  - `LibraryNotes`: Note personali dell'utente
  - `LibrarySharePrivacyLevel`: Livello privacy del link condiviso
  - `CollectionMetadata`: Metadati flessibili per collezioni generiche
  - `WishlistPriority`: Priorita dell'elemento in wishlist
  - `WishlistVisibility`: Visibilita dell'elemento in wishlist
- **Enums/**: Enumerazioni
  - `EntityType`: Tipi di entita collezionabili (Player, Event, Session, Agent, Document, ChatSession)
  - `PrivateGameSource`: Sorgente del gioco privato (Manual, BoardGameGeek)
  - `PostApprovalMigrationChoice`: Scelta migrazione (Pending, LinkToCatalog, KeepPrivate)
- **Events/**: Domain events (15 eventi)
  - `GameAddedToLibraryEvent`, `GameRemovedFromLibraryEvent`
  - `GameSessionRecordedEvent`, `GameStateChangedEvent`
  - `OwnershipDeclaredEvent`
  - `PrivatePdfAssociatedEvent`, `PrivatePdfRemovedEvent`
  - `AgentLinkedToPrivateGameEvent`, `AgentUnlinkedFromPrivateGameEvent`
  - `ItemAddedToCollectionEvent`, `ItemRemovedFromCollectionEvent`
  - `WishlistItemAddedEvent`, `WishlistItemUpdatedEvent`, `WishlistItemRemovedEvent`
  - `GameSuggestionAcceptedEvent`
- **Repositories/**: Interfacce repository (8 contratti)
  - `IUserLibraryRepository`, `IUserCollectionRepository`, `IPrivateGameRepository`
  - `IWishlistRepository`, `IGameLabelRepository`, `ILibraryShareLinkRepository`
  - `IGameSuggestionRepository`, `IProposalMigrationRepository`
- **Services/**: Domain services
  - `IGameLibraryQuotaService`: Servizio per gestione limiti/quote della libreria
- **Constants/**: Costanti di dominio

### Application/
Orchestrazione e casi d'uso (CQRS pattern con MediatR):
- **Commands/**: Operazioni di scrittura
  - *Libreria Core*: AddGameToLibrary, RemoveGameFromLibrary, UpdateLibraryEntry, UpdateGameState, DeclareOwnership
  - *Sessioni*: RecordGameSession
  - *Agente AI*: ConfigureGameAgent, ResetGameAgent, SaveAgentConfig
  - *PDF*: UploadCustomGamePdf, ResetGamePdf, RemovePrivatePdf
  - *Condivisione*: CreateLibraryShareLink, UpdateLibraryShareLink, RevokeLibraryShareLink
  - *Prestito*: SendLoanReminder
  - *Labels*: AddLabelToGame, RemoveLabelFromGame, CreateCustomLabel, DeleteCustomLabel
  - *Collezioni*: AddToCollection, RemoveFromCollection, BulkAddToCollection, BulkRemoveFromCollection
  - *Wishlist*: AddToWishlist, RemoveFromWishlist, UpdateWishlistItem
  - *Giochi Privati*: AddPrivateGame, UpdatePrivateGame, DeletePrivateGame, ProposePrivateGame, LinkAgentToPrivateGame, UnlinkAgentFromPrivateGame
  - *Migrazioni*: HandleMigrationChoice
- **Queries/**: Operazioni di lettura
  - *Libreria*: GetUserLibrary, GetLibraryStats, GetLibraryQuota, GetGameInLibraryStatus, BatchCheckGamesInLibrary, GetGameDetail
  - *Giochi Utente*: GetUserGames
  - *Agente*: GetGameAgentConfig
  - *PDF*: GetGamePdfs
  - *Condivisione*: GetLibraryShareLink, GetSharedLibrary
  - *Labels*: GetLabels, GetGameLabels
  - *Wishlist*: GetWishlist, GetWishlistHighlights
  - *Checklist*: GetGameChecklist
  - *Wizard*: GetGameWizardPreview
  - *Giochi Privati*: GetPrivateGamesList, GetPrivateGame, GetMyProposals
  - *Migrazioni*: GetPendingMigrations
  - *Collezioni*: GetCollectionStatus, GetBulkCollectionAssociatedData
- **DTOs/**: Data Transfer Objects per le risposte
- **Validators/**: FluentValidation validators per comandi e query
- **EventHandlers/**: Gestori di domain events
  - `CreateProposalMigrationOnApprovalHandler`: Crea migrazione quando una proposta viene approvata
  - `GamePreAddedHandler`: Gestisce pre-aggiunta gioco
  - `GameSuggestedHandler`: Gestisce suggerimenti di gioco
  - `PrivatePdfRemovedEventHandler`: Pulizia vettori quando un PDF privato viene rimosso

### Infrastructure/
Implementazioni concrete e adattatori:
- **Persistence/**: Implementazioni EF Core dei repository (8 repository)
  - `UserLibraryRepository`, `UserCollectionRepository`, `PrivateGameRepository`
  - `WishlistRepository`, `GameLabelRepository`, `LibraryShareLinkRepository`
  - `GameSuggestionRepository`, `ProposalMigrationRepository`
- **Services/**: Implementazioni concrete di servizi
  - `GameLibraryQuotaService`: Implementazione limiti/quote libreria
- **DependencyInjection/**: Registrazione servizi nel container DI
  - `UserLibraryServiceExtensions`: Registra tutti i repository e servizi

## Pattern Architetturali

- **CQRS**: Separazione tra Commands (scrittura) e Queries (lettura)
- **MediatR**: Tutti gli endpoint HTTP usano `IMediator.Send()` per invocare handlers
- **Domain-Driven Design**: Aggregates, Value Objects, Domain Events, Factory Methods
- **Repository Pattern**: Astrazione dell'accesso ai dati con 8 repository specializzati
- **Soft Delete**: `IsDeleted` + `DeletedAt` su PrivateGame
- **Optimistic Concurrency**: `[Timestamp] RowVersion` su ProposalMigration
- **State Machine**: GameState con transizioni valide (Nuovo -> Owned -> InPrestito, etc.)
- **Polymorphic Collections**: UserCollectionEntry con EntityType per collezionare entita diverse

## API Endpoints

### Libreria Core (`/api/v1/library`)
```
GET    /library                                      -> GetUserLibraryQuery
GET    /library/stats                                -> GetLibraryStatsQuery
GET    /library/quota                                -> GetLibraryQuotaQuery
POST   /library/games/{gameId}                       -> AddGameToLibraryCommand
DELETE /library/games/{gameId}                       -> RemoveGameFromLibraryCommand
PATCH  /library/games/{gameId}                       -> UpdateLibraryEntryCommand
GET    /library/games/{gameId}                       -> GetGameDetailQuery
GET    /library/games/{gameId}/status                -> GetGameInLibraryStatusQuery
GET    /library/games/batch-status                   -> BatchCheckGamesInLibraryQuery
PUT    /library/games/{gameId}/state                 -> UpdateGameStateCommand
POST   /library/games/{gameId}/sessions              -> RecordGameSessionCommand
POST   /library/games/{gameId}/remind-loan           -> SendLoanReminderCommand
POST   /library/{gameId}/declare-ownership           -> DeclareOwnershipCommand
```

### Agente AI (`/api/v1/library/games/{gameId}`)
```
GET    /library/games/{gameId}/agent-config          -> GetGameAgentConfigQuery
PUT    /library/games/{gameId}/agent                 -> ConfigureGameAgentCommand
DELETE /library/games/{gameId}/agent                 -> ResetGameAgentCommand
POST   /library/games/{gameId}/agent-config          -> SaveAgentConfigCommand
POST   /library/games/{gameId}/agent                 -> ConfigureGameAgentCommand
```

### Toolkit (`/api/v1/library/games/{gameId}`)
```
GET    /library/games/{gameId}/toolkit               -> GetActiveToolkitQuery
PUT    /library/games/{gameId}/toolkit               -> UpdateToolkitCommand
PATCH  /library/games/{gameId}/toolkit/widgets/{type} -> UpdateWidgetCommand
```

### Checklist (`/api/v1/library/games/{gameId}`)
```
GET    /library/games/{gameId}/checklist             -> GetGameChecklistQuery
```

### PDF (`/api/v1/library`)
```
POST   /library/games/{gameId}/pdf                   -> UploadCustomGamePdfCommand
DELETE /library/games/{gameId}/pdf                   -> ResetGamePdfCommand
GET    /library/games/{gameId}/pdfs                  -> GetGamePdfsQuery
GET    /library/games/{gameId}/pdf-status            -> GetGamePdfIndexingStatusQuery
GET    /library/private-games/{gameId}/pdf-status    -> GetPrivateGamePdfIndexingStatusQuery
GET    /library/{entryId}/pdf/progress               -> GetPdfProgressQuery
DELETE /library/entries/{entryId}/private-pdf         -> RemovePrivatePdfCommand
```

### Condivisione Libreria (`/api/v1/library/share`)
```
POST   /library/share                                -> CreateLibraryShareLinkCommand
GET    /library/share                                -> GetLibraryShareLinkQuery
PATCH  /library/share/{shareToken}                   -> UpdateLibraryShareLinkCommand
DELETE /library/share/{shareToken}                   -> RevokeLibraryShareLinkCommand
GET    /library/shared/{shareToken}                  -> GetSharedLibraryQuery
```

### Labels (`/api/v1/library`)
```
GET    /library/labels                               -> GetLabelsQuery
POST   /library/labels                               -> CreateCustomLabelCommand
DELETE /library/labels/{labelId}                     -> DeleteCustomLabelCommand
GET    /library/games/{gameId}/labels                -> GetGameLabelsQuery
POST   /library/games/{gameId}/labels/{labelId}      -> AddLabelToGameCommand
DELETE /library/games/{gameId}/labels/{labelId}      -> RemoveLabelFromGameCommand
```

### Collezioni Generiche (`/api/v1/collections`)
```
GET    /collections/{entityType}/{entityId}/status   -> GetCollectionStatusQuery
POST   /collections/{entityType}/{entityId}          -> AddToCollectionCommand
DELETE /collections/{entityType}/{entityId}          -> RemoveFromCollectionCommand
POST   /collections/{entityType}/bulk-add            -> BulkAddToCollectionCommand
DELETE /collections/{entityType}/bulk-remove         -> BulkRemoveFromCollectionCommand
POST   /collections/{entityType}/bulk-associated-data -> GetBulkCollectionAssociatedDataQuery
```

### Wishlist (`/api/v1/wishlist`)
```
GET    /wishlist                                     -> GetWishlistQuery
GET    /wishlist/highlights                          -> GetWishlistHighlightsQuery
POST   /wishlist                                     -> AddToWishlistCommand
PUT    /wishlist/{id}                                -> UpdateWishlistItemCommand
DELETE /wishlist/{id}                                -> RemoveFromWishlistCommand
```

### Giochi Privati (`/api/v1/private-games`)
```
GET    /private-games                                -> GetPrivateGamesListQuery
POST   /private-games                                -> AddPrivateGameCommand
GET    /private-games/{id}                           -> GetPrivateGameQuery
PUT    /private-games/{id}                           -> UpdatePrivateGameCommand
DELETE /private-games/{id}                           -> DeletePrivateGameCommand
POST   /private-games/{id}/propose-to-catalog        -> ProposePrivateGameCommand
POST   /private-games/{id}/link-agent/{agentId}      -> LinkAgentToPrivateGameCommand
DELETE /private-games/{id}/unlink-agent              -> UnlinkAgentFromPrivateGameCommand
```

### Admin Config
```
GET    /admin/config/game-library-limits             -> GetGameLibraryLimitsQuery
PUT    /admin/config/game-library-limits             -> UpdateGameLibraryLimitsCommand
```

## Database Entities

Vedi `Infrastructure/Persistence/`:
- `UserLibraryEntry`: Giunzione utente-gioco con metadati, note, preferiti, stato, statistiche
- `PrivateGame`: Gioco privato con soft-delete e riferimento opzionale a BGG
- `UserCollectionEntry`: Collezione generica polimorfica con metadati flessibili
- `WishlistItem`: Elemento wishlist con priorita e prezzo target
- `LibraryShareLink`: Link condivisione con token sicuro e analytics
- `GameSuggestion`: Suggerimento gioco con stato accettazione/rifiuto
- `ProposalMigration`: Scelta migrazione post-approvazione con concurrency control
- `GameSession`: Sessione di gioco con durata, esito e giocatori
- `GameChecklist`: Passo checklist di setup con ordine di visualizzazione
- `GameLabel`: Etichetta (predefinita o custom) con nome e colore hex
- `UserGameLabel`: Junction table tra libreria e etichette

## Modelli di Dominio

### UserLibraryEntry Aggregate
- **Identita**: Id (GUID)
- **Proprieta**: UserId, GameId, AddedAt, Notes, IsFavorite, CurrentState, Stats, CustomAgentConfig, CustomPdfMetadata, PrivatePdfId, OwnershipDeclaredAt
- **Figli**: Sessions (GameSession[]), Checklist (GameChecklist[]), Labels (UserGameLabel[])
- **Invarianti**:
  - UserId e GameId non vuoti
  - Transizioni di stato validate (State Machine)
  - Non si puo dichiarare proprieta di un elemento in wishlist
  - Label non duplicabili sullo stesso gioco

### PrivateGame Aggregate
- **Identita**: Id (GUID)
- **Proprieta**: OwnerId, BggId, Title, YearPublished, Description, MinPlayers, MaxPlayers, PlayingTimeMinutes, MinAge, ComplexityRating, ImageUrl, ThumbnailUrl, Source, AgentDefinitionId
- **Invarianti**:
  - MinPlayers >= 1 e <= 100
  - MinPlayers <= MaxPlayers
  - ComplexityRating tra 1.0 e 5.0
  - Title obbligatorio (max 200 caratteri)
  - Sync BGG solo per giochi con Source = BoardGameGeek

### WishlistItem Aggregate
- **Identita**: Id (GUID)
- **Proprieta**: UserId, GameId, Priority, TargetPrice, Notes, Visibility
- **Invarianti**:
  - Notes max 500 caratteri
  - TargetPrice non negativo

### LibraryShareLink Aggregate
- **Identita**: Id (GUID)
- **Proprieta**: UserId, ShareToken (32 hex chars), PrivacyLevel, IncludeNotes, ExpiresAt, RevokedAt, ViewCount, LastAccessedAt
- **Invarianti**:
  - Token generato con RandomNumberGenerator (crittograficamente sicuro)
  - ExpiresAt deve essere nel futuro
  - Non modificabile dopo revoca

## Testing

- Unit tests per domain logic, invarianti e transizioni di stato
- Integration tests con Testcontainers (PostgreSQL)
- Test coverage: >90%

## Dipendenze

- **EF Core**: Persistence
- **MediatR**: CQRS orchestration
- **FluentValidation**: Input validation
- **Authentication BC**: Identita utente e sessione
- **GameManagement BC**: Riferimenti ai giochi del catalogo
- **SharedGameCatalog BC**: Promozione giochi privati a catalogo condiviso
- **DocumentProcessing BC**: Upload e indicizzazione PDF regolamento
- **KnowledgeBase BC**: Accesso RAG per giochi con proprieta dichiarata

## Related Documentation

- `docs/01-architecture/overview/system-architecture.md`
- `docs/03-api/board-game-ai-api-specification.md`
