# Bounded Context Diagrams - Complete Index

**Visual architecture documentation for all 11 bounded contexts**

## 📊 Quick Navigation

| Context | Entities | Primary Command | Secondary Flow | Integration |
|---------|----------|-----------------|----------------|-------------|
| **UserLibrary** | [entities.mmd](./user-library/entities.mmd) | [Add Game](./user-library/flow-add-game.mmd) | [Upload PDF](./user-library/flow-upload-private-pdf.mmd) | [integration](./user-library/integration-flow.mmd) |
| **Administration** | [entities.mmd](./administration/entities.mmd) | [Suspend User](./administration/flow-suspend-user.mmd) | [Add Credits](./administration/flow-add-token-credits.mmd) | [integration](./administration/integration-flow.mmd) |
| **DocumentProcessing** | [entities.mmd](./document-processing/entities.mmd) | [Extract PDF](./document-processing/flow-extract-pdf.mmd) | [Upload PDF](./document-processing/flow-upload-pdf.mmd) | [integration](./document-processing/integration-flow.mmd) |
| **SharedGameCatalog** | [entities.mmd](./shared-game-catalog/entities.mmd) | [Approve Publication](./shared-game-catalog/flow-approve-publication.mmd) | - | [integration](./shared-game-catalog/integration-flow.mmd) |
| **SystemConfiguration** | [entities.mmd](./system-configuration/entities.mmd) | [Update Config](./system-configuration/flow-update-config.mmd) | [Tier Routing](./system-configuration/flow-tier-routing.mmd) | [integration](./system-configuration/integration-flow.mmd) |
| **UserNotifications** | [entities.mmd](./user-notifications/entities.mmd) | [Create Notification](./user-notifications/flow-create-notification.mmd) | [Get Notifications](./user-notifications/flow-get-notifications.mmd) | [integration](./user-notifications/integration-flow.mmd) |
| **WorkflowIntegration** | [entities.mmd](./workflow-integration/entities.mmd) | [Create n8n Config](./workflow-integration/flow-create-n8n-config.mmd) | [Test Connection](./workflow-integration/flow-test-connection.mmd) | [integration](./workflow-integration/integration-flow.mmd) |
| **SessionTracking** | [entities.mmd](./session-tracking/entities.mmd) | [Create Session](./session-tracking/flow-create-session.mmd) | [Roll Dice](./session-tracking/flow-roll-dice.mmd) | [integration](./session-tracking/integration-flow.mmd) |
| **Authentication** | [entities.mmd](./authentication/entities.mmd) | [Registration](./authentication/flow-registration.mmd) | [Login 2FA](./authentication/flow-login-2fa.mmd) | [integration](./authentication/integration-flow.mmd) |
| **GameManagement** | [entities.mmd](./game-management/entities.mmd) | - | - | - |
| **KnowledgeBase** | [entities.mmd](./knowledge-base/entities.mmd) | - | - | - |

## 🎯 Diagram Type Descriptions

### 1. Entity Relationship Diagrams (entities.mmd)

**Purpose**: Database schema and domain model visualization

**Key Elements**:
- Aggregates and entities
- Value objects
- Primary keys (PK), Foreign keys (FK), Unique keys (UK)
- Cardinality relationships
- Key properties for each entity

**Example Use Cases**:
- Understanding data model structure
- Planning database migrations
- Identifying relationships between entities
- Domain-Driven Design reference

### 2. Primary Command Flow Diagrams (flow-*.mmd)

**Purpose**: CQRS command execution visualization

**Key Elements**:
- HTTP request/response cycle
- MediatR pipeline (Command → Validator → Handler)
- Domain method invocations
- Database operations
- Event publishing
- Side effects

**Example Use Cases**:
- Understanding command execution flow
- Debugging business logic issues
- Implementing new commands
- Testing strategy planning

### 3. Secondary Flow Diagrams (flow-*.mmd)

**Purpose**: Additional important operations (queries, specialized commands)

**Key Elements**:
- Query execution patterns
- Caching strategies
- Background job processing
- Complex workflows
- External service integration

**Example Use Cases**:
- Understanding query optimization
- Planning caching strategies
- Debugging performance issues
- Integration testing design

### 4. Integration Flow Diagrams (integration-flow.mmd)

**Purpose**: Cross-context communication and dependencies

**Key Elements**:
- Event-driven communication (solid arrows)
- Direct dependencies (dashed arrows)
- External service integration
- Context boundaries
- Communication patterns

**Example Use Cases**:
- Understanding system architecture
- Planning new features across contexts
- Identifying event subscribers
- Debugging integration issues

## 🔍 Diagram Details by Context

