# Documentation Tools Guide

**Tools per generazione e validazione automatica documentazione**

---

## 🛠️ Tool Disponibili

### 1. Scalar.AspNetCore (API Documentation)

**Descrizione**: Interactive API documentation con OpenAPI/Swagger UI

**Status**: ✅ Installato e configurato

**Location**:
- Package: `Scalar.AspNetCore` v2.11.1
- Configuration: `apps/api/src/Api/Program.cs`
- UI: http://localhost:8080/scalar/v1
- Spec: http://localhost:8080/openapi/v1.json

**Usage**:
```csharp
// Automatic OpenAPI generation from endpoints
app.MapPost("/api/v1/games", async (CreateGameCommand cmd, IMediator mediator) =>
    await mediator.Send(cmd))
.WithTags("Games")          // Group in Scalar UI
.WithOpenApi()              // Include in OpenAPI spec
.Produces<GameDto>(201)     // Document response types
.Produces(400)              // Document error responses
.WithSummary("Create a new game")
.WithDescription("Creates a new game in the catalog with validation");
```

**Auto-Generated**:
- ✅ Endpoint list con metodi HTTP
- ✅ Request/response schemas
- ✅ Authentication requirements
- ✅ Response codes e error formats
- ✅ Try-it-out interactive testing

**Benefits**:
- Zero maintenance (auto-updated da codice)
- Always in sync con API attuale
- Interactive testing UI
- OpenAPI spec standard

---

### 2. validate-doc-links.sh (Link Validation)

**Descrizione**: Valida link interni markdown per evitare broken references

**Status**: ✅ Disponibile

**Location**: `scripts/validate-doc-links.sh`

**Usage**:
```bash
cd scripts
./validate-doc-links.sh

# Output:
# 🔍 Validating documentation links...
# 📄 Validating INDEX.md...
# 📄 Validating README.md...
# ✅ All links validated successfully!
```

