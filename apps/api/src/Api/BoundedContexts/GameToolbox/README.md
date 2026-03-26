# GameToolbox Bounded Context

> **Nota: Diverso da GameToolkit** — Toolbox e basato su template manuali con configurazione freeform/phased, Toolkit usa generazione AI con widget tipizzati.

## Responsabilita

Gestisce i "toolbox" di gioco: contenitori configurabili di strumenti (dadi, carte, contatori, timer) che gli utenti possono assemblare manualmente per supportare le sessioni di gioco. Supporta due modalita operative (Freeform e Phased), template riutilizzabili e un contesto condiviso tra giocatori (turni, round, proprieta custom).

## Funzionalita Principali

- **Toolbox CRUD**: Creazione, lettura, aggiornamento e soft-delete di toolbox per gioco
- **Gestione Strumenti**: Aggiunta, rimozione e riordinamento di tool all'interno del toolbox (DiceRoller, ScoreTracker, CardDeck, ecc.)
- **Modalita Operative**: Freeform (tutti gli strumenti attivi) e Phased (attivazione strumenti per fase)
- **Gestione Fasi**: Creazione, rimozione, riordinamento e avanzamento automatico delle fasi con wrap-around del round
- **Contesto Condiviso**: Stato condiviso tra tutti i tool (giocatori, turno corrente, round, proprieta custom)
- **Card Deck**: Creazione mazzi standard/custom, pescata, mescolamento e reset tramite adapter verso SessionTracking
- **Template**: Creazione di template riutilizzabili da toolbox esistenti, applicazione di template per istanziare nuovi toolbox
- **Adapter Legacy**: Mappatura dei widget GameToolkit verso tipi ToolboxTool tramite `ToolkitWidgetAdapter`

## Struttura

