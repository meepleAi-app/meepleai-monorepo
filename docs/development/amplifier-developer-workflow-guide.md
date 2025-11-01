# Microsoft Amplifier - Guida al Workflow di Sviluppo per MeepleAI

## Panoramica

**Microsoft Amplifier NON è un framework di produzione** - è un sistema di sviluppo metacognitivo che migliora il TUO workflow quando sviluppi MeepleAI con Claude Code.

**Cosa fa Amplifier**:
- ✅ Crea comandi slash personalizzati per task ripetitivi
- ✅ Automatizza workflow di sviluppo (design → code → test → deploy)
- ✅ Genera boilerplate code con best practices
- ✅ Design intelligence per UI components
- ✅ Gestione worktree Git per sviluppo parallelo

**Cosa NON fa**:
- ❌ Non si integra nel runtime di MeepleAI
- ❌ Non migliora gli agenti AI di produzione (usa Agent Lightning per quello)
- ❌ Non offre servizi/API per l'applicazione

## Quando Usare Amplifier

### ✅ Usa Amplifier Per (Workflow Sviluppo)

1. **Automatizzare Task Ripetitivi**
   - Generare boilerplate per nuovi services
   - Creare test templates
   - Setup infrastruttura standard

2. **Workflow Coordinati**
   - Document-Driven Development (design → implement → test)
   - Sviluppo parallelo con worktree
   - Code review automatizzato

3. **Design System**
   - Componenti UI con accessibility
   - Design tokens e style guides
   - Responsive layouts

### ❌ NON Usare Amplifier Per (Produzione)

1. **Ottimizzare Agenti AI**
   - ❌ RAG optimization
   - ❌ Prompt training
   - ❌ LLM fine-tuning
   - ✅ **Usa Agent Lightning invece**

2. **Runtime Services**
   - ❌ API endpoints
   - ❌ Database operations
   - ❌ Background jobs

## Setup Amplifier per MeepleAI

### Prerequisiti

```bash
# Verifica prerequisiti
python --version    # Need 3.11+
node --version      # Any version
pnpm --version      # Any version
git --version       # Any version
uv --version        # Python package manager
```

### Installazione

**Opzione 1: Workspace Pattern (Raccomandato per Progetti Lunghi)**

```bash
# Clone Amplifier separatamente
cd ~/dev-tools
git clone https://github.com/microsoft/amplifier.git
cd amplifier

# Aggiungi MeepleAI come submodule
git submodule add https://github.com/your-org/meepleai-monorepo.git projects/meepleai
cd projects/meepleai

# Install Amplifier dependencies
cd ../..
pnpm install
uv sync

# Activate environment
source .venv/bin/activate  # Linux/Mac
# .venv\Scripts\Activate.ps1  # Windows PowerShell

# Start Claude Code
claude-code
```

**Opzione 2: Amplifier dentro MeepleAI (Quick Start)**

```bash
cd D:/Repositories/meepleai-monorepo

# Clone Amplifier come submodule
git submodule add https://github.com/microsoft/amplifier.git tools/amplifier
cd tools/amplifier

# Install dependencies
pnpm install
uv sync

# Activate
source .venv/bin/activate

# Start Claude Code
claude-code
```

### Configurazione Progetto

```bash
# Crea AGENTS.md per context MeepleAI
cat > AGENTS.md << 'EOF'
# MeepleAI Development Context

## Project Overview
AI-powered board game rules assistant with RAG, vector search, and LLM integration.

## Stack
- **Backend**: ASP.NET Core 9.0, PostgreSQL, Qdrant, Redis
- **Frontend**: Next.js 14, React, TypeScript
- **AI**: OpenRouter, RAG, Semantic Kernel

## Architecture Patterns
- **Services**: Dependency Injection, async I/O
- **Database**: EF Core, migrations
- **Testing**: xUnit + Testcontainers (backend), Jest + Playwright (frontend)

## Naming Conventions
- C#: PascalCase for classes/methods, camelCase for parameters
- TypeScript: camelCase for variables/functions, PascalCase for components

## Quality Standards
- Test coverage: 90%+ for new code
- All services must have interfaces
- Always use `using` for IDisposable
- No `any` in TypeScript

## Common Tasks
See /meepleai:* commands for project-specific workflows.
EOF
```

## Creare Comandi Slash Personalizzati per MeepleAI

### Esempio 1: Generare Nuovo Service

Crea un comando `/meepleai:new-service` che genera boilerplate completo:

