# Microsoft Amplifier - Architettura e Concetti

## Cos'è Amplifier

**Microsoft Amplifier** è un sistema di sviluppo metacognitivo che trasforma la tua expertise in strumenti AI riutilizzabili, senza scrivere codice.

**Definizione formale**:
> "Automate complex workflows by describing how you think through them."

## Architettura Core

```
┌───────────────────────────────────────────────────────────────┐
│                    Microsoft Amplifier                         │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │         Claude Code (AI Coding Assistant)                │ │
│  └────────────────────┬────────────────────────────────────┘ │
│                       │                                        │
│  ┌────────────────────▼────────────────────────────────────┐ │
│  │         Metacognitive Recipe Engine                      │ │
│  │  ┌──────────────────────────────────────────────────┐  │ │
│  │  │ 1. User describes thinking process               │  │ │
│  │  │ 2. Amplifier converts to executable tool         │  │ │
│  │  │ 3. Tool becomes slash command                    │  │ │
│  │  │ 4. Command reusable across sessions              │  │ │
│  │  └──────────────────────────────────────────────────┘  │ │
│  └─────────────────────────────────────────────────────────┘ │
│                       │                                        │
│  ┌────────────────────▼────────────────────────────────────┐ │
│  │         Tool Library                                     │ │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │ │
│  │  │ DDD Workflow │  │ Design       │  │ Custom       │ │ │
│  │  │ Commands     │  │ Agents       │  │ Tools        │ │ │
│  │  │              │  │              │  │              │ │ │
│  │  │ /ddd:1-plan  │  │ /designer    │  │ /your-tool   │ │ │
│  │  │ /ddd:2-impl  │  │ art-director │  │              │ │ │
│  │  │ /ddd:3-clean │  │ component-   │  │              │ │ │
│  │  │              │  │   designer   │  │              │ │ │
│  │  └──────────────┘  └──────────────┘  └──────────────┘ │ │
│  └─────────────────────────────────────────────────────────┘ │
│                       │                                        │
│  ┌────────────────────▼────────────────────────────────────┐ │
│  │         Project Context (AGENTS.md)                      │ │
│  │  - Architecture patterns                                 │ │
│  │  - Code conventions                                      │ │
│  │  - Project-specific guidance                             │ │
│  └─────────────────────────────────────────────────────────┘ │
└───────────────────────────────────────────────────────────────┘
```

## Concetti Chiave

### 1. Metacognitive Recipes

**Definizione**: Descrizione step-by-step di come un esperto pensa attraverso un task.

**Esempio - Code Review Process**:
```
1. Understand the change
   - Read PR description
   - Identify affected components
   - Note stated objectives

2. Analyze code quality
   - Check naming conventions
   - Verify error handling
   - Review test coverage
   - Assess security implications

3. Consider architecture
   - Does it fit existing patterns?
   - Any ripple effects?
   - Long-term maintainability?

4. Provide feedback
   - Start with positives
   - Suggest improvements with rationale
   - Ask clarifying questions
   - Recommend resources if needed

5. Make recommendation
   - Approve, request changes, or needs discussion
   - Summarize key points
```

Amplifier converte questa recipe in un comando `/code-review` eseguibile.

### 2. Document-Driven Development (DDD)

**Filosofia**: "If it's not documented, it doesn't exist"

**Workflow**:
```
Phase 1: PLAN (Documentation)
  ↓
  docs/issue/FEATURE-001.md created
  - Requirements
  - API design
  - Database schema
  - Architecture decisions
  ↓
  Human approval required
  ↓

Phase 2: IMPLEMENT (Code)
  ↓
  Code generated based on docs/issue/
  - Services
  - Endpoints
  - Tests
  - Migrations
  ↓
  Tests must pass
  ↓

Phase 3: CLEAN (Finalize)
  ↓
  - Update CLAUDE.md
  - Remove TODOs
  - Final quality checks
  ↓
  Ready for PR
```