### Domain/
Logica di business pura e modelli di dominio:
- **Entities/**:
  - `Toolbox`: Aggregate root — contenitore per-gioco di strumenti con modalita Freeform/Phased
  - `ToolboxTool`: Strumento individuale con tipo, configurazione JSON, stato e ordine
  - `ToolboxTemplate`: Blueprint riutilizzabile per creare toolbox preconfigurati (sorgente: Manual, Community, AI)
  - `Phase`: Fase in modalita Phased che controlla quali strumenti sono attivi
- **ValueObjects/**:
  - `ToolboxMode`: Enum (Freeform, Phased)
  - `SharedContext`: Record immutabile con giocatori (`PlayerInfo`), indice giocatore corrente, round e proprieta custom
- **Repositories/**:
  - `IToolboxRepository`: Contratto per accesso dati Toolbox
  - `IToolboxTemplateRepository`: Contratto per accesso dati ToolboxTemplate

### Application/
Orchestrazione e casi d'uso (CQRS pattern con MediatR):
- **Commands/**:
  - Toolbox CRUD: `CreateToolboxCommand`, `UpdateToolboxModeCommand`
  - Tool Management: `AddToolToToolboxCommand`, `RemoveToolFromToolboxCommand`, `ReorderToolsCommand`
  - Shared Context: `UpdateSharedContextCommand`
  - Phases: `AddPhaseCommand`, `RemovePhaseCommand`, `ReorderPhasesCommand`, `AdvancePhaseCommand`
  - Card Deck: `CreateCardDeckCommand`, `ShuffleCardDeckCommand`, `DrawCardsCommand`, `ResetCardDeckCommand`
  - Templates: `CreateToolboxTemplateCommand`, `ApplyToolboxTemplateCommand`
- **Queries/**:
  - `GetToolboxQuery`, `GetToolboxByGameQuery`
  - `GetToolboxTemplatesQuery`, `GetAvailableToolsQuery`
- **DTOs/**: Data Transfer Objects (`ToolboxDto`, `ToolboxToolDto`, `PhaseDto`, `SharedContextDto`, `CardDrawResultDto`, ecc.)
- **Validators/**: FluentValidation validators per validazione comandi

### Adapters/
Adattatori per integrazione con altri bounded context:
- `CardDeckAdapter`: Facade verso `SessionTracking.SessionDeck` — espone solo Shuffle/Draw/Reset senza mani, scarti o visibilita per giocatore
- `ToolkitWidgetAdapter`: Mappatura statica dei widget GameToolkit (RandomGenerator, TurnManager, ecc.) verso tipi ToolboxTool

### Infrastructure/
Implementazioni concrete:
- **Persistence/**:
  - `ToolboxRepository`: Implementazione EF Core di `IToolboxRepository`
  - `ToolboxTemplateRepository`: Implementazione EF Core di `IToolboxTemplateRepository`
  - **Configurations/**: `ToolboxEntityConfiguration` — configurazione EF Core
- **DependencyInjection/**: `GameToolboxServiceExtensions` — registrazione servizi nel container DI

## Pattern Architetturali

- **CQRS**: Separazione tra Commands (scrittura) e Queries (lettura)
- **MediatR**: Tutti gli endpoint HTTP usano `IMediator.Send()` per invocare handlers
- **Domain-Driven Design**: Aggregate root (`Toolbox`), Value Objects (`SharedContext`, `ToolboxMode`), factory methods
- **Repository Pattern**: Astrazione dell'accesso ai dati con interfacce nel Domain
- **Soft Delete**: `IsDeleted` + `DeletedAt` su Toolbox e ToolboxTemplate
- **Adapter Pattern**: `CardDeckAdapter` per integrare SessionTracking, `ToolkitWidgetAdapter` per mappare widget legacy
- **Immutable Value Objects**: `SharedContext` come record con metodi `AdvancePlayer()` e `AdvanceRound()` che ritornano nuove istanze

## API Endpoints

```
# Toolbox CRUD
GET    /api/v1/toolboxes/{id}                              → GetToolboxQuery
GET    /api/v1/toolboxes/by-game/{gameId}                  → GetToolboxByGameQuery
GET    /api/v1/toolboxes/available-tools                   → GetAvailableToolsQuery
POST   /api/v1/toolboxes                                   → CreateToolboxCommand
PUT    /api/v1/toolboxes/{id}/mode                         → UpdateToolboxModeCommand

# Tool Management
POST   /api/v1/toolboxes/{id}/tools                        → AddToolToToolboxCommand
DELETE /api/v1/toolboxes/{id}/tools/{toolId}                → RemoveToolFromToolboxCommand
PUT    /api/v1/toolboxes/{id}/tools/reorder                → ReorderToolsCommand

# Shared Context
PUT    /api/v1/toolboxes/{id}/shared-context               → UpdateSharedContextCommand

# Card Deck
POST   /api/v1/toolboxes/{id}/card-decks                   → CreateCardDeckCommand
POST   /api/v1/toolboxes/{id}/card-decks/{deckId}/shuffle  → ShuffleCardDeckCommand
POST   /api/v1/toolboxes/{id}/card-decks/{deckId}/draw     → DrawCardsCommand
POST   /api/v1/toolboxes/{id}/card-decks/{deckId}/reset    → ResetCardDeckCommand

# Phases
POST   /api/v1/toolboxes/{id}/phases                       → AddPhaseCommand
DELETE /api/v1/toolboxes/{id}/phases/{phaseId}              → RemovePhaseCommand
PUT    /api/v1/toolboxes/{id}/phases/reorder               → ReorderPhasesCommand
POST   /api/v1/toolboxes/{id}/phases/advance               → AdvancePhaseCommand

# Templates
GET    /api/v1/toolbox-templates                           → GetToolboxTemplatesQuery
POST   /api/v1/toolbox-templates/{templateId}/apply        → ApplyToolboxTemplateCommand
```

## Modelli di Dominio

### Toolbox Aggregate
- **Identita**: Id (GUID)
- **Proprieta**: Name, GameId, TemplateId, Mode (Freeform/Phased), SharedContext, CurrentPhaseId
- **Collezioni**: Tools (ToolboxTool[]), Phases (Phase[])
- **Invarianti**:
  - Il nome e obbligatorio
  - AdvancePhase e valido solo in modalita Phased
  - Il wrap-around delle fasi avanza automaticamente il round

### ToolboxTool
- **Proprieta**: Type (stringa), Config (JSON), State (JSON), IsEnabled, Order
- **Operazioni**: Enable, Disable, UpdateConfig, UpdateState

### ToolboxTemplate
- **Proprieta**: Name, GameId, Mode, Source (Manual/Community/AI), ToolsJson, PhasesJson, SharedContextDefaultsJson
- **Soft Delete**: IsDeleted + DeletedAt

### Phase
- **Proprieta**: Name, Order, ActiveToolIds (lista di GUID)
- **Operazioni**: UpdateName, SetActiveTools, IsToolActive

## Dipendenze

- **EF Core**: Persistence
- **MediatR**: CQRS orchestration
- **FluentValidation**: Input validation
- **SessionTracking BC**: Integrazione card deck via `CardDeckAdapter` (dipendenza su `ISessionDeckRepository`)
- **GameToolkit BC**: Mappatura widget via `ToolkitWidgetAdapter`