**Dì a Claude Code**:
```
/ultrathink-task Create a command /meepleai:new-service that generates a complete service for MeepleAI with:

1. Service interface (IMyService.cs)
2. Service implementation (MyService.cs)
3. Registration in Program.cs (DI)
4. Unit tests (MyServiceTests.cs)
5. Integration tests (MyServiceIntegrationTests.cs)

Follow these patterns:
- All services async
- Constructor injection
- ILogger<T> for logging
- Return types: Task<T> or Task
- Tests use xUnit + Moq

Example output for "UserPreferences" service:
- apps/api/src/Api/Services/IUserPreferencesService.cs
- apps/api/src/Api/Services/UserPreferencesService.cs
- apps/api/tests/Api.Tests/Unit/UserPreferencesServiceTests.cs
- apps/api/tests/Api.Tests/Integration/UserPreferencesEndpointsTests.cs

Prompt user for:
1. Service name (e.g., "UserPreferences")
2. Primary methods (e.g., "GetPreferences, UpdatePreferences")
3. Dependencies (e.g., "IDbContext, ILogger")
```

**Amplifier creerà il comando**, poi potrai usarlo:

```bash
# In Claude Code
/meepleai:new-service

# Amplifier chiederà:
# Service name: NotificationService
# Primary methods: SendNotification, GetNotificationHistory, MarkAsRead
# Dependencies: IDbContext, IEmailService, ILogger

# Output: Genererà tutti i file con boilerplate completo
```

### Esempio 2: Workflow Feature Completo

Crea workflow Document-Driven Development per nuove feature:

**Dì a Claude Code**:
```
/ultrathink-task Create a DDD workflow for MeepleAI features:

/meepleai:feature-start <feature-name>
  1. Create branch: feature/<feature-name>
  2. Create docs/issue/<feature-name>.md with template
  3. Ask for feature requirements
  4. Generate initial architecture plan

/meepleai:feature-design
  1. Review architecture in docs/issue/
  2. Generate API endpoints design
  3. Create database schema changes
  4. Update docs with design decisions
  5. Ask for approval before implementation

/meepleai:feature-implement
  1. Generate services (backend)
  2. Generate API endpoints
  3. Generate React components (frontend)
  4. Generate tests (unit + integration)
  5. Update CLAUDE.md with new endpoints

/meepleai:feature-test
  1. Run backend tests: dotnet test
  2. Run frontend tests: pnpm test
  3. Run E2E tests: pnpm test:e2e
  4. Check coverage: >90% required
  5. Generate test report

/meepleai:feature-complete
  1. Run all quality checks (lint, typecheck, test)
  2. Generate migration SQL if needed
  3. Update documentation
  4. Create PR description from docs/issue/
  5. Ready for code review
```

**Uso**:
```bash
# Start new feature
/meepleai:feature-start game-collections

# Design phase
/meepleai:feature-design
# (Claude genera design docs, tu approvi)

# Implementation
/meepleai:feature-implement
# (Genera tutto il codice con tests)

# Testing
/meepleai:feature-test
# (Esegue tutti i test e verifica coverage)

# Finalize
/meepleai:feature-complete
# (Prepara per PR)
```

### Esempio 3: Design System Components

**Dì a Claude Code**:
```
/ultrathink-task Create /meepleai:ui-component command for Next.js components:

Generate accessible React component with:
1. TypeScript component file (components/<name>.tsx)
2. CSS module (components/<name>.module.css)
3. Jest test (components/__tests__/<name>.test.tsx)
4. Storybook story (components/<name>.stories.tsx)
5. Accessibility compliance (WCAG 2.1 AA)

Follow patterns:
- Tailwind CSS for styling
- shadcn/ui conventions
- Radix UI primitives for accessibility
- Responsive by default

Prompt for:
1. Component name
2. Component type (Button, Card, Modal, etc.)
3. Props needed
4. Accessibility requirements
```

**Uso**:
```bash
/meepleai:ui-component

# Component name: GameCard
# Type: Card
# Props: game (Game object), onSelect (function)
# Accessibility: Keyboard navigation, ARIA labels

# Output: Genera componente completo con test e accessibility
```

## Workflow Document-Driven Development (DDD)

Amplifier include un potente workflow DDD per evitare "doc drift":

### Fase 1: Design

```bash
/ddd:1-plan

# Amplifier chiede feature description
# Genera:
# - docs/issue/FEATURE-001.md (spec completa)
# - Architecture decisions
# - API design
# - Database schema
```