**Benefici**:
- ✅ Docs sempre sincronizzate con codice
- ✅ Design review prima di implementation
- ✅ Context preservato per future modifiche
- ✅ No "doc drift"

### 3. Workspace Pattern

**Pattern**: Amplifier diventa workspace che ospita progetti come submodules.

```
amplifier/                    # Amplifier root
├── .claude/
│   ├── agents/              # Design agents
│   ├── commands/            # Slash commands
│   └── tools/               # Utilities
├── projects/
│   ├── meepleai/           # ← Git submodule
│   │   ├── AGENTS.md       # MeepleAI context
│   │   ├── apps/
│   │   └── docs/
│   └── other-project/
├── ai_context/             # Shared knowledge
└── scenarios/              # Scenario tools
```

**Vantaggi**:
- Git history pulito per ogni progetto
- Amplifier updates indipendenti
- Context persistente tra sessioni
- Scalabile a N progetti

### 4. Design Intelligence

**7 Agenti Specializzati**:

| Agente | Focus | Esempio |
|--------|-------|---------|
| **art-director** | Visual strategy, branding | Color palette, typography |
| **component-designer** | React components | Buttons, cards, modals |
| **layout-architect** | Information architecture | Dashboard layouts, navigation |
| **responsive-strategist** | Device adaptation | Mobile-first, breakpoints |
| **animation-choreographer** | Motion design | Transitions, micro-interactions |
| **voice-strategist** | UX copy, tone | Button labels, error messages |
| **design-system-architect** | Design systems | Tokens, patterns, guidelines |

**Framework Design**:
- **9 Dimensions**: Purpose, hierarchy, color, typography, spacing, responsive, accessibility, motion, voice
- **4 Layers**: Foundational, structural, behavioral, experiential
- **Evidence-based**: WCAG 2.1, color theory, animation principles

### 5. Parallel Development

**Git Worktree Integration**:

```bash
# Prova due approcci in parallelo
make worktree name=approach-a branch=feat/approach-a
make worktree name=approach-b branch=feat/approach-b

# Due workspace completamente isolati:
meepleai-monorepo/          # Main
meepleai-monorepo-approach-a/  # Variant A
meepleai-monorepo-approach-b/  # Variant B

# Confronta, scegli il migliore
git diff approach-a..approach-b

# Rimuovi quello non scelto
make worktree-remove name=approach-b
```

## Stack Tecnologico

### Amplifier Dependencies

```json
{
  "python": "3.11+",
  "node": "20+",
  "package_managers": ["pnpm", "uv"],
  "ai_assistant": "Claude Code",
  "git": "2.37+"
}
```

### Amplifier Components

```
amplifier/
├── amplifier/              # Core Python package
│   ├── tools/             # Tool implementations
│   ├── scenarios/         # Scenario handlers
│   └── utils/             # Utilities
├── .claude/
│   ├── agents/            # Specialized agents (7 design + custom)
│   ├── commands/          # Slash commands definitions
│   └── tools/             # Helper scripts
├── docs/                  # Documentation
│   ├── CREATE_YOUR_OWN_TOOLS.md
│   ├── document_driven_development/
│   └── design/
├── scenarios/             # Built-in scenarios
└── tests/                 # Test suite
```

## Workflow Types

### Type 1: Command Creation

```
User describes thinking process
  ↓
/ultrathink-task converts to tool
  ↓
Tool saved as slash command
  ↓
Command reusable forever
```

**Example**:
```
Input: "When creating API endpoint, I think through: security, validation, error handling, tests"
Output: /api:new-endpoint command
```

### Type 2: Agent Deployment

```
User invokes specialist agent
  ↓
Agent applies domain expertise
  ↓
Coordinated multi-step execution
  ↓
Quality-assured output
```

**Example**:
```
Input: "Deploy component-designer for GameCard"
Output: Complete component with accessibility, tests, stories
```

### Type 3: DDD Workflow