### UserLibrary Context
**Aggregates**: UserLibraryEntry, Label, LibraryShareLink
**Key Flows**:
- Add game to library with quota enforcement
- Upload private PDF with SSE progress tracking
**Integrations**: GameManagement (game metadata), DocumentProcessing (PDF pipeline), UserNotifications (share link access)

### Administration Context
**Aggregates**: User (extended), AuditLog, TokenBalance, TokenTransaction, BatchJob, AlertRule, Alert
**Key Flows**:
- Suspend user with session invalidation
- Add token credits with transaction history
**Integrations**: All contexts (audit logging), KnowledgeBase (token tracking), UserNotifications (alerts)

### DocumentProcessing Context
**Aggregates**: PdfDocument, ExtractionAttempt, DocumentCollection, ChunkedUploadSession
**Key Flows**:
- 3-stage extraction pipeline (Unstructured → SmolDocling → Docnet)
- Upload PDF with background job processing
**Integrations**: UserLibrary (private PDFs), KnowledgeBase (RAG indexing), External Python services

### SharedGameCatalog Context
**Aggregates**: SharedGame, ShareRequest, DeleteRequest, Badge, UserBadge
**Key Flows**:
- Approve publication with badge system integration
**Integrations**: GameManagement (private games), DocumentProcessing (PDF processing), BoardGameGeek API

### SystemConfiguration Context
**Aggregates**: Configuration, AiModel, FeatureFlag, TierRouting, various Limits
**Key Flows**:
- Update configuration with versioning and cache invalidation
- Update tier routing for LLM model selection
**Integrations**: All contexts (runtime config), KnowledgeBase (model routing)

### UserNotifications Context
**Aggregates**: Notification
**Key Flows**:
- Create notification from domain events
- Get notifications with caching
**Integrations**: All contexts (event listeners), External SMTP/FCM

### WorkflowIntegration Context
**Aggregates**: N8NConfiguration, WorkflowErrorLog
**Key Flows**:
- Create n8n configuration with encryption
- Test connection with health check
**Integrations**: All contexts (webhook triggers), External n8n instance

### SessionTracking Context
**Aggregates**: GameSession, SessionNote, SessionDeck, DiceRoll, ScoreEntry
**Key Flows**:
- Create session with code generation
- Roll dice with cryptographic randomness
**Integrations**: GameManagement (game metadata), Redis (SSE pub/sub)

## 🎨 Viewing Recommendations

### VS Code
1. Install: **Markdown Preview Mermaid Support** extension
2. Open any `.mmd` file
3. Right-click → "Open Preview to the Side"

### IntelliJ/WebStorm
1. Enable built-in Mermaid support (Settings → Languages → Mermaid)
2. Open `.mmd` files
3. Preview pane shows rendered diagram

### Online
1. Visit [Mermaid Live Editor](https://mermaid.live)
2. Copy/paste diagram content
3. Export as PNG/SVG

## 🔗 Cross-Reference Links

### By Feature
- **PDF Processing**: DocumentProcessing [entities](./document-processing/entities.mmd), [extraction flow](./document-processing/flow-extract-pdf.mmd)
- **Token System**: Administration [entities](./administration/entities.mmd), [add credits flow](./administration/flow-add-token-credits.mmd)
- **Share Requests**: SharedGameCatalog [entities](./shared-game-catalog/entities.mmd), [approval flow](./shared-game-catalog/flow-approve-publication.mmd)
- **Real-Time Sessions**: SessionTracking [entities](./session-tracking/entities.mmd), [create session](./session-tracking/flow-create-session.mmd)

### By Integration Pattern
- **Event-Driven**: All [integration-flow.mmd](./user-library/integration-flow.mmd) diagrams
- **External Services**: [DocumentProcessing integration](./document-processing/integration-flow.mmd), [WorkflowIntegration](./workflow-integration/integration-flow.mmd)
- **Caching Patterns**: [SystemConfiguration flows](./system-configuration/flow-update-config.mmd), [UserNotifications queries](./user-notifications/flow-get-notifications.mmd)

### By Technical Pattern
- **CQRS**: All flow-* diagrams show MediatR pipeline
- **Soft Delete**: SharedGameCatalog [entities](./shared-game-catalog/entities.mmd)
- **Encryption**: SessionTracking [entities](./session-tracking/entities.mmd), WorkflowIntegration [config flow](./workflow-integration/flow-create-n8n-config.mmd)
- **SSE Streaming**: DocumentProcessing [upload flow](./document-processing/flow-upload-pdf.mmd), SessionTracking [dice roll](./session-tracking/flow-roll-dice.mmd)

---

**Created**: 2026-02-07
**Total Diagrams**: 42 .mmd files + 1 README + 1 INDEX
**Contexts Covered**: 8 new + 3 existing = 11 total
**Diagram Types**: Entities (8), Command Flows (11), Query Flows (3), Integration (8)