**Output Example** (`docs/issue/FEATURE-001-game-collections.md`):
```markdown
# Feature: Game Collections

## Overview
Allow users to organize games into custom collections.

## Requirements
- Users can create named collections
- Add/remove games from collections
- Share collections with other users
- Search within collections

## API Design

### Endpoints
POST /api/v1/collections
GET /api/v1/collections
GET /api/v1/collections/{id}
PUT /api/v1/collections/{id}
DELETE /api/v1/collections/{id}
POST /api/v1/collections/{id}/games

### Models
Collection {
  id: Guid
  userId: Guid
  name: string
  description: string
  gameIds: Guid[]
  isPublic: bool
  createdAt: DateTime
}

## Database Schema
CREATE TABLE collections (
  id UUID PRIMARY KEY,
  user_id UUID NOT NULL REFERENCES users(id),
  name VARCHAR(100) NOT NULL,
  description TEXT,
  is_public BOOLEAN DEFAULT false,
  created_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE collection_games (
  collection_id UUID REFERENCES collections(id),
  game_id UUID REFERENCES games(id),
  added_at TIMESTAMP DEFAULT NOW(),
  PRIMARY KEY (collection_id, game_id)
);

## Services
- ICollectionService
  - CreateCollectionAsync(userId, name, description)
  - GetUserCollectionsAsync(userId)
  - AddGameToCollectionAsync(collectionId, gameId)
  - RemoveGameFromCollectionAsync(collectionId, gameId)

## Tests
- Unit tests for CollectionService
- Integration tests for API endpoints
- E2E tests for UI flows
```

### Fase 2: Implementation

```bash
/ddd:2-impl

# Legge docs/issue/FEATURE-001.md
# Genera:
# - Backend services
# - API endpoints
# - Database migrations
# - Frontend components
# - Tests (unit + integration)
```

### Fase 3: Cleanup

```bash
/ddd:3-clean

# Aggiorna:
# - CLAUDE.md con nuovi endpoints
# - README.md se necessario
# - Database schema docs
# - Rimuove TODOs e debug code
```

## Sviluppo Parallelo con Worktree

Amplifier supporta worktree Git per provare approcci diversi in parallelo:

```bash
# Crea worktree per approccio alternativo
make worktree name=redis-cache branch=feat/redis-cache

# Ora hai due workspace:
# 1. D:/Repositories/meepleai-monorepo (main approach)
# 2. D:/Repositories/meepleai-monorepo-redis-cache (alternative)

# Lavora sull'alternativa
cd ../meepleai-monorepo-redis-cache
# ... implement Redis caching

# Confronta risultati
cd ../meepleai-monorepo
git diff main..feat/redis-cache

# Tieni il migliore, elimina l'altro
make worktree-remove name=redis-cache
```

## Design Intelligence

Amplifier include agenti specializzati per design:

### Creare UI Component Accessibile

```bash
# In Claude Code
/designer create a game selection modal with:
- Search functionality
- Grid layout (responsive)
- Keyboard navigation
- ARIA labels
- Focus management

# Amplifier genera componente completo con accessibility
```

### Agenti Design Disponibili

```bash
# Art Director - Visual strategy
Use the art-director agent to establish color palette for MeepleAI

# Component Designer - React components
Deploy component-designer to create reusable GameCard component

# Layout Architect - Information architecture
Have layout-architect design the dashboard layout

# Responsive Strategist - Device adaptation
Use responsive-strategist for mobile-first navigation

# Animation Choreographer - Motion design
Deploy animation-choreographer for page transitions
```

## Esempi Pratici per MeepleAI

### 1. Nuovo Endpoint API

**Comando personalizzato**:
```bash
/meepleai:new-endpoint

# Name: GetGamesByCategory
# Method: GET
# Route: /api/v1/games/category/{category}
# Service: IGameService
# Response: List<GameDto>

# Output:
# - Endpoint in Program.cs
# - Service method
# - DTO models
# - Tests
# - Swagger docs update
```

### 2. Database Migration

**Comando personalizzato**:
```bash
/meepleai:new-migration

# Description: Add user_preferences table
# Fields: user_id, theme, language, notifications_enabled

# Output:
# - Migration file (timestamp_AddUserPreferences.cs)
# - Up() method with CREATE TABLE
# - Down() method with DROP TABLE
# - Entity class (UserPreference.cs)
# - DbSet in MeepleAiDbContext
```

### 3. React Component con Tests

**Comando personalizzato**:
```bash
/meepleai:new-component

# Name: GameRulesViewer
# Type: Functional component
# Props: gameId, onClose
# State: loading, rules, error
# Features: PDF viewer, search, pagination

# Output:
# - components/GameRulesViewer.tsx
# - components/__tests__/GameRulesViewer.test.tsx
# - API integration with RagService
# - Loading states
# - Error handling
```

## Best Practices

### 1. Mantieni Context Pulito

```bash
# Usa AGENTS.md per context progetto
# Aggiorna quando architettura cambia
# Include esempi di codice standard
```

### 2. Comandi Specifici per Dominio

```bash
# Non creare comandi generici
❌ /new-thing

# Crea comandi specifici MeepleAI
✅ /meepleai:new-service
✅ /meepleai:new-endpoint
✅ /meepleai:ui-component
```

### 3. Validation Automatica