```
/ddd:1-plan (Design phase)
  ↓ (Human approval)
/ddd:2-impl (Implementation)
  ↓ (Tests must pass)
/ddd:3-clean (Finalization)
  ↓
Feature complete with docs
```

## Performance Characteristics

### Time Savings

| Task | Manual | With Amplifier | Saving |
|------|--------|----------------|--------|
| New Service | 30 min | 2 min | 93% |
| New Endpoint | 15 min | 1 min | 93% |
| UI Component | 45 min | 5 min | 89% |
| Feature (small) | 4 hours | 1 hour | 75% |
| Feature (large) | 2 days | 6 hours | 75% |

### Quality Improvements

| Aspetto | Manuale | Con Amplifier |
|---------|---------|---------------|
| **Test Coverage** | 60-80% | 90-100% |
| **Pattern Consistency** | Variable | 100% |
| **Documentation** | Often outdated | Always synced |
| **Error Rate** | 15-20% | <5% |

### Learning Curve

```
Week 1: Setup + first command (4-5 hours)
  ↓
Week 2-3: Build command library (10-15 hours)
  ↓
Month 2+: Productivity gains compound
  ↓
ROI: 100+ hours/year saved
```

## Confronto con Altre Soluzioni

### Amplifier vs GitHub Copilot

| Aspetto | Amplifier | Copilot |
|---------|-----------|---------|
| **Scope** | Workflow automation | Code completion |
| **Reusability** | High (slash commands) | Low (per-use) |
| **Context** | Project-aware (AGENTS.md) | File-aware |
| **Quality** | Enforced patterns | Suggestions only |
| **Learning** | Compounds over time | Static |

**Complementari**: Usa entrambi
- Copilot: Quick inline completions
- Amplifier: Complex workflow automation

### Amplifier vs Custom Scripts

| Aspetto | Amplifier | Custom Scripts |
|---------|-----------|----------------|
| **Creation** | Natural language | Code required |
| **Maintenance** | Self-documenting | Manual docs needed |
| **Adaptation** | AI adjusts to context | Fixed logic |
| **Discovery** | Slash command interface | Must remember paths |

### Amplifier vs Agent Lightning

| Aspetto | Amplifier | Agent Lightning |
|---------|-----------|-----------------|
| **Purpose** | Dev workflow | AI agent training |
| **Target** | Developers | AI systems |
| **Output** | Tools/commands | Trained models |
| **Runtime** | Development | Development |
| **Production** | ❌ No | ✅ Yes (artifacts) |

**Per MeepleAI**:
- Amplifier: Sviluppa più velocemente
- Agent Lightning: Migliora qualità AI

## Integrazione con MeepleAI Workflow Esistente

### Scenario: Nuova Feature Completa

**Senza Amplifier** (Attuale):
```bash
# 1. Create docs (30 min)
# 2. Create branch (1 min)
# 3. Design API (45 min)
# 4. Implement service (2 hours)
# 5. Create endpoints (1 hour)
# 6. Write tests (1.5 hours)
# 7. Frontend component (1 hour)
# 8. Update docs (30 min)
# 9. Code review prep (30 min)

Total: 7.5 hours
```

**Con Amplifier**:
```bash
/meepleai:feature-start user-preferences
# (Genera branch, docs template) - 2 min

/ddd:1-plan
# (Design completo con AI) - 15 min
# Review e approval - 10 min

/ddd:2-impl
# (Genera tutto: service, endpoints, tests, UI) - 20 min
# Fix eventuali errori - 15 min

/ddd:3-clean
# (Cleanup, docs sync) - 5 min

Total: 1 hour 7 minutes
Saving: 6.4 hours (85%)
```

### Scenario: Bug Fix con Context

**Senza Amplifier**:
```bash
# 1. Understand issue (read code, logs) - 30 min
# 2. Find root cause - 45 min
# 3. Implement fix - 20 min
# 4. Write test to prevent regression - 30 min
# 5. Verify fix - 15 min

Total: 2.3 hours
```

