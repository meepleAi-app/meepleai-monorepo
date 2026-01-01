# Living Documentation Guide

MeepleAI utilizza **living documentation** estratta automaticamente dal codice sorgente, riducendo manutenzione e garantendo sincronizzazione con implementazione.

## Filosofia

✅ **Documentation as Code**: Documentazione vive nel codice sorgente
✅ **Single Source of Truth**: Codice è la verità, docs sono estratte
✅ **Auto-Generation**: Generazione automatica riduce drift
✅ **Minimal Manual Docs**: Solo architettura core e ADR manuali

## Componenti

### 1. OpenAPI Specification (Backend API)

**Endpoint**: [http://localhost:8080/openapi/v1.json](http://localhost:8080/openapi/v1.json)
**UI Interattiva**: [http://localhost:8080/scalar/v1](http://localhost:8080/scalar/v1)

**Generazione**: Automatica da attributi .NET 9
- Native OpenAPI support (`AddOpenApi()`)
- Scalar UI per interattività
- Generazione TypeScript client (NSwag)

**Utilizzo**:
```bash
# Avvia API
cd apps/api/src/Api && dotnet run

# Accedi Scalar UI
open http://localhost:8080/scalar/v1

# Download OpenAPI spec
curl http://localhost:8080/openapi/v1.json > openapi.json
```

### 2. XML Documentation Comments (Backend)

**Standard**: Triple-slash comments (`///`) su public APIs

**Pattern**:
```csharp
/// <summary>
/// Executes RAG query with hybrid retrieval (vector + keyword).
/// </summary>
/// <param name="request">Query request with question and game context</param>
/// <param name="cancellationToken">Cancellation token for async operation</param>
/// <returns>Answer with confidence score and source documents</returns>
/// <exception cref="ValidationException">Invalid request parameters</exception>
public async Task<AnswerDto> HandleAsync(
    AskQuestionCommand request,
    CancellationToken cancellationToken)
{
    // Implementation
}
```

**Configurazione**:
```xml
<!-- Api.csproj -->
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn> <!-- Missing XML comment warnings -->
</PropertyGroup>
```

**Output**: `bin/Debug/net9.0/Api.xml` (incluso in NuGet package)

### 3. JSDoc Comments (Frontend)

**Standard**: Block comments (`/** */`) su funzioni exported

**Pattern**:
```typescript
/**
 * Sends RAG query to backend API with streaming response handling.
 *
 * @param question - User question about game rules
 * @param gameId - Optional game ID for context filtering
 * @param onChunk - Callback for SSE chunks (streaming response)
 * @returns Promise resolving to complete answer with metadata
 * @throws {ApiError} Network or validation errors
 *
 * @example
 * ```ts
 * const answer = await askQuestion(
 *   "How many players in Catan?",
 *   "catan-uuid",
 *   (chunk) => console.log(chunk)
 * );
 * ```
 */
export async function askQuestion(
  question: string,
  gameId?: string,
  onChunk?: (chunk: string) => void
): Promise<AnswerDto> {
  // Implementation
}
```

**Generazione Docs**:
```bash
cd apps/web
pnpm add -D typedoc

# Generate HTML docs
npx typedoc --out docs-generated src/lib/api.ts
```

### 4. Component README Pattern

**Struttura**: Ogni bounded context e modulo frontend ha README auto-documentato

**Backend Pattern** (`BoundedContexts/{Context}/README.md`):
```markdown
# {Context} Bounded Context

## Domain Model
[Auto-estratto da Domain/*.cs entities]

## Commands & Queries
[Auto-estratto da Application/Commands|Queries/*.cs]

## API Endpoints
[Link a OpenAPI spec section]

## Dependencies
[Auto-estratto da Infrastructure/Dependencies]
```

**Frontend Pattern** (`lib/{module}/README.md`):
```markdown
# {Module} Module

## Components
[Auto-estratto da index.ts exports]

## Hooks
[Auto-estratto da JSDoc comments]

## API Integration
[Auto-estratto da api.ts functions]
```

### 5. ADR e Architettura (Manual)

**Preservati**: `docs/01-architecture/`
- `adr/` - Architectural Decision Records (22 ADR)
- `overview/` - System architecture overview
- `ddd/` - Domain-Driven Design patterns
- `components/` - Component architecture
- `diagrams/` - System diagrams (Mermaid/PlantUML)

**Rationale**: Decisioni architetturali richiedono contesto storico e motivazioni che codice non cattura.

## Workflow

### Sviluppatore

1. **Scrivi codice** con XML/JSDoc comments su public APIs
2. **Commit codice** → Docs auto-generate in CI/CD
3. **Verifica Scalar UI** per validare OpenAPI spec
4. **README contestuali** per bounded contexts/moduli significativi

### Manutentore

1. **Monitora OpenAPI** drift detection (CI failing tests)
2. **Review ADR** per decisioni architetturali
3. **Audit comments** per completezza documentazione

### Utente Finale

1. **Scalar UI** per API reference interattiva
2. **README bounded contexts** per comprensione domini
3. **ADR** per background decisioni architetturali

## CI/CD Integration

```yaml
# .github/workflows/docs.yml
name: Documentation

on: [push, pull_request]

jobs:
  validate-openapi:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet build apps/api/src/Api
      - run: dotnet run --project apps/api/src/Api &
      - run: sleep 10
      - run: curl -f http://localhost:8080/openapi/v1.json || exit 1

  generate-frontend-docs:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: pnpm install
      - run: pnpm typedoc --out docs-generated apps/web/src/lib
      - uses: actions/upload-artifact@v4
        with:
          name: frontend-docs
          path: docs-generated/
```

## Best Practices

### ✅ DO
- Documenta **public APIs** e interfacce esposte
- Scrivi **esempi pratici** nei commenti
- Usa **tipi precisi** per auto-completion
- Mantieni **ADR aggiornati** per decisioni architetturali
- Valida **OpenAPI spec** in CI/CD

### ❌ DON'T
- Documenta **implementazioni interne** (self-documenting code)
- Duplica informazioni già nel codice
- Scrivi docs separate che diventano obsolete
- Ignora **warnings** su missing XML comments

## Struttura Finale

```
meepleai-monorepo/
├── apps/
│   ├── api/
│   │   └── src/Api/BoundedContexts/{Context}/
│   │       ├── README.md              # Auto-documentato
│   │       ├── Domain/*.cs            # XML comments
│   │       └── Application/*.cs       # XML comments
│   └── web/
│       └── lib/{module}/
│           ├── README.md              # Auto-documentato
│           └── *.ts                   # JSDoc comments
├── docs/
│   ├── 01-architecture/               # MANUAL (preserved)
│   │   ├── adr/                       # ADR manuali
│   │   ├── overview/                  # Architettura sistema
│   │   ├── ddd/                       # DDD patterns
│   │   └── diagrams/                  # Diagrammi sistema
│   ├── living-documentation.md        # Questa guida
│   └── INDEX.md                       # Indice generale
└── docs-generated/                    # AUTO-GENERATED (gitignored)
    ├── api/                           # OpenAPI spec export
    └── frontend/                      # TypeDoc output
```

## Migrazione da Docs Manuali

**Completato** 2024-12-31:
- ❌ Rimosso: `docs/02-development/`, `docs/03-api/`, `docs/04-*/`, `docs/05-*/`
- ✅ Preservato: `docs/01-architecture/` (ADR + overview + DDD + diagrams)
- ✅ Backup: `docs-backup-20241231/` per recovery

**Rationale**: 84% riduzione file (301 → 47), focus su living docs da codice.

---

**Versione**: 1.0
**Ultimo Aggiornamento**: 2024-12-31
**Maintainer**: Engineering Team
