# GameToolkit Bounded Context

> **Nota: Diverso da GameToolbox** — Toolkit usa generazione AI con widget tipizzati, versionamento e template marketplace. Toolbox e basato su template manuali con configurazione freeform/phased.

## Responsabilita

Gestisce i toolkit di gioco con due modelli complementari:

1. **GameToolkit** (admin/editor): Aggregate root versionato per la configurazione avanzata di strumenti per gioco — dadi, carte, timer, contatori, template di punteggio e turno, con supporto alla generazione AI da Knowledge Base e un marketplace di template con workflow di review.
2. **Toolkit/ToolkitWidget** (user-facing): Dashboard di widget per-gioco con 6 tipi predefiniti (RandomGenerator, TurnManager, ScoreTracker, ResourceManager, NoteManager, Whiteboard). Default condiviso auto-creato quando un gioco viene aggiunto alla libreria, con possibilita di override per-utente.

## Funzionalita Principali

### GameToolkit (Admin)
- **CRUD Toolkit**: Creazione, aggiornamento, pubblicazione con versionamento incrementale
- **Strumenti Tipizzati**: Dadi (D4-D20, custom), carte (deck con zone e orientamento), timer (countdown/count-up/chess), contatori (min/max/per-player)
- **Override Flags**: Possibilita di nascondere strumenti base della sessione (turno, punteggio, dadi)
- **Template di Punteggio**: Dimensioni multiple, unita configurabili, tipi di punteggio (Points, ecc.)
- **Template di Turno**: Tipi di ordine turno (RoundRobin, ecc.) con fasi
- **State Template**: Definizioni JSON-schema per bootstrappare toolkit di giochi popolari
- **Agent Config**: Configurazione per agenti AI integrati
- **Template Marketplace**: Workflow Draft → PendingReview → Approved/Rejected con review admin
- **Generazione AI**: Generazione draft toolkit da chunk della Knowledge Base (`GenerateToolkitFromKbCommand`)
- **Applicazione Suggerimenti AI**: Revisione e applicazione delle suggestion AI (`ApplyAiToolkitSuggestionCommand`)
- **Clone da Template**: Clonazione di template approvati per nuovi giochi

### Toolkit Dashboard (User)
- **Default Auto-Creato**: Quando un gioco viene aggiunto alla libreria utente, un Toolkit default con tutti i 6 widget abilitati viene creato automaticamente (BR-01, event handler su `GameAddedToLibraryEvent`)
- **Override Per-Utente**: Il default e read-only; per personalizzare, l'utente clona in un override personale (BR-02)
- **Gestione Widget**: Abilitazione/disabilitazione, configurazione JSON per-widget
- **Unicita**: Al massimo un Toolkit attivo per coppia (GameId, OwnerUserId) (BR-03)

## Struttura