**Con Amplifier** (con transcript search):
```bash
make transcripts-grep pattern="similar-bug-keyword"
# Trova conversazione passata con soluzione simile

/bug-hunter analyze issue #574
# Agent specializzato trova root cause rapidamente

# Implement fix
# (Con context da transcript e agent analysis)

Total: 45 minutes
Saving: 1.5 hours (65%)
```

## Metacognitive Recipes: Deep Dive

### Anatomia di una Recipe

```python
# Recipe Structure
{
  "name": "meepleai:new-service",
  "description": "Generate complete service with tests",
  "thinking_process": [
    {
      "step": 1,
      "thought": "What does this service need to do?",
      "action": "Gather requirements",
      "validation": "Requirements clear and specific"
    },
    {
      "step": 2,
      "thought": "What are the dependencies?",
      "action": "Identify required services/repos",
      "validation": "All dependencies available in DI"
    },
    {
      "step": 3,
      "thought": "What's the interface contract?",
      "action": "Design method signatures",
      "validation": "Follows async patterns, returns Task<T>"
    },
    {
      "step": 4,
      "thought": "How do we test this?",
      "action": "Generate unit + integration tests",
      "validation": "Coverage ≥90%, all edge cases covered"
    },
    {
      "step": 5,
      "thought": "Is it properly integrated?",
      "action": "Add DI registration, update docs",
      "validation": "Service discoverable, documented"
    }
  ],
  "output_artifacts": [
    "IMyService.cs",
    "MyService.cs",
    "MyServiceTests.cs",
    "Program.cs (DI registration)",
    "CLAUDE.md (documentation)"
  ]
}
```

### Creazione di una Recipe

**Processo**:
1. **Identifica task ripetitivo** nel tuo workflow
2. **Descrivi come pensi** quando lo esegui
3. **Usa `/ultrathink-task`** per convertirlo in tool
4. **Testa e refina** fino a risultati perfetti
5. **Riusa infinite volte**

**Example Dialog**:
```
You: /ultrathink-task Create a command that generates a new EF Core entity with:
- Entity class in Infrastructure/Entities/
- DbSet in MeepleAiDbContext
- Migration file
- Seed data template
- Unit tests

Follow MeepleAI patterns:
- Nullable reference types
- Audit fields (CreatedAt, UpdatedAt)
- Guid primary keys
- Navigation properties with virtual

Prompt user for:
- Entity name
- Properties with types
- Relationships

Claude: I'll create the /meepleai:new-entity command...
[Creates the tool]

You: Let's test it. /meepleai:new-entity

Claude:
Entity name: UserNotification
Properties:
  - UserId (Guid, FK to Users)
  - Message (string, 500 chars)
  - IsRead (bool)
  - NotificationType (enum)
Relationships:
  - User (many-to-one)

[Generates all files with perfect patterns]

You: Perfect! Now I have this command forever.
```

## Design Intelligence Architecture

### 9 Design Dimensions

```yaml
design_framework:
  foundational_layer:
    - purpose: "Why does this exist?"
    - hierarchy: "What's most important?"

  structural_layer:
    - color: "Color theory, contrast, accessibility"
    - typography: "Readability, hierarchy, scale"
    - spacing: "Rhythm, density, breathing room"

  behavioral_layer:
    - responsive: "Device adaptation, fluid layouts"
    - accessibility: "WCAG 2.1 AA compliance"

  experiential_layer:
    - motion: "Transitions, animations, choreography"
    - voice: "Tone, personality, UX copy"
```

### Design Agent Workflow

```
User: /designer create game card component

art-director: Establishes visual direction
  ↓ (Color palette, typography scale)

layout-architect: Defines structure
  ↓ (Grid system, spacing, hierarchy)

component-designer: Builds component
  ↓ (React code, CSS modules)

responsive-strategist: Adds breakpoints
  ↓ (Mobile, tablet, desktop)

animation-choreographer: Adds motion
  ↓ (Hover states, transitions)

voice-strategist: Refines copy
  ↓ (Button text, alt text, labels)

Output: Production-ready component with:
  - Accessibility (WCAG 2.1 AA)
  - Responsive (mobile-first)
  - Animations (subtle, purposeful)
  - Tests (Jest + Playwright)
  - Storybook stories
```

