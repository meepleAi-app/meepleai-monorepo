# AGENTS.md

Questa guida è progettata per AI coding assistants (Claude Code, GitHub Copilot, Cursor, ecc.) che lavorano su questo progetto. Fornisce standard, convenzioni, workflow e prompt per garantire contributi consistenti e di qualità.

## 📋 Indice

- [Standard di Progetto](#standard-di-progetto)
- [Convenzioni di Codice](#convenzioni-di-codice)
- [GitHub Flow Workflow](#github-flow-workflow)
- [Convenzioni di Test](#convenzioni-di-test)
- [Prompt Utili](#prompt-utili)
- [Checklist Pre-Commit](#checklist-pre-commit)

## 🎯 Standard di Progetto

### Principi Fondamentali

1. **Codice Manutenibile**: Scrivi codice che il tuo futuro sé possa modificare facilmente
2. **Test-Driven**: Scrivi test prima del codice quando possibile
3. **BDD Approach**: Focalizzati sul comportamento, non sull'implementazione
4. **Documentazione**: Codice auto-documentante + commenti solo dove necessario
5. **Type Safety**: Sfrutta i sistemi di tipo (C# nullable, TypeScript strict)

### Tech Stack

- **Backend**: ASP.NET Core 9.0 (C#), Entity Framework Core, Minimal APIs
- **Frontend**: Next.js 14, React 18, TypeScript
- **Database**: PostgreSQL 16, Qdrant (vector DB), Redis (cache)
- **Testing**: xUnit (backend), Jest + Playwright (frontend)
- **Infrastructure**: Docker Compose

## 💻 Convenzioni di Codice

### C# Backend (`apps/api`)

#### Naming Conventions

```csharp
// Classes: PascalCase
public class GameService { }

// Interfaces: IPascalCase
public interface IGameService { }

// Methods: PascalCase
public async Task<Game> GetGameAsync(string gameId) { }

// Private fields: _camelCase
private readonly ILogger<GameService> _logger;

// Parameters: camelCase
public void ProcessGame(string gameId, bool includeDeleted) { }

// Constants: UPPER_SNAKE_CASE
private const int MAX_RETRIES = 3;
```

#### Async/Await Patterns

```csharp
// ✅ GOOD: Async suffix, proper cancellation token
public async Task<List<Game>> GetGamesAsync(CancellationToken cancellationToken = default)
{
    return await _dbContext.Games
        .Where(g => !g.IsDeleted)
        .ToListAsync(cancellationToken);
}

// ❌ BAD: No async suffix, no cancellation token
public async Task<List<Game>> GetGames()
{
    return await _dbContext.Games.ToListAsync();
}
```

#### Dependency Injection

Registra tutti i servizi in `Program.cs`:

```csharp
// Singleton per servizi stateless
builder.Services.AddSingleton<ITextChunkingService, TextChunkingService>();

// Scoped per servizi con stato per request (es. DbContext)
builder.Services.AddScoped<IGameService, GameService>();

// Transient per servizi lightweight
builder.Services.AddTransient<IEmailService, EmailService>();
```

#### Error Handling

```csharp
// ✅ GOOD: Specific exceptions, meaningful messages
if (string.IsNullOrWhiteSpace(gameId))
{
    throw new ArgumentException("Game ID cannot be null or empty", nameof(gameId));
}

// ✅ GOOD: Proper HTTP status codes in endpoints
app.MapGet("/api/games/{id}", async (string id, IGameService gameService) =>
{
    var game = await gameService.GetGameAsync(id);
    return game is not null
        ? Results.Ok(game)
        : Results.NotFound(new { error = $"Game '{id}' not found" });
});
```

#### Null Safety

```csharp
// ✅ GOOD: Nullable reference types
public class Game
{
    public string Id { get; set; } = null!; // Required
    public string? Description { get; set; } // Optional
}

// ✅ GOOD: Null checks
if (game?.Description is not null)
{
    ProcessDescription(game.Description);
}
```

### TypeScript Frontend (`apps/web`)

#### Naming Conventions

```typescript
// Interfaces & Types: PascalCase
interface Game {
  id: string;
  name: string;
}

// Components: PascalCase
export default function GameList() { }

// Functions: camelCase
function fetchGames(): Promise<Game[]> { }

// Constants: UPPER_SNAKE_CASE
const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE;

// Variables: camelCase
const gameList = await fetchGames();
```

#### React Patterns

```typescript
// ✅ GOOD: Typed props, destructuring
interface GameCardProps {
  game: Game;
  onSelect: (id: string) => void;
}

export function GameCard({ game, onSelect }: GameCardProps) {
  return (
    <div onClick={() => onSelect(game.id)}>
      <h3>{game.name}</h3>
    </div>
  );
}

// ✅ GOOD: Custom hooks
function useGames() {
  const [games, setGames] = useState<Game[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchGames().then(setGames).finally(() => setLoading(false));
  }, []);

  return { games, loading };
}
```

#### API Client Usage

```typescript
// ✅ GOOD: Use centralized API client
import api from '@/lib/api';

async function loadGames() {
  try {
    const games = await api.get<Game[]>('/api/games');
    return games;
  } catch (error) {
    console.error('Failed to load games:', error);
    throw error;
  }
}

// ❌ BAD: Direct fetch calls
async function loadGames() {
  const response = await fetch('http://localhost:8080/api/games');
  return response.json();
}
```

#### Type Safety

```typescript
// ✅ GOOD: Explicit types, no any
interface ApiResponse<T> {
  data: T;
  error?: string;
}

async function fetchData<T>(url: string): Promise<ApiResponse<T>> {
  // Implementation
}

// ❌ BAD: Using any
async function fetchData(url: string): Promise<any> {
  // Implementation
}
```

## 🔄 GitHub Flow Workflow

Questo progetto segue **GitHub Flow**, un workflow semplificato basato su feature branches e pull requests.

### Step-by-Step Workflow

#### 1. Crea Feature Branch

```bash
# Assicurati di essere su main aggiornata
git checkout main
git pull origin main

# Crea branch dalla naming convention
git checkout -b feature/DOC-02-agents-md
git checkout -b fix/api-auth-bug
git checkout -b refactor/improve-pdf-service
```

**Naming Convention Branches**:
- `feature/ISSUE-NUM-description` - Nuove funzionalità
- `fix/ISSUE-NUM-description` - Bug fix
- `refactor/description` - Refactoring
- `docs/description` - Solo documentazione
- `test/description` - Solo test

#### 2. Lavora sulla Feature

```bash
# Fai commit frequenti e atomici
git add src/Services/GameService.cs
git commit -m "feat(api): add GetGameById method

Implements game retrieval by ID with proper error handling

Closes #123"

# Conventional Commits format:
# <type>(<scope>): <subject>
#
# Types: feat, fix, docs, style, refactor, test, chore
# Scopes: api, web, db, infra, ci
```

**Commit Message Template**:

```
<type>(<scope>): <short summary>

<optional body: explain WHY, not WHAT>

<optional footer: closes #123, breaking changes>
```

**Esempi**:

```bash
# Feature con issue reference
git commit -m "feat(api): implement PDF text extraction service

Add PdfTextExtractionService using Docnet.Core for reliable
PDF text extraction across different PDF formats.

Closes #42"

# Bug fix
git commit -m "fix(web): prevent form submission with empty fields

Adds client-side validation to upload form

Fixes #89"

# Documentazione
git commit -m "docs: add agents.md with project standards

Provides guidance for AI coding assistants on conventions,
workflow, and best practices.

Closes #297"
```

#### 3. Push e Crea Pull Request

```bash
# Push del branch
git push -u origin feature/DOC-02-agents-md

# Crea PR (usando gh CLI)
gh pr create \
  --title "feat(docs): add AGENTS.md with project standards (DOC-02)" \
  --body "Implements DOC-02: comprehensive guide for AI agents with standards, conventions, and GitHub Flow workflow.

## Changes
- Added AGENTS.md with coding standards
- Updated README.md with reference to AGENTS.md
- Documented GitHub Flow workflow

Closes #297"
```

**PR Template Suggerito**:

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] 🚀 New feature
- [ ] 🐛 Bug fix
- [ ] 📚 Documentation
- [ ] ♻️ Refactoring
- [ ] ✅ Test

## Changes Made
- Change 1
- Change 2

## Testing
- [ ] Unit tests pass
- [ ] Integration tests pass
- [ ] Manual testing completed

## Checklist
- [ ] Code follows project conventions
- [ ] Tests added/updated
- [ ] Documentation updated
- [ ] No breaking changes (or documented if present)

Closes #<issue-number>
```

#### 4. Code Review e Merge

1. **Wait for CI**: Assicurati che tutte le GitHub Actions passino
2. **Address feedback**: Rispondi ai commenti di review
3. **Update branch**: Se main è cambiata, fai rebase o merge
4. **Merge**: Una volta approvata, fai merge (squash merge preferito)

```bash
# Se main è cambiata durante la review
git checkout main
git pull origin main
git checkout feature/DOC-02-agents-md
git rebase main

# Push force dopo rebase (SOLO su branch di feature!)
git push --force-with-lease
```

#### 5. Clean Up

```bash
# Dopo il merge, elimina il branch locale
git checkout main
git pull origin main
git branch -d feature/DOC-02-agents-md
```

### Branch Protection Rules

**main branch** ha:
- Require PR before merging
- Require status checks to pass (CI/CD)
- Require branches to be up to date
- No direct pushes allowed

## ✅ Convenzioni di Test

Seguiamo **BDD-style naming** per tutti i test. Consulta [testing-guide.md](./testing-guide.md) per la guida completa.

### Quick Reference

#### Backend (xUnit C#)

```csharp
public class GameServiceTests
{
    [Fact]
    public async Task Should_ReturnGame_When_GameExists()
    {
        // Arrange
        var gameId = "test-game";
        var service = CreateService();

        // Act
        var result = await service.GetGameAsync(gameId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(gameId, result.Id);
    }

    [Fact]
    public async Task Should_ThrowNotFoundException_When_GameNotFound()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetGameAsync("non-existent"));
    }
}
```

**Pattern**: `Should_[ExpectedBehavior]_When_[Condition]`

#### Frontend (Jest TypeScript)

```typescript
describe('GameList', () => {
  it('should display loading spinner when games are being fetched', () => {
    // Arrange
    render(<GameList />);

    // Assert
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
  });

  it('should display games when fetch succeeds', async () => {
    // Arrange
    const mockGames = [{ id: '1', name: 'Chess' }];
    jest.spyOn(api, 'get').mockResolvedValue(mockGames);

    // Act
    render(<GameList />);

    // Assert
    await waitFor(() => {
      expect(screen.getByText('Chess')).toBeInTheDocument();
    });
  });

  it('should show error message when fetch fails', async () => {
    // Arrange
    jest.spyOn(api, 'get').mockRejectedValue(new Error('API Error'));

    // Act
    render(<GameList />);

    // Assert
    await waitFor(() => {
      expect(screen.getByText(/error/i)).toBeInTheDocument();
    });
  });
});
```json
**Pattern**: `it('should [expected behavior] when [condition]')`

### Coverage Requirements

- **Frontend**: 90% minimum (enforced)
- **Backend**: Target 80%+ (not enforced but recommended)

```bash
# Run tests with coverage
cd apps/web && pnpm test:coverage
cd apps/api && dotnet test -p:CollectCoverage=true
```

## 💡 Prompt Utili

### Per Implementare una Nuova Feature

```
Devo implementare [FEATURE_NAME] per [USE_CASE].

Requisiti:
- [Requirement 1]
- [Requirement 2]

Segui questi step:
1. Analizza l'architettura esistente in CLAUDE.md
2. Identifica i file da modificare/creare
3. Scrivi i test prima (BDD-style naming)
4. Implementa la feature
5. Verifica che i test passino
6. Aggiorna la documentazione se necessario

Usa le convenzioni in AGENTS.md per codice e test.
```

### Per Risolvere un Bug

```
Ho un bug: [DESCRIZIONE_BUG]

Error message: [ERROR_MESSAGE]

Segui questo approccio:
1. Riproduci il bug con un test che fallisce
2. Analizza il codice per identificare la root cause
3. Implementa la fix minimale
4. Verifica che il test ora passi
5. Verifica che non ci siano regressioni

Usa BDD naming per il test: Should_[CorrectBehavior]_When_[Condition]
```

### Per Refactoring

```
Voglio refactorare [COMPONENT/SERVICE] perché [REASON].

Obiettivi:
- [Goal 1]
- [Goal 2]

Approccio:
1. Assicurati che esistano test per il comportamento attuale
2. Fai il refactoring mantenendo i test verdi
3. Migliora il codice in piccoli step incrementali
4. Verifica coverage dopo ogni step
5. Aggiorna commenti/documentazione

Principio guida: "Codice modificabile facilmente in futuro"
```

### Per Creare una PR

```
Ho completato la feature/fix sul branch [BRANCH_NAME].

Crea una PR seguendo il GitHub Flow in AGENTS.md:
1. Verifica che tutti i test passino localmente
2. Assicurati che il branch sia updated con main
3. Scrivi title e description seguendo il template
4. Includi "Closes #[ISSUE_NUMBER]" nel body
5. Push e crea PR con gh CLI

Usa Conventional Commits format per il title.
```csharp
## 📝 Checklist Pre-Commit

Prima di ogni commit, verifica:

### Backend (API)

- [ ] Codice compila senza errori: `dotnet build`
- [ ] Test passano: `dotnet test`
- [ ] Convenzioni C# rispettate (PascalCase, _camelCase, async/await)
- [ ] Null safety applicato correttamente
- [ ] Servizi registrati in DI (`Program.cs`)
- [ ] Test BDD-style: `Should_[Behavior]_When_[Condition]`
- [ ] Nessun warning del compilatore

### Frontend (Web)

- [ ] Codice compila: `pnpm build`
- [ ] Linter pulito: `pnpm lint`
- [ ] TypeCheck passa: `pnpm typecheck`
- [ ] Test passano: `pnpm test`
- [ ] Coverage >= 90%: `pnpm test:coverage`
- [ ] Nessun `any` type (usa explicit types)
- [ ] API client usato correttamente (`@/lib/api`)
- [ ] Test BDD-style: `it('should [behavior] when [condition]')`

### Generale

- [ ] Commit message segue Conventional Commits
- [ ] Nessun file sensibile (`.env`, credentials)
- [ ] Documentazione aggiornata se necessario
- [ ] CLAUDE.md aggiornato se cambiano workflow/architettura

### Pre-Push

```bash
# Backend checks
cd apps/api
dotnet build
dotnet test

# Frontend checks
cd apps/web
pnpm lint
pnpm typecheck
pnpm test:coverage

# All good? Push!
git push
```

## 🔗 Risorse Aggiuntive

- **[CLAUDE.md](../../CLAUDE.md)** - Guida tecnica completa per Claude Code
- **[README.md](../../README.md)** - Overview del progetto e quick start
- **[testing-guide.md](./testing-guide.md)** - Guida dettagliata BDD testing
- **[docs/code-coverage.md](../code-coverage.md)** - Misurazione coverage
- **[.github/workflows/ci.yml](../../.github/workflows/ci.yml)** - Pipeline CI/CD

## 🤖 Note per AI Assistants

Quando lavori su questo progetto:

1. **Leggi sempre** CLAUDE.md prima di iniziare per capire l'architettura
2. **Segui** le convenzioni in questo file per codice e test
3. **Usa** il GitHub Flow workflow per branch e PR
4. **Scrivi** test prima del codice quando possibile (TDD)
5. **Focalizzati** sul comportamento (BDD), non sull'implementazione
6. **Mantieni** il codice modificabile: il futuro te stesso ti ringrazierà
7. **Verifica** sempre che test e build passino prima di committare

---

**Maintained by**: MeepleAI Team
**Last Updated**: 2025-10-09