**Validates**:
- Link relativi (`../01-architecture/adr/adr-001.md`)
- File existence
- Directory existence
- Skips external URLs (http://, https://)
- Skips anchors (#section)

**CI Integration**:
```yaml
# Add to .github/workflows/docs.yml
- name: Validate Documentation Links
  run: bash scripts/validate-doc-links.sh
```

---

### 3. XML Documentation Comments (C#)

**Descrizione**: Generazione documentazione da XML comments nel codice

**Status**: 🚧 Configurabile (non attivo)

**How to Enable**:

**1. Enable XML generation in .csproj**:
```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);CS1591</NoWarn> <!-- Disable missing XML comment warnings -->
</PropertyGroup>
```

**2. Write XML comments**:
```csharp
/// <summary>
/// Retrieves a game by its unique identifier
/// </summary>
/// <param name="gameId">The game's unique identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>GameDto if found, null otherwise</returns>
/// <exception cref="NotFoundException">Thrown when game doesn't exist</exception>
public async Task<GameDto?> GetGameAsync(Guid gameId, CancellationToken cancellationToken)
{
    return await _db.Games.FindAsync(gameId, cancellationToken);
}
```

**3. Generate HTML docs with DocFX**:
```bash
# Install DocFX
dotnet tool install -g docfx

# Initialize DocFX project
docfx init

# Build docs
docfx build

# Serve locally
docfx serve _site
```

**Benefits**:
- Auto-generated API reference da XML comments
- Search functionality
- Cross-references tra classi
- Versioning support

**Drawbacks**:
- Richiede disciplina team (scrivere XML comments)
- Setup iniziale complesso
- Maintenance overhead

**Recommendation**: ⚠️ Considera solo se team >5 developer

---

### 4. TypeDoc (TypeScript Documentation)

**Descrizione**: Generazione documentazione da TSDoc comments

**Status**: 🚧 Configurabile (non attivo)

**How to Enable**:

**1. Install TypeDoc**:
```bash
cd apps/web
pnpm add -D typedoc
```

**2. Write TSDoc comments**:
```typescript
/**
 * Fetches a game by ID from the API
 * @param gameId - Unique game identifier
 * @returns Promise resolving to GameDto or null
 * @throws {ApiError} When API request fails
 * @example
 * ```ts
 * const game = await getGame('123e4567-e89b-12d3-a456-426614174000');
 * ```
 */
export async function getGame(gameId: string): Promise<GameDto | null> {
  const response = await apiClient.get(`/api/v1/games/${gameId}`);
  return response.data;
}
```

**3. Generate docs**:
```bash
pnpm typedoc --entryPoints src --out docs-generated
```

**4. Add to package.json**:
```json
{
  "scripts": {
    "docs:generate": "typedoc --entryPoints src --out docs-generated"
  }
}
```

---

### 5. Storybook (Component Documentation)

**Descrizione**: Interactive component library e documentation

**Status**: 🚧 Configurabile (non attivo)

**How to Enable**:

**1. Install Storybook**:
```bash
cd apps/web
pnpx storybook@latest init
```

**2. Write stories**:
```typescript
// GameCard.stories.tsx
import type { Meta, StoryObj } from '@storybook/react';
import { GameCard } from './GameCard';

const meta: Meta<typeof GameCard> = {
  title: 'Components/GameCard',
  component: GameCard,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    game: {
      id: '1',
      name: 'Catan',
      playCount: 5,
    },
  },
};

export const NoPlays: Story = {
  args: {
    game: {
      id: '2',
      name: 'Ticket to Ride',
      playCount: 0,
    },
  },
};
```

**3. Run Storybook**:
```bash
pnpm storybook
# Opens http://localhost:6006
```

**Benefits**:
- Visual component testing
- Interactive playground
- Auto-generated props documentation
- Accessibility testing integration
- Chromatic visual regression testing

**Recommendation**: ✅ Considera per component library team

---

## 📊 Tool Comparison Matrix

| Tool | Purpose | Auto-Gen | Maintenance | Team Size | Recommendation |
|------|---------|----------|-------------|-----------|----------------|
| **Scalar** | API docs | ✅ Yes | Zero | Any | ⭐ Essential |
| **validate-doc-links.sh** | Link validation | ✅ Yes | Low | Any | ⭐ Essential |
| **DocFX** | C# API reference | ⚠️ Partial | High | >5 | 🤔 Consider |
| **TypeDoc** | TypeScript docs | ⚠️ Partial | Medium | >3 | 🤔 Consider |
| **Storybook** | Component library | ⚠️ Partial | Medium | >2 | ✅ Recommended |

---

## ✅ Recommended Setup

### Current (✅ Already Active)
1. **Scalar** - API documentation (zero effort)
2. **validate-doc-links.sh** - CI link validation

### Add for Team >2 (✅ Recommended)
3. **Storybook** - Component library documentation
   - Setup time: ~1 hour
   - Benefit: Visual testing + component docs
   - CI integration: Chromatic visual regression

### Add for Team >5 (🤔 Consider)
4. **TypeDoc** - TypeScript API reference
   - Setup time: ~2 hours
   - Benefit: Auto-generated frontend API docs
   - Requires: Team discipline scrivere TSDoc

### Skip (❌ Not Worth It)
5. **DocFX** - C# API reference
   - Reason: Scalar già copre API documentation
   - Alternative: XML comments in codice + Scalar

---

## 🚀 Quick Wins

### 1. Validare Link (Ora)
```bash
cd scripts
./validate-doc-links.sh
```

### 2. Migliorare Scalar Docs (10 min)
```csharp
// Add XML comments to endpoints
app.MapPost("/api/v1/games", ...)
.WithSummary("Create new game")
.WithDescription("Creates a new game with validation. Requires Admin role.")
.Produces<GameDto>(201)
.ProducesProblem(400)
.ProducesProblem(401)
.ProducesProblem(409);
```

### 3. Setup Storybook (1 ora - se team >2)
```bash
cd apps/web
pnpx storybook@latest init
pnpm storybook
```

---

## 📖 Related Documentation

- [API Documentation](../03-api/README.md) - Using Scalar UI
- [Development Guide](./README.md) - Development workflow
- [Testing Guide](../05-testing/README.md) - Testing documentation

---

**Last Updated**: 2026-01-18
**Maintainer**: Documentation Team
**Active Tools**: 2 (Scalar, validate-doc-links.sh)
**Recommended**: +1 (Storybook for component library)