## Transcript System

### Automatic Export

```
Before compaction event:
  ↓
Amplifier exports full conversation
  ↓
Saved to .data/transcripts/YYYY-MM-DD_HH-MM-SS.json
  ↓
Includes:
  - All messages
  - Tool usage
  - Thinking blocks
  - Code changes
```

### Transcript Commands

```bash
# List available transcripts
make transcripts-list

# Search past solutions
make transcripts-grep pattern="auth implementation"

# Restore full conversation
/transcripts

# Extract patterns from multiple sessions
make transcripts-analyze topic="RAG optimization"
```

**Use Cases**:
- Recover lost context after compaction
- Find past solutions to similar problems
- Learn from successful patterns
- Share knowledge with team

## Development Commands

```bash
# Quality checks
make format        # Python formatting
make lint          # Code linting
make typecheck     # Type validation
make test          # Run test suite

# AI context rebuild
make rebuild-context

# Worktree management
make worktree name=experiment branch=feat/experiment
make worktree-list
make worktree-remove name=experiment

# Transcript utilities
make transcripts-list
make transcripts-grep pattern="keyword"
```

## Integration Patterns

### Pattern 1: Command Library

```bash
# Build MeepleAI command library over time
Week 1: /meepleai:new-service
Week 2: /meepleai:new-endpoint
Week 3: /meepleai:new-entity
Week 4: /meepleai:ui-component
Week 5: /meepleai:new-migration

# Dopo 1 mese: Library completa
# Velocity +50-75%
```

### Pattern 2: DDD for Features

```bash
# Use DDD workflow per ogni nuova feature
1. /ddd:1-plan  → Design + approval
2. /ddd:2-impl  → Implementation + tests
3. /ddd:3-clean → Cleanup + docs

# Garantisce:
# - Docs sempre aggiornate
# - Quality consistente
# - No forgotten steps
```

### Pattern 3: Design System Evolution

```bash
# Usa design agents per UI consistency
/designer audit existing components
# Identifica inconsistenze

art-director establish design tokens
# Crea design system foundation

component-designer migrate to design system
# Refactora componenti esistenti
```

## Limitations

### 1. Development-Only Tool

```
❌ Runtime integration:
   Amplifier NON gira in production
   Amplifier NON è un framework applicativo

✅ Development acceleration:
   Amplifier migliora il workflow di sviluppo
   Output (code, docs) va in production
```

### 2. Requires Claude Code

```
Amplifier è progettato per Claude Code
Non funziona con:
  - GitHub Copilot
  - Cursor
  - Altri AI assistants

Alternativa: Usa i concetti (metacognitive recipes)
ma implementa con altri tool
```

### 3. Learning Investment

```
Setup iniziale: 4-5 ore
Build command library: 10-15 ore
Break-even: 15-20 task (~2-3 settimane)

ROI positivo dopo: 1 mese
ROI significativo dopo: 3 mesi
```

## Quando Vale la Pena Usare Amplifier

### ✅ Vale la Pena Se:

- Lavori su MeepleAI **full-time** (>20 ore/settimana)
- Hai task ripetitivi frequenti (>2x/settimana)
- Team piccolo che vuole velocity boost
- Focus su quality e consistency
- Progetti a lungo termine (>6 mesi)

### ❌ Non Vale la Pena Se:

- Contributi occasionali (poche ore/mese)
- Task sempre diversi (poca ripetitività)
- Preferisci controllo manuale completo
- Time pressure immediate (setup richiede ore)

## ROI Calculator