```bash
# Includi quality checks nei comandi
/meepleai:feature-implement
  → Genera codice
  → Esegue dotnet build
  → Esegue tests
  → Verifica coverage
  → Se fallisce, correggi automaticamente
```

### 4. Documentation Sync

```bash
# Ogni comando aggiorna docs
/meepleai:new-endpoint
  → Genera codice
  → Aggiorna CLAUDE.md
  → Aggiorna Swagger
  → Aggiorna API docs
```

## Transcript e Learning

Amplifier salva automaticamente le conversazioni:

```bash
# Visualizza transcript
make transcripts-list

# Cerca nelle conversazioni passate
make transcripts-grep pattern="authentication"

# Riusa soluzioni passate
/transcripts
# (Ripristina conversazione completa)
```

## Confronto: Workflow Con vs Senza Amplifier

### Senza Amplifier (Manuale)

```bash
# Nuovo service richiede ~30 minuti
1. Crea IMyService.cs (3 min)
2. Crea MyService.cs (5 min)
3. Aggiungi DI in Program.cs (2 min)
4. Crea tests (10 min)
5. Verifica pattern (5 min)
6. Aggiorna docs (5 min)

Totale: 30 minuti
Errori: Facili (dimenticare DI, test incompleti)
```

### Con Amplifier (Automatizzato)

```bash
# Con comando personalizzato: ~2 minuti
/meepleai:new-service MyService

Totale: 2 minuti
Errori: Quasi zero (pattern sempre corretti)
Quality: Test completi, docs aggiornati automaticamente
```

**ROI**:
- **Tempo risparmiato**: 93% (28 min/service)
- **Quality**: Test coverage 100% automatico
- **Consistency**: Stesso pattern ogni volta

## Limitazioni e Caveat

### ❌ Non Sostituisce Agent Lightning

```
Amplifier: Workflow di SVILUPPO
  ↓
  Genera codice più velocemente

Agent Lightning: Ottimizzazione AGENTI
  ↓
  Migliora qualità risposte AI
```

**Usa Entrambi**:
- **Amplifier**: Sviluppa feature più velocemente
- **Agent Lightning**: Ottimizza prompt RAG/QA

### ⚠️ Richiede Effort Iniziale

```bash
# Setup comandi personalizzati: 2-4 ore
# Ma poi risparmi 30+ min per task

Break-even: ~10-15 task
ROI Annuale: 100+ ore risparmiate
```

### 🔧 Manutenzione Comandi

```bash
# Quando architettura MeepleAI cambia:
# - Aggiorna AGENTS.md
# - Rivedi comandi personalizzati
# - Testa su nuovo pattern

Frequenza: Ogni major architectural change
```

## Quick Start Checklist

- [ ] **Install Amplifier** (30 min)
  ```bash
  git clone amplifier
  pnpm install && uv sync
  ```

- [ ] **Setup MeepleAI Context** (15 min)
  ```bash
  # Crea AGENTS.md con architecture
  # Aggiungi common patterns
  ```

- [ ] **Primo Comando** (1 ora)
  ```bash
  /ultrathink-task Create /meepleai:new-service command
  # Testa su service reale
  # Refina output
  ```

- [ ] **Secondo Comando** (30 min)
  ```bash
  /ultrathink-task Create /meepleai:new-endpoint command
  ```

- [ ] **DDD Workflow** (2 ore)
  ```bash
  # Prova workflow completo su feature piccola
  /ddd:1-plan → /ddd:2-impl → /ddd:3-clean
  ```

**Totale Setup**: 4-5 ore
**Break-even**: Dopo 10-15 task (~1-2 settimane)

## Risorse

- **Amplifier GitHub**: https://github.com/microsoft/amplifier
- **Amplifier Vision**: `AMPLIFIER_VISION.md` nel repo
- **Create Your Own Tools**: `docs/CREATE_YOUR_OWN_TOOLS.md`
- **Document-Driven Development**: `docs/document_driven_development/`
- **The Amplifier Way**: `docs/THIS_IS_THE_WAY.md`

## Prossimi Passi

1. **Setup Base** (Oggi)
   - Installa Amplifier
   - Crea AGENTS.md per MeepleAI
   - Familiarizza con `/ultrathink-task`

2. **Primo Comando** (Settimana 1)
   - `/meepleai:new-service` (task più comune)
   - Testa su 2-3 services reali
   - Refina finché perfetto

3. **Espandi** (Settimana 2-3)
   - `/meepleai:new-endpoint`
   - `/meepleai:ui-component`
   - DDD workflow completo

4. **Produzione** (Mese 2)
   - Tutti sviluppatori usano comandi
   - Velocity +50%
   - Quality consistente

---

**Ricorda**: Amplifier migliora il TUO workflow, non l'app di produzione. Per ottimizzare gli agenti AI di MeepleAI, usa **Agent Lightning** (già documentato).