### Domain/
Logica di business pura e modelli di dominio:
- **Entities/**:
  - `GameToolkit`: Aggregate root (sealed, internal) — toolkit versionato per admin/editor con collezioni di tool config (dadi, carte, timer, contatori), template e marketplace workflow. Eredita da `AggregateRoot<Guid>`
  - `Toolkit`: Aggregate root — dashboard widget per-utente con default condiviso e meccanismo di override/clone
  - `ToolkitWidget`: Entita figlia — slot widget singolo con tipo, abilitazione, ordine e config JSON
  - Value Objects embedded: `DiceToolConfig`, `CardToolConfig`, `TimerToolConfig`, `CounterToolConfig`, `ScoringTemplateConfig`, `TurnTemplateConfig`, `StateTemplateDefinition`, `CardEntry`
- **Enums/**:
  - `WidgetType`: RandomGenerator, TurnManager, ScoreTracker, ResourceManager, NoteManager, Whiteboard
  - `DiceType`: D4, D6, D8, D10, D12, D20, Custom
  - `CardZone`, `CardOrientation`: Zone e orientamento carte
  - `ScoreType`: Tipi di punteggio
  - `TimerType`: CountDown, CountUp, Chess
  - `TurnOrderType`: Tipi di ordine turno (RoundRobin, ecc.)
  - `TemplateCategory`: Strategy, Party, CardGames, Cooperative
  - `TemplateStatus`: Draft, PendingReview, Approved, Rejected
- **Events/**:
  - `ToolkitCreatedEvent`, `ToolkitUpdatedEvent`, `ToolkitPublishedEvent`, `TimerExpiredEvent`
- **Repositories/**:
  - `IGameToolkitRepository`: Contratto per GameToolkit aggregate
  - `IToolkitRepository`: Contratto per Toolkit/ToolkitWidget aggregate

### Application/
Orchestrazione e casi d'uso (CQRS pattern con MediatR):
- **Commands/**:
  - GameToolkit CRUD: `CreateToolkitCommand`, `UpdateToolkitCommand`, `PublishToolkitCommand`
  - Tool Management: `AddDiceToolCommand`, `AddCardToolCommand`, `AddTimerToolCommand`, `AddCounterToolCommand`, `RemoveDice/Card/Timer/CounterToolCommand`
  - Templates: `SetScoringTemplateCommand`, `SetTurnTemplateCommand`, `SetStateTemplateCommand`, `ClearStateTemplateCommand`
  - Marketplace: `SubmitTemplateForReviewCommand`, `ApproveTemplateCommand`, `RejectTemplateCommand`, `CloneFromTemplateCommand`
  - AI: `GenerateToolkitFromKbCommand`, `ApplyAiToolkitSuggestionCommand`
  - Dashboard: `OverrideToolkitCommand`, `UpdateWidgetCommand`
- **Queries/**:
  - `GetToolkitQuery`, `GetToolkitsByGameQuery`, `GetToolkitsByPrivateGameQuery`, `GetPublishedToolkitsQuery`
  - `GetApprovedTemplatesQuery`, `GetPendingReviewTemplatesQuery`
  - `GetActiveToolkitQuery` (dashboard — ritorna default o override utente)
- **DTOs/**: `GameToolkitDto`, `ToolkitDashboardDto`, `AiToolkitSuggestionDto`, `CardEntryDto`, ecc.
- **Validators/**: FluentValidation validators per validazione comandi
- **EventHandlers/**:
  - `CreateDefaultToolkitWhenGameAddedHandler`: Reagisce a `GameAddedToLibraryEvent` per creare il toolkit default (BR-01), idempotente

### Infrastructure/
Implementazioni concrete:
- **Persistence/**:
  - `GameToolkitRepository`: Implementazione EF Core di `IGameToolkitRepository`
  - `ToolkitRepository`: Implementazione EF Core di `IToolkitRepository`
  - **Configurations/**:
    - `ToolkitEntityConfiguration`: Configurazione EF Core per Toolkit
    - `ToolkitWidgetEntityConfiguration`: Configurazione EF Core per ToolkitWidget
- **DependencyInjection/**: `GameToolkitServiceExtensions` — registrazione servizi nel container DI

## Pattern Architetturali

- **CQRS**: Separazione tra Commands (scrittura) e Queries (lettura)
- **MediatR**: Tutti gli endpoint HTTP usano `IMediator.Send()` per invocare handlers
- **Domain-Driven Design**: Aggregate roots (`GameToolkit`, `Toolkit`), Value Objects embedded, Domain Events, factory methods
- **Repository Pattern**: Astrazione dell'accesso ai dati con interfacce nel Domain
- **Event-Driven**: `CreateDefaultToolkitWhenGameAddedHandler` reagisce a domain events da UserLibrary
- **Versionamento**: `GameToolkit.Version` incrementato alla pubblicazione per immutabilita di sessione
- **Template Marketplace Workflow**: Draft → PendingReview → Approved/Rejected con review admin
- **Clone Pattern**: `Toolkit.Override(userId)` clona il default in un override per-utente
- **AI-Assisted Generation**: Generazione toolkit da KB chunks con review umana prima dell'applicazione

## API Endpoints

```
# GameToolkit CRUD
GET    /api/v1/game-toolkits/{id}                               → GetToolkitQuery
GET    /api/v1/game-toolkits/by-game/{gameId}                   → GetToolkitsByGameQuery
GET    /api/v1/game-toolkits/by-private-game/{privateGameId}    → GetToolkitsByPrivateGameQuery
GET    /api/v1/game-toolkits/published                          → GetPublishedToolkitsQuery
POST   /api/v1/game-toolkits                                    → CreateToolkitCommand
PUT    /api/v1/game-toolkits/{id}                               → UpdateToolkitCommand
POST   /api/v1/game-toolkits/{id}/publish                       → PublishToolkitCommand

# Dice Tools
POST   /api/v1/game-toolkits/{id}/dice-tools                   → AddDiceToolCommand
DELETE /api/v1/game-toolkits/{id}/dice-tools/{toolName}         → RemoveDiceToolCommand

# Card Tools
POST   /api/v1/game-toolkits/{id}/card-tools                   → AddCardToolCommand
DELETE /api/v1/game-toolkits/{id}/card-tools/{toolName}         → RemoveCardToolCommand

# Timer Tools
POST   /api/v1/game-toolkits/{id}/timer-tools                  → AddTimerToolCommand
DELETE /api/v1/game-toolkits/{id}/timer-tools/{toolName}        → RemoveTimerToolCommand

# Counter Tools
POST   /api/v1/game-toolkits/{id}/counter-tools                → AddCounterToolCommand
DELETE /api/v1/game-toolkits/{id}/counter-tools/{toolName}      → RemoveCounterToolCommand

# Templates
PUT    /api/v1/game-toolkits/{id}/scoring-template              → SetScoringTemplateCommand
PUT    /api/v1/game-toolkits/{id}/turn-template                 → SetTurnTemplateCommand
PUT    /api/v1/game-toolkits/{id}/state-template                → SetStateTemplateCommand
DELETE /api/v1/game-toolkits/{id}/state-template                → ClearStateTemplateCommand

# Template Marketplace
GET    /api/v1/game-toolkits/templates                          → GetApprovedTemplatesQuery
GET    /api/v1/game-toolkits/templates/pending-review           → GetPendingReviewTemplatesQuery (Admin)
POST   /api/v1/game-toolkits/{id}/submit-for-review             → SubmitTemplateForReviewCommand
POST   /api/v1/game-toolkits/{id}/approve                       → ApproveTemplateCommand (Admin)
POST   /api/v1/game-toolkits/{id}/reject                        → RejectTemplateCommand (Admin)
POST   /api/v1/game-toolkits/clone-from-template/{templateId}   → CloneFromTemplateCommand

# AI Generation
POST   /api/v1/game-toolkits/{id}/generate-from-kb              → GenerateToolkitFromKbCommand
POST   /api/v1/game-toolkits/{id}/apply-ai-suggestion           → ApplyAiToolkitSuggestionCommand
```

## Modelli di Dominio

### GameToolkit Aggregate (Admin)
- **Identita**: Id (GUID), eredita da `AggregateRoot<Guid>`
- **Proprieta**: GameId (o PrivateGameId, mutuamente esclusivi), Name, Version, CreatedByUserId, IsPublished
- **Override Flags**: OverridesTurnOrder, OverridesScoreboard, OverridesDiceSet
- **Collezioni**: DiceTools, CardTools, TimerTools, CounterTools (max 20 ciascuna)
- **Template Config**: ScoringTemplate, TurnTemplate, StateTemplate, AgentConfig
- **Marketplace**: TemplateStatus (Draft/PendingReview/Approved/Rejected), IsTemplate, ReviewNotes
- **Invarianti**:
  - Esattamente uno tra GameId e PrivateGameId deve essere valorizzato
  - Nome obbligatorio (max 200 caratteri)
  - Max 20 tool per tipo
  - Solo Draft/Rejected possono essere sottomessi per review
  - Solo PendingReview possono essere approvati/rifiutati

### Toolkit Aggregate (User Dashboard)
- **Identita**: Id (GUID)
- **Proprieta**: GameId, OwnerUserId (null per default), IsDefault, DisplayName
- **Collezioni**: Widgets (ToolkitWidget[]) — 6 widget predefiniti
- **Business Rules**:
  - BR-01: Default auto-creato con tutti i widget abilitati quando gioco aggiunto a libreria
  - BR-02: Default read-only, Override(userId) per personalizzare (clone)
  - BR-03: Al massimo un Toolkit attivo per (GameId, OwnerUserId)

### ToolkitWidget
- **Proprieta**: Type (WidgetType enum), IsEnabled, DisplayOrder, Config (JSON)
- **Operazioni**: Enable, Disable, UpdateConfig

## Dipendenze

- **EF Core**: Persistence
- **MediatR**: CQRS orchestration
- **FluentValidation**: Input validation
- **SharedKernel**: `AggregateRoot<T>`, `DomainEventBase`, `ICommand<T>`, `IQuery<T>`, `IUnitOfWork`
- **UserLibrary BC**: Reagisce a `GameAddedToLibraryEvent` per creare toolkit default
- **KnowledgeBase BC**: Lettura chunk KB per generazione AI toolkit (`GenerateToolkitFromKbCommand`)