```python
# Stima ROI per MeepleAI

# Setup time
setup_hours = 5

# Time saved per task type (hours)
service_creation_saved = 0.47  # 28 min
endpoint_creation_saved = 0.23  # 14 min
ui_component_saved = 0.67      # 40 min
feature_development_saved = 3.0 # 3 hours

# Frequenza mensile
services_per_month = 4
endpoints_per_month = 8
ui_components_per_month = 6
features_per_month = 2

# Calcolo
monthly_savings = (
    services_per_month * service_creation_saved +
    endpoints_per_month * endpoint_creation_saved +
    ui_components_per_month * ui_component_saved +
    features_per_month * feature_development_saved
)

# monthly_savings ≈ 13 hours/month

break_even_months = setup_hours / monthly_savings
# ≈ 0.4 months (2 settimane)

yearly_savings = monthly_savings * 12
# ≈ 156 hours/year
```

**Per MeepleAI Development**:
- Break-even: 2 settimane
- ROI annuale: 156 ore risparmiate (~1 mese di lavoro)

## Best Practices

### 1. Start Small

```bash
# Prima settimana: 1 comando
/meepleai:new-service

# Seconda settimana: Aggiungi se utile
/meepleai:new-endpoint

# Non creare 10 comandi subito
```

### 2. Iterate on Commands

```bash
# Primo tentativo: 80% corretto
/meepleai:new-service
# Output: Quasi perfetto, ma manca validation

# Refina
Improve /meepleai:new-service to add input validation by default

# Secondo tentativo: 95% corretto
# Continua a refinare finché perfetto
```

### 3. Document in AGENTS.md

```markdown
# AGENTS.md

## Custom Commands

### /meepleai:new-service
Creates complete service with interface, implementation, DI registration, and tests.

Example:
/meepleai:new-service
Service name: EmailService
Methods: SendEmail, SendBulkEmail
Dependencies: IConfiguration, ILogger

Output:
- IEmailService.cs
- EmailService.cs
- Program.cs (DI)
- EmailServiceTests.cs
```

### 4. Share with Team

```bash
# Commit comandi custom nel repo
git add .claude/commands/meepleai-*.md
git commit -m "Add Amplifier commands for MeepleAI workflows"
git push

# Team può usare gli stessi comandi
```

## Security Considerations

### Amplifier in Development

```yaml
safe_usage:
  - Review generated code before commit
  - Never commit secrets to AGENTS.md
  - Use .gitignore for sensitive context
  - Validate all generated SQL migrations
  - Test in local environment first

risky_usage:
  - Auto-commit without review
  - Include production credentials in context
  - Run generated scripts without inspection
  - Deploy without testing
```

### Project Context Hygiene

```bash
# ✅ Safe in AGENTS.md
- Architecture patterns
- Code conventions
- Public API structure
- Design decisions

# ❌ Never in AGENTS.md
- API keys
- Database passwords
- User data
- Production URLs (use placeholders)
```

## Troubleshooting

### Issue: Command Not Working as Expected

```bash
# Debug command
claude-code

Tell Claude: "Debug /meepleai:new-service command. Show me the metacognitive recipe."

# Refine
"The command should also add XML comments. Update the recipe."
```

### Issue: Context Too Large

```bash
# Split AGENTS.md
AGENTS.md (core)
AGENTS_PATTERNS.md (code patterns)
AGENTS_API.md (API conventions)

# Reference in AGENTS.md:
"See @AGENTS_PATTERNS.md for code examples"
```

### Issue: Amplifier Updates Breaking Commands

```bash
# Pin Amplifier version
cd tools/amplifier
git checkout <stable-commit>

# Or: Regular maintenance
git pull
# Test all custom commands
# Fix any breaking changes
```

## Next Steps

Vedi **amplifier-developer-workflow-guide.md** per:
- Setup step-by-step
- Primi comandi da creare
- Esempi pratici MeepleAI
- Quick start checklist

---

**Ricorda**: Amplifier è per il **tuo workflow di sviluppo**, non per produzione. Per ottimizzare gli **agenti AI di MeepleAI**, usa **Agent Lightning**.
