# BGG Search on New Game Page — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add BGG search to `/admin/shared-games/new` so admins can auto-fill game data from BoardGameGeek before creating a catalog entry.

**Architecture:** Extract a reusable `BggSearchPanel` from `Step3BggMatch`, extend `CreateSharedGameCommand` with metadata lists (categories, mechanics, designers, publishers), build a `MetadataTagInput` for preserving original casing, and wire everything into the existing new-game form.

**Tech Stack:** .NET 9 (MediatR, EF Core, FluentValidation) | Next.js (React 19, Zod, React Hook Form, TanStack Query)

**Spec:** `docs/superpowers/specs/2026-03-12-bgg-search-new-game-design.md`

---

## Chunk 1: Backend — Extend CreateSharedGameCommand

### Task 1: Add metadata fields to CreateSharedGameCommand

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/CreateSharedGameCommand.cs`

- [ ] **Step 1: Add list parameters to the command record**

```csharp
internal record CreateSharedGameCommand(
    string Title,
    int YearPublished,
    string Description,
    int MinPlayers,
    int MaxPlayers,
    int PlayingTimeMinutes,
    int MinAge,
    decimal? ComplexityRating,
    decimal? AverageRating,
    string ImageUrl,
    string ThumbnailUrl,
    GameRulesDto? Rules,
    Guid CreatedBy,
    int? BggId = null,
    List<string>? Categories = null,
    List<string>? Mechanics = null,
    List<string>? Designers = null,
    List<string>? Publishers = null
) : ICommand<Guid>;
```

- [ ] **Step 2: Verify it compiles**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

### Task 2: Add validation rules for new fields

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/CreateSharedGameCommandValidator.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Handlers/CreateSharedGameCommandValidatorTests.cs`

- [ ] **Step 1: Write failing tests for new validation rules**

Add these test methods to `CreateSharedGameCommandValidatorTests`:

```csharp
[Fact]
[Trait("Category", "Unit")]
public void Should_Pass_When_Categories_Is_Null()
{
    var command = CreateValidCommand() with { Categories = null };
    var result = _validator.Validate(command);
    result.IsValid.Should().BeTrue();
}

[Fact]
[Trait("Category", "Unit")]
public void Should_Pass_When_Categories_Has_Valid_Items()
{
    var command = CreateValidCommand() with { Categories = new List<string> { "Economic", "Negotiation" } };
    var result = _validator.Validate(command);
    result.IsValid.Should().BeTrue();
}

[Fact]
[Trait("Category", "Unit")]
public void Should_Fail_When_Categories_Exceeds_Max_Items()
{
    var command = CreateValidCommand() with { Categories = Enumerable.Range(0, 51).Select(i => $"Cat{i}").ToList() };
    var result = _validator.Validate(command);
    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.PropertyName == "Categories");
}

[Fact]
[Trait("Category", "Unit")]
public void Should_Fail_When_Categories_Contains_Empty_String()
{
    var command = CreateValidCommand() with { Categories = new List<string> { "Valid", "" } };
    var result = _validator.Validate(command);
    result.IsValid.Should().BeFalse();
}

[Fact]
[Trait("Category", "Unit")]
public void Should_Fail_When_Mechanics_Exceeds_Max_Items()
{
    var command = CreateValidCommand() with { Mechanics = Enumerable.Range(0, 51).Select(i => $"Mech{i}").ToList() };
    var result = _validator.Validate(command);
    result.IsValid.Should().BeFalse();
}

[Fact]
[Trait("Category", "Unit")]
public void Should_Fail_When_Designers_Exceeds_Max_Items()
{
    var command = CreateValidCommand() with { Designers = Enumerable.Range(0, 21).Select(i => $"Designer{i}").ToList() };
    var result = _validator.Validate(command);
    result.IsValid.Should().BeFalse();
}

[Fact]
[Trait("Category", "Unit")]
public void Should_Fail_When_Publishers_Exceeds_Max_Items()
{
    var command = CreateValidCommand() with { Publishers = Enumerable.Range(0, 21).Select(i => $"Pub{i}").ToList() };
    var result = _validator.Validate(command);
    result.IsValid.Should().BeFalse();
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api && dotnet test --filter "ClassName~CreateSharedGameCommandValidatorTests" --no-restore -v minimal 2>&1 | tail -10`
Expected: 4 new tests FAIL (Categories/Mechanics/Designers/Publishers max items + empty string)

- [ ] **Step 3: Add validation rules to the validator**

Add to `CreateSharedGameCommandValidator` constructor:

```csharp
// Metadata lists validation
When(x => x.Categories != null, () =>
{
    RuleFor(x => x.Categories!)
        .Must(list => list.Count <= 50).WithMessage("Cannot have more than 50 categories");
    RuleForEach(x => x.Categories!)
        .NotEmpty().WithMessage("Category name cannot be empty")
        .MaximumLength(200).WithMessage("Category name too long");
});

When(x => x.Mechanics != null, () =>
{
    RuleFor(x => x.Mechanics!)
        .Must(list => list.Count <= 50).WithMessage("Cannot have more than 50 mechanics");
    RuleForEach(x => x.Mechanics!)
        .NotEmpty().WithMessage("Mechanic name cannot be empty")
        .MaximumLength(200).WithMessage("Mechanic name too long");
});

When(x => x.Designers != null, () =>
{
    RuleFor(x => x.Designers!)
        .Must(list => list.Count <= 20).WithMessage("Cannot have more than 20 designers");
    RuleForEach(x => x.Designers!)
        .NotEmpty().WithMessage("Designer name cannot be empty")
        .MaximumLength(200).WithMessage("Designer name too long");
});

When(x => x.Publishers != null, () =>
{
    RuleFor(x => x.Publishers!)
        .Must(list => list.Count <= 20).WithMessage("Cannot have more than 20 publishers");
    RuleForEach(x => x.Publishers!)
        .NotEmpty().WithMessage("Publisher name cannot be empty")
        .MaximumLength(200).WithMessage("Publisher name too long");
});
```

Note: FluentValidation's `.MaximumLength()` is a string validator — it does NOT work on `List<T>`. Use `.Must(list => list.Count <= N)` to validate collection size. `.MaximumLength()` on `RuleForEach` items is correct since those are strings.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/api && dotnet test --filter "ClassName~CreateSharedGameCommandValidatorTests" --no-restore -v minimal 2>&1 | tail -10`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/CreateSharedGameCommand.cs \
       apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/CreateSharedGameCommandValidator.cs \
       apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Handlers/CreateSharedGameCommandValidatorTests.cs
git commit -m "feat(shared-catalog): add metadata lists to CreateSharedGameCommand"
```

### Task 3: Extend handler to associate metadata entities

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/CreateSharedGameCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Handlers/CreateSharedGameCommandHandlerTests.cs`

- [ ] **Step 1: Write failing tests for metadata association**

Add to `CreateSharedGameCommandHandlerTests`:

```csharp
[Fact]
[Trait("Category", "Unit")]
public async Task Handle_WithCategories_AssociatesCategoriesWithGame()
{
    // Arrange
    var command = CreateValidCommand() with
    {
        Categories = new List<string> { "Economic", "Negotiation" }
    };

    // Act
    var gameId = await _handler.Handle(command, CancellationToken.None);

    // Assert
    var game = await _repository.GetByIdAsync(gameId, CancellationToken.None);
    game.Should().NotBeNull();
    game!.Categories.Should().HaveCount(2);
    game.Categories.Select(c => c.Name).Should().Contain("Economic");
    game.Categories.Select(c => c.Name).Should().Contain("Negotiation");
}

[Fact]
[Trait("Category", "Unit")]
public async Task Handle_WithDesignersAndPublishers_AssociatesWithGame()
{
    var command = CreateValidCommand() with
    {
        Designers = new List<string> { "Klaus Teuber" },
        Publishers = new List<string> { "KOSMOS", "Catan Studio" }
    };

    var gameId = await _handler.Handle(command, CancellationToken.None);

    var game = await _repository.GetByIdAsync(gameId, CancellationToken.None);
    game.Should().NotBeNull();
    game!.Designers.Should().HaveCount(1);
    game.Designers.First().Name.Should().Be("Klaus Teuber");
    game.Publishers.Should().HaveCount(2);
}

[Fact]
[Trait("Category", "Unit")]
public async Task Handle_WithNullMetadata_CreatesGameWithoutMetadata()
{
    var command = CreateValidCommand() with
    {
        Categories = null,
        Mechanics = null,
        Designers = null,
        Publishers = null
    };

    var gameId = await _handler.Handle(command, CancellationToken.None);

    var game = await _repository.GetByIdAsync(gameId, CancellationToken.None);
    game.Should().NotBeNull();
    game!.Categories.Should().BeEmpty();
    game.Mechanics.Should().BeEmpty();
    game.Designers.Should().BeEmpty();
    game.Publishers.Should().BeEmpty();
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api && dotnet test --filter "ClassName~CreateSharedGameCommandHandlerTests" --no-restore -v minimal 2>&1 | tail -10`
Expected: 3 new tests FAIL

- [ ] **Step 3: Add metadata association to the handler**

After the `SharedGame.Create()` call and before `_repository.AddAsync()`, add:

```csharp
// Associate metadata from BGG
if (command.Categories is { Count: > 0 })
{
    foreach (var categoryName in command.Categories)
    {
        sharedGame.AddCategory(categoryName);
    }
}

if (command.Mechanics is { Count: > 0 })
{
    foreach (var mechanicName in command.Mechanics)
    {
        sharedGame.AddMechanic(mechanicName);
    }
}

if (command.Designers is { Count: > 0 })
{
    foreach (var designerName in command.Designers)
    {
        sharedGame.AddDesigner(designerName);
    }
}

if (command.Publishers is { Count: > 0 })
{
    foreach (var publisherName in command.Publishers)
    {
        sharedGame.AddPublisher(publisherName);
    }
}
```

Note: Check the `SharedGame` aggregate for the exact method names (`AddCategory`, `AddMechanic`, etc.). If they use different names (e.g., `AddGameCategory`), use those. If no such methods exist, add them to the aggregate following the existing pattern for `AddFaq` or `AddErrata`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/api && dotnet test --filter "ClassName~CreateSharedGameCommandHandlerTests" --no-restore -v minimal 2>&1 | tail -10`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/CreateSharedGameCommandHandler.cs \
       apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Handlers/CreateSharedGameCommandHandlerTests.cs
git commit -m "feat(shared-catalog): associate metadata entities in CreateSharedGameCommandHandler"
```

### Task 4: Update endpoint to accept new fields

**Files:**
- Modify: `apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs`
- Modify: `apps/api/src/Api/Models/Contracts.cs` (if the create DTO lives there)

- [ ] **Step 1: Find and update the create game DTO**

Search for the DTO that maps to `CreateSharedGameCommand` in the routing file. Add the 4 new list fields:

```csharp
// Add to the existing CreateSharedGameRequest/Dto
public List<string>? Categories { get; init; }
public List<string>? Mechanics { get; init; }
public List<string>? Designers { get; init; }
public List<string>? Publishers { get; init; }
```

- [ ] **Step 2: Update the endpoint mapping to pass new fields**

In the endpoint that handles `POST /api/v1/admin/shared-games`, update the command construction to include the new fields:

```csharp
// Add to the command construction
Categories = request.Categories,
Mechanics = request.Mechanics,
Designers = request.Designers,
Publishers = request.Publishers
```

- [ ] **Step 3: Add distinct metadata query endpoints**

These endpoints power the autocomplete `suggestions` prop on `MetadataTagInput`. Add four lightweight read-only endpoints that return distinct metadata values from existing shared games.

In `SharedGameCatalogEndpoints.cs`, add:

```csharp
group.MapGet("/metadata/categories", async (AppDbContext db) =>
    Results.Ok(await db.Set<GameCategory>().Select(c => c.Name).Distinct().OrderBy(n => n).ToListAsync()));

group.MapGet("/metadata/mechanics", async (AppDbContext db) =>
    Results.Ok(await db.Set<GameMechanic>().Select(m => m.Name).Distinct().OrderBy(n => n).ToListAsync()));

group.MapGet("/metadata/designers", async (AppDbContext db) =>
    Results.Ok(await db.Set<GameDesigner>().Select(d => d.Name).Distinct().OrderBy(n => n).ToListAsync()));

group.MapGet("/metadata/publishers", async (AppDbContext db) =>
    Results.Ok(await db.Set<GamePublisher>().Select(p => p.Name).Distinct().OrderBy(n => n).ToListAsync()));
```

Note: These are simple read-only queries that bypass CQRS/MediatR intentionally — no command/handler needed for basic lookups. If the project conventions require MediatR for all endpoints, wrap them in `GetDistinctCategoriesQuery` etc. Check the existing routing file for the pattern.

In `apps/web/src/lib/api/clients/sharedGamesClient.ts`, add:

```typescript
getDistinctCategories: () => fetchJson<string[]>('/api/v1/admin/shared-games/metadata/categories'),
getDistinctMechanics: () => fetchJson<string[]>('/api/v1/admin/shared-games/metadata/mechanics'),
getDistinctDesigners: () => fetchJson<string[]>('/api/v1/admin/shared-games/metadata/designers'),
getDistinctPublishers: () => fetchJson<string[]>('/api/v1/admin/shared-games/metadata/publishers'),
```

- [ ] **Step 4: Verify it compiles**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs apps/api/src/Api/Models/Contracts.cs \
       apps/web/src/lib/api/clients/sharedGamesClient.ts
git commit -m "feat(shared-catalog): accept metadata lists and add distinct metadata query endpoints"
```

---

## Chunk 2: Frontend — BggSearchPanel & MetadataTagInput

### Task 5: Extend frontend BGG types

**Files:**
- Modify: `apps/web/src/types/bgg.ts`

- [ ] **Step 1: Add missing fields to BggGameDetailsDto**

```typescript
export interface BggGameDetailsDto {
  id: number;
  name: string;
  yearPublished: number;
  minPlayers: number;
  maxPlayers: number;
  playingTime: number;
  minAge: number;
  rating: number;
  thumbnail: string | null;
  description?: string;
  categories?: string[];
  mechanics?: string[];
  // New fields
  designers?: string[];
  publishers?: string[];
  complexityRating?: number;
  averageRating?: number;
  imageUrl?: string;
}
```

- [ ] **Step 2: Add BggFullGameData type for the search panel callback**

```typescript
/** Extended BGG data used by BggSearchPanel onSelect callback */
export interface BggFullGameData {
  id: number;
  name: string;
  yearPublished?: number;
  minPlayers?: number;
  maxPlayers?: number;
  playingTime?: number;
  minAge?: number;
  description?: string;
  imageUrl?: string;
  thumbnailUrl?: string;
  categories: string[];
  mechanics: string[];
  designers: string[];
  publishers: string[];
  complexityRating?: number;
  averageRating?: number;
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/types/bgg.ts
git commit -m "feat(web): extend BGG types with metadata and BggFullGameData"
```

### Task 6: Create MetadataTagInput component

The existing `TagInput` normalizes tags (lowercase + hyphens). BGG metadata (e.g., "Klaus Teuber", "KOSMOS") must preserve original casing. Create a variant.

**Files:**
- Create: `apps/web/src/components/admin/shared-games/MetadataTagInput.tsx`
- Create: `apps/web/src/components/admin/shared-games/__tests__/MetadataTagInput.test.tsx`

- [ ] **Step 1: Write failing tests for MetadataTagInput**

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { MetadataTagInput } from '../MetadataTagInput';

describe('MetadataTagInput', () => {
  it('renders existing tags as chips', () => {
    render(<MetadataTagInput label="Categories" tags={['Economic', 'Negotiation']} onChange={vi.fn()} />);
    expect(screen.getByText('Economic')).toBeInTheDocument();
    expect(screen.getByText('Negotiation')).toBeInTheDocument();
  });

  it('adds tag on Enter without normalizing case', async () => {
    const onChange = vi.fn();
    render(<MetadataTagInput label="Designers" tags={[]} onChange={onChange} />);
    const input = screen.getByPlaceholderText(/add/i);
    await userEvent.type(input, 'Klaus Teuber{Enter}');
    expect(onChange).toHaveBeenCalledWith(['Klaus Teuber']);
  });

  it('removes tag on chip X click', async () => {
    const onChange = vi.fn();
    render(<MetadataTagInput label="Categories" tags={['Economic', 'Strategy']} onChange={onChange} />);
    const removeButtons = screen.getAllByRole('button', { name: /remove/i });
    await userEvent.click(removeButtons[0]);
    expect(onChange).toHaveBeenCalledWith(['Strategy']);
  });

  it('prevents duplicate tags (case-insensitive)', async () => {
    const onChange = vi.fn();
    render(<MetadataTagInput label="Categories" tags={['Economic']} onChange={onChange} />);
    const input = screen.getByPlaceholderText(/add/i);
    await userEvent.type(input, 'economic{Enter}');
    expect(onChange).not.toHaveBeenCalled();
  });

  it('respects maxTags limit', () => {
    const tags = Array.from({ length: 50 }, (_, i) => `Tag${i}`);
    render(<MetadataTagInput label="Categories" tags={tags} onChange={vi.fn()} maxTags={50} />);
    expect(screen.getByPlaceholderText(/add/i)).toBeDisabled();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm test -- --run src/components/admin/shared-games/__tests__/MetadataTagInput.test.tsx 2>&1 | tail -10`
Expected: FAIL — module not found

- [ ] **Step 3: Implement MetadataTagInput**

```tsx
'use client';

import { useState, type KeyboardEvent } from 'react';
import type { JSX } from 'react';
import { X } from 'lucide-react';
import { Badge } from '@/components/ui/data-display/badge';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';

export interface MetadataTagInputProps {
  label: string;
  tags: string[];
  onChange: (tags: string[]) => void;
  maxTags?: number;
  disabled?: boolean;
  placeholder?: string;
  /** Badge color variant */
  colorClass?: string;
  /** Autocomplete suggestions from existing DB values */
  suggestions?: string[];
}

export function MetadataTagInput({
  label,
  tags,
  onChange,
  maxTags = 50,
  disabled = false,
  placeholder = 'Add...',
  colorClass = '',
  suggestions = [],
}: MetadataTagInputProps): JSX.Element {
  const [inputValue, setInputValue] = useState('');
  const [showSuggestions, setShowSuggestions] = useState(false);

  // Filter suggestions: match input, exclude already-added tags
  const filteredSuggestions = inputValue.length >= 2
    ? suggestions.filter(
        s => s.toLowerCase().includes(inputValue.toLowerCase())
          && !tags.some(t => t.toLowerCase() === s.toLowerCase())
      ).slice(0, 8)
    : [];

  const addTag = () => {
    const trimmed = inputValue.trim();
    if (!trimmed) return;
    if (tags.length >= maxTags) return;
    // Case-insensitive duplicate check
    if (tags.some(t => t.toLowerCase() === trimmed.toLowerCase())) return;
    onChange([...tags, trimmed]);
    setInputValue('');
  };

  const removeTag = (tagToRemove: string) => {
    onChange(tags.filter(t => t !== tagToRemove));
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      addTag();
    } else if (e.key === 'Backspace' && !inputValue && tags.length > 0) {
      removeTag(tags[tags.length - 1]);
    }
  };

  return (
    <div className="space-y-1.5">
      <Label>{label}</Label>
      <div className="flex flex-wrap gap-1.5 rounded-lg border border-input bg-background p-2 min-h-[42px] items-center">
        {tags.map(tag => (
          <Badge key={tag} variant="secondary" className={`gap-1 pr-1 ${colorClass}`}>
            <span className="text-xs">{tag}</span>
            <button
              type="button"
              onClick={() => removeTag(tag)}
              disabled={disabled}
              aria-label={`Remove ${tag}`}
              className="ml-0.5 rounded-full p-0.5 hover:bg-destructive/20"
            >
              <X className="h-3 w-3" />
            </button>
          </Badge>
        ))}
        <div className="relative flex-1 min-w-[80px]">
          <Input
            type="text"
            value={inputValue}
            onChange={e => { setInputValue(e.target.value); setShowSuggestions(true); }}
            onKeyDown={handleKeyDown}
            onFocus={() => setShowSuggestions(true)}
            onBlur={() => setTimeout(() => setShowSuggestions(false), 150)}
            placeholder={placeholder}
            disabled={disabled || tags.length >= maxTags}
            className="border-0 p-0 h-7 text-sm shadow-none focus-visible:ring-0"
          />
          {showSuggestions && filteredSuggestions.length > 0 && (
            <ul className="absolute z-10 mt-1 w-56 rounded-md border bg-popover p-1 shadow-md">
              {filteredSuggestions.map(suggestion => (
                <li key={suggestion}>
                  <button
                    type="button"
                    className="w-full rounded-sm px-2 py-1.5 text-sm text-left hover:bg-accent"
                    onMouseDown={() => {
                      onChange([...tags, suggestion]);
                      setInputValue('');
                      setShowSuggestions(false);
                    }}
                  >
                    {suggestion}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm test -- --run src/components/admin/shared-games/__tests__/MetadataTagInput.test.tsx 2>&1 | tail -10`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/shared-games/MetadataTagInput.tsx \
       apps/web/src/components/admin/shared-games/__tests__/MetadataTagInput.test.tsx
git commit -m "feat(web): add MetadataTagInput component with case preservation"
```

### Task 7: Extract BggSearchPanel from Step3BggMatch

**Files:**
- Create: `apps/web/src/components/admin/shared-games/BggSearchPanel.tsx`
- Create: `apps/web/src/components/admin/shared-games/__tests__/BggSearchPanel.test.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/shared-games/import/steps/Step3BggMatch.tsx`

- [ ] **Step 1: Write failing tests for BggSearchPanel**

```tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BggSearchPanel } from '../BggSearchPanel';

// Mock the BGG search hook
vi.mock('@/hooks/queries/useSearchBggGames', () => ({
  useSearchBggGames: vi.fn(() => ({
    data: null,
    isLoading: false,
    error: null,
  })),
}));

// Mock the API client for manual BGG ID fetch and duplicate check
vi.mock('@/lib/api', () => ({
  api: {
    bgg: {
      getGameDetails: vi.fn(),
    },
    sharedGames: {
      checkBggDuplicate: vi.fn().mockResolvedValue({ exists: false }),
    },
  },
}));

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    {children}
  </QueryClientProvider>
);

describe('BggSearchPanel', () => {
  it('renders search input', () => {
    render(<BggSearchPanel onSelect={vi.fn()} />, { wrapper });
    expect(screen.getByPlaceholderText(/search/i)).toBeInTheDocument();
  });

  it('renders with initialQuery pre-filled', () => {
    render(<BggSearchPanel onSelect={vi.fn()} initialQuery="Catan" />, { wrapper });
    expect(screen.getByDisplayValue('Catan')).toBeInTheDocument();
  });

  it('shows search results when available', async () => {
    const { useSearchBggGames } = await import('@/hooks/queries/useSearchBggGames');
    (useSearchBggGames as ReturnType<typeof vi.fn>).mockReturnValue({
      data: {
        results: [
          { bggId: 13, name: 'Catan', yearPublished: 1995, type: 'boardgame', thumbnailUrl: null },
        ],
      },
      isLoading: false,
      error: null,
    });

    render(<BggSearchPanel onSelect={vi.fn()} initialQuery="Catan" />, { wrapper });
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('calls onSelect with full game data when result clicked', async () => {
    const onSelect = vi.fn();
    const { useSearchBggGames } = await import('@/hooks/queries/useSearchBggGames');
    (useSearchBggGames as ReturnType<typeof vi.fn>).mockReturnValue({
      data: {
        results: [
          { bggId: 13, name: 'Catan', yearPublished: 1995, type: 'boardgame', thumbnailUrl: null },
        ],
      },
      isLoading: false,
      error: null,
    });

    const { api } = await import('@/lib/api');
    (api.bgg.getGameDetails as ReturnType<typeof vi.fn>).mockResolvedValue({
      id: 13,
      name: 'Catan',
      yearPublished: 1995,
      minPlayers: 3,
      maxPlayers: 4,
      playingTime: 120,
      minAge: 10,
      description: 'Trade and build',
      categories: ['Negotiation'],
      mechanics: ['Dice Rolling'],
      designers: ['Klaus Teuber'],
      publishers: ['KOSMOS'],
      complexityRating: 2.32,
      averageRating: 7.14,
      imageUrl: 'https://example.com/catan.jpg',
      thumbnail: 'https://example.com/catan-thumb.jpg',
    });

    render(<BggSearchPanel onSelect={onSelect} initialQuery="Catan" />, { wrapper });

    const result = screen.getByText('Catan');
    await userEvent.click(result);

    await waitFor(() => {
      expect(onSelect).toHaveBeenCalledWith(
        expect.objectContaining({
          id: 13,
          name: 'Catan',
          categories: ['Negotiation'],
          designers: ['Klaus Teuber'],
        })
      );
    });
  });

  it('shows duplicate warning when game exists in catalog', async () => {
    const { api } = await import('@/lib/api');
    (api.sharedGames.checkBggDuplicate as ReturnType<typeof vi.fn>).mockResolvedValue({
      exists: true,
      existingGameId: 'abc-123',
    });

    const { useSearchBggGames } = await import('@/hooks/queries/useSearchBggGames');
    (useSearchBggGames as ReturnType<typeof vi.fn>).mockReturnValue({
      data: {
        results: [
          { bggId: 13, name: 'Catan', yearPublished: 1995, type: 'boardgame', thumbnailUrl: null },
        ],
      },
      isLoading: false,
      error: null,
    });

    render(<BggSearchPanel onSelect={vi.fn()} initialQuery="Catan" />, { wrapper });
    await userEvent.click(screen.getByText('Catan'));

    await waitFor(() => {
      expect(screen.getByText(/already exists/i)).toBeInTheDocument();
    });
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm test -- --run src/components/admin/shared-games/__tests__/BggSearchPanel.test.tsx 2>&1 | tail -10`
Expected: FAIL — module not found

- [ ] **Step 3: Implement BggSearchPanel**

Create `BggSearchPanel.tsx` by extracting the search logic from `Step3BggMatch.tsx`:
- Copy the search input, debounce logic, results list, match scoring, manual BGG ID section
- Replace `useGameImportWizardStore` dependency with local state + `onSelect` callback
- On result click: fetch full details via `api.bgg.getGameDetails(bggId)`, then map to `BggFullGameData`, then call `onSelect`
- On result click: also call `api.sharedGames.checkBggDuplicate(bggId)` and show warning if duplicate

Key differences from Step3BggMatch:
- No wizard store dependency
- Fetches full game details (with categories/mechanics/designers/publishers) on selection
- Includes duplicate check with warning UI
- Calls `onSelect(BggFullGameData)` with all metadata

**Throttle & error UX** (spec requirement — must be included):

```tsx
// Inside BggSearchPanel, add a slow-response timer:
const [isThrottled, setIsThrottled] = useState(false);
const [isUnavailable, setIsUnavailable] = useState(false);
const throttleTimerRef = useRef<NodeJS.Timeout | null>(null);

// When search starts loading:
useEffect(() => {
  if (isLoading) {
    throttleTimerRef.current = setTimeout(() => setIsThrottled(true), 3000);
  } else {
    if (throttleTimerRef.current) clearTimeout(throttleTimerRef.current);
    setIsThrottled(false);
  }
  return () => { if (throttleTimerRef.current) clearTimeout(throttleTimerRef.current); };
}, [isLoading]);

// When search errors out:
useEffect(() => {
  if (error) setIsUnavailable(true);
}, [error]);

// Render throttle notice:
{isThrottled && !isUnavailable && (
  <div className="flex items-center gap-2 rounded-lg bg-blue-50 border border-blue-200 p-3 text-sm text-blue-700">
    <Loader2 className="h-4 w-4 animate-spin" />
    BGG is responding slowly. Results may take a moment...
  </div>
)}

// Render unavailable alert:
{isUnavailable && (
  <div className="flex items-center gap-2 rounded-lg bg-amber-50 border border-amber-200 p-3 text-sm text-amber-700">
    <AlertTriangle className="h-4 w-4" />
    BGG is unavailable. Fill the form manually.
    <button type="button" onClick={() => { setIsUnavailable(false); refetch(); }}
      className="ml-auto text-xs underline">Retry</button>
  </div>
)}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm test -- --run src/components/admin/shared-games/__tests__/BggSearchPanel.test.tsx 2>&1 | tail -10`
Expected: All tests PASS

- [ ] **Step 5: Refactor Step3BggMatch to use BggSearchPanel**

Update `Step3BggMatch.tsx` to delegate search UI to `BggSearchPanel`:

```tsx
import { BggSearchPanel } from '@/components/admin/shared-games/BggSearchPanel';
import { useGameImportWizardStore } from '@/stores/useGameImportWizardStore';

export function Step3BggMatch({ onComplete }: Step3BggMatchProps) {
  const { extractedMetadata, setSelectedBggId } = useGameImportWizardStore();

  return (
    <BggSearchPanel
      initialQuery={extractedMetadata?.title}
      onSelect={(data) => {
        const bggGameData = {
          id: data.id,
          name: data.name,
          yearPublished: data.yearPublished,
          minPlayers: data.minPlayers,
          maxPlayers: data.maxPlayers,
          playingTime: data.playingTime,
          minAge: data.minAge,
          description: data.description,
          imageUrl: data.imageUrl,
          thumbnailUrl: data.thumbnailUrl,
        };
        setSelectedBggId(data.id, bggGameData);
        onComplete?.(data.id, bggGameData);
      }}
    />
  );
}
```

- [ ] **Step 6: Run Step3BggMatch tests to verify no regression**

Run: `cd apps/web && pnpm test -- --run src/app/admin/\\(dashboard\\)/shared-games/import/steps/__tests__/Step3BggMatch.test.tsx 2>&1 | tail -10`
Expected: All existing tests PASS

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/admin/shared-games/BggSearchPanel.tsx \
       apps/web/src/components/admin/shared-games/__tests__/BggSearchPanel.test.tsx \
       apps/web/src/app/admin/\\(dashboard\\)/shared-games/import/steps/Step3BggMatch.tsx
git commit -m "feat(web): extract BggSearchPanel from Step3BggMatch"
```

---

## Chunk 3: Frontend — Wire BggSearchPanel into NewGameClient

### Task 8: Update Zod schema and API client for new fields

**Files:**
- Modify: `apps/web/src/lib/api/schemas/shared-games.schemas.ts`
- Modify: `apps/web/src/lib/api/clients/sharedGamesClient.ts`

- [ ] **Step 1: Add new fields to the create game Zod schema**

Find the create game schema in `shared-games.schemas.ts` and add:

```typescript
categories: z.array(z.string()).optional().default([]),
mechanics: z.array(z.string()).optional().default([]),
designers: z.array(z.string()).optional().default([]),
publishers: z.array(z.string()).optional().default([]),
```

- [ ] **Step 2: Update the API client create() method**

In `sharedGamesClient.ts`, update the `create()` method to pass the new fields in the request body.

- [ ] **Step 3: Verify it compiles**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -5`
Expected: No errors

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/api/schemas/shared-games.schemas.ts \
       apps/web/src/lib/api/clients/sharedGamesClient.ts
git commit -m "feat(web): add metadata fields to create game schema and API client"
```

### Task 9: Integrate BggSearchPanel into NewGameClient

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/shared-games/new/client.tsx`

- [ ] **Step 1: Extend the form schema**

Add to `NewGameSchema`:

```typescript
const NewGameSchema = z
  .object({
    title: z.string().min(1, 'Title is required').max(255),
    description: z.string().min(1, 'Description is required'),
    yearPublished: z.coerce.number().int().min(1900).max(2100),
    minPlayers: z.coerce.number().int().min(1).max(100),
    maxPlayers: z.coerce.number().int().min(1).max(100),
    playingTimeMinutes: z.coerce.number().int().min(1).max(10000),
    minAge: z.coerce.number().int().min(0).max(100).default(0),
    imageUrl: z.union([z.string().url(), z.literal('')]).optional(),
    thumbnailUrl: z.union([z.string().url(), z.literal('')]).optional(),
    // New fields
    bggId: z.coerce.number().int().positive().optional(),
    complexityRating: z.coerce.number().min(0).max(5).optional(),
    averageRating: z.coerce.number().min(0).max(10).optional(),
    categories: z.array(z.string()).default([]),
    mechanics: z.array(z.string()).default([]),
    designers: z.array(z.string()).default([]),
    publishers: z.array(z.string()).default([]),
  })
  .refine((data) => data.maxPlayers >= data.minPlayers, {
    message: 'Max players must be ≥ min players',
    path: ['maxPlayers'],
  });
```

- [ ] **Step 2: Add BggSearchPanel, MetadataTagInput, and auto-fill handler**

Add imports at the top of the file:

```tsx
import { useState } from 'react';
import { BggSearchPanel } from '@/components/admin/shared-games/BggSearchPanel';
import { MetadataTagInput } from '@/components/admin/shared-games/MetadataTagInput';
import type { BggFullGameData } from '@/types/bgg';
```

Add state and handler inside the component, after `useForm()`:

```tsx
const [selectedBggId, setSelectedBggId] = useState<number | null>(null);
const [bggFilledFields, setBggFilledFields] = useState<Set<string>>(new Set());

const onBggSelect = (data: BggFullGameData) => {
  const fieldsToFill: Record<string, unknown> = {
    title: data.name,
    description: data.description ?? '',
    yearPublished: data.yearPublished,
    minPlayers: data.minPlayers,
    maxPlayers: data.maxPlayers,
    playingTimeMinutes: data.playingTime,
    minAge: data.minAge ?? 0,
    imageUrl: data.imageUrl ?? '',
    thumbnailUrl: data.thumbnailUrl ?? '',
    bggId: data.id,
    complexityRating: data.complexityRating,
    averageRating: data.averageRating,
    categories: data.categories,
    mechanics: data.mechanics,
    designers: data.designers,
    publishers: data.publishers,
  };

  const filled = new Set<string>();
  for (const [key, value] of Object.entries(fieldsToFill)) {
    if (value !== undefined && value !== null) {
      setValue(key as keyof NewGameFormData, value, { shouldValidate: true });
      filled.add(key);
    }
  }
  setBggFilledFields(filled);
  setSelectedBggId(data.id);
};
```

Fetch existing metadata values for autocomplete (add after `onBggSelect`):

```tsx
// Autocomplete suggestions from existing DB values
// These require backend endpoints or can be fetched from existing shared games
// For now, use a simple query hook — if no endpoint exists, create one in Task 4
// or fetch from the shared games list and extract unique values client-side
const { data: existingCategories = [] } = useQuery({
  queryKey: ['shared-games', 'metadata', 'categories'],
  queryFn: () => api.sharedGames.getDistinctCategories(),
  staleTime: 5 * 60 * 1000,
});
const { data: existingMechanics = [] } = useQuery({
  queryKey: ['shared-games', 'metadata', 'mechanics'],
  queryFn: () => api.sharedGames.getDistinctMechanics(),
  staleTime: 5 * 60 * 1000,
});
const { data: existingDesigners = [] } = useQuery({
  queryKey: ['shared-games', 'metadata', 'designers'],
  queryFn: () => api.sharedGames.getDistinctDesigners(),
  staleTime: 5 * 60 * 1000,
});
const { data: existingPublishers = [] } = useQuery({
  queryKey: ['shared-games', 'metadata', 'publishers'],
  queryFn: () => api.sharedGames.getDistinctPublishers(),
  staleTime: 5 * 60 * 1000,
});
```

Note: If the `getDistinct*()` API methods don't exist yet, add them to the backend as simple `SELECT DISTINCT Name FROM GameCategories` queries exposed via `GET /api/v1/admin/shared-games/metadata/categories` etc. These are lightweight read-only endpoints that can be added in Task 4.

Add `BggSearchPanel` above the form card in the JSX return:

```tsx
{/* BGG Search Section */}
<div className="mb-6">
  <BggSearchPanel onSelect={onBggSelect} />
  {selectedBggId && (
    <div className="mt-2 flex items-center gap-2">
      <span className="inline-flex items-center rounded-full bg-blue-100 px-3 py-1 text-sm font-medium text-blue-800">
        Linked to BGG #{selectedBggId}
      </span>
    </div>
  )}
</div>
```

Add a helper to apply warm background tint on auto-filled fields:

```tsx
const bggFieldClass = (field: string) =>
  bggFilledFields.has(field) ? 'bg-orange-50/50' : '';
```

Apply `bggFieldClass('title')`, `bggFieldClass('description')`, etc. to each form field's wrapper `className`.

Add MetadataTagInput components after the existing form fields, before the submit button:

```tsx
{/* BGG Metadata */}
<div className="grid grid-cols-1 md:grid-cols-2 gap-4">
  <MetadataTagInput
    label="Categories"
    tags={watch('categories') ?? []}
    onChange={(tags) => setValue('categories', tags)}
    maxTags={50}
    colorClass="bg-purple-100 text-purple-800"
    suggestions={existingCategories}
  />
  <MetadataTagInput
    label="Mechanics"
    tags={watch('mechanics') ?? []}
    onChange={(tags) => setValue('mechanics', tags)}
    maxTags={50}
    colorClass="bg-rose-100 text-rose-800"
    suggestions={existingMechanics}
  />
  <MetadataTagInput
    label="Designers"
    tags={watch('designers') ?? []}
    onChange={(tags) => setValue('designers', tags)}
    maxTags={20}
    colorClass="bg-green-100 text-green-800"
    suggestions={existingDesigners}
  />
  <MetadataTagInput
    label="Publishers"
    tags={watch('publishers') ?? []}
    onChange={(tags) => setValue('publishers', tags)}
    maxTags={20}
    colorClass="bg-amber-100 text-amber-800"
    suggestions={existingPublishers}
  />
</div>

{/* Ratings (only show when BGG data is loaded) */}
{selectedBggId && (
  <div className="grid grid-cols-2 gap-4">
    <FormField label="Complexity Rating (0-5)">
      <Input type="number" step="0.01" min="0" max="5"
        {...register('complexityRating', { valueAsNumber: true })}
        className={bggFieldClass('complexityRating')}
      />
    </FormField>
    <FormField label="Average Rating (0-10)">
      <Input type="number" step="0.01" min="0" max="10"
        {...register('averageRating', { valueAsNumber: true })}
        className={bggFieldClass('averageRating')}
      />
    </FormField>
  </div>
)}
```

- [ ] **Step 3: Update the onSubmit handler**

Pass the new fields to `api.sharedGames.create()`:

```typescript
const onSubmit = async (data: NewGameFormData) => {
  try {
    const gameId = await api.sharedGames.create({
      title: data.title,
      description: data.description,
      yearPublished: data.yearPublished,
      minPlayers: data.minPlayers,
      maxPlayers: data.maxPlayers,
      playingTimeMinutes: data.playingTimeMinutes,
      minAge: data.minAge,
      imageUrl: data.imageUrl || 'https://placehold.co/300x300?text=No+Image',
      thumbnailUrl: data.thumbnailUrl || data.imageUrl || 'https://placehold.co/150x150?text=No+Image',
      bggId: data.bggId,
      complexityRating: data.complexityRating,
      averageRating: data.averageRating,
      categories: data.categories,
      mechanics: data.mechanics,
      designers: data.designers,
      publishers: data.publishers,
    });
    router.push(`/admin/shared-games/${gameId}`);
  } catch (err) {
    setError('root', {
      message: err instanceof Error ? err.message : 'Failed to create game.',
    });
  }
};
```

- [ ] **Step 4: Verify it compiles**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -5`
Expected: No errors

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\\(dashboard\\)/shared-games/new/client.tsx
git commit -m "feat(web): integrate BggSearchPanel into new game page"
```

### Task 10: Write NewGameClient integration tests

**Files:**
- Create or Modify: `apps/web/src/app/admin/(dashboard)/shared-games/new/__tests__/NewGameClient.test.tsx`

- [ ] **Step 1: Write tests for BGG auto-fill flow**

```tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import NewGameClient from '../client';

// Mock next/navigation
const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

// Mock BggSearchPanel to control onSelect calls
let capturedOnSelect: ((data: any) => void) | null = null;
vi.mock('@/components/admin/shared-games/BggSearchPanel', () => ({
  BggSearchPanel: ({ onSelect }: { onSelect: (data: any) => void }) => {
    capturedOnSelect = onSelect;
    return <div data-testid="bgg-search-panel" />;
  },
}));

// Mock API client
const mockCreate = vi.fn().mockResolvedValue('new-game-id');
vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      create: (...args: any[]) => mockCreate(...args),
    },
  },
}));

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    {children}
  </QueryClientProvider>
);

const bggGameData = {
  id: 13,
  name: 'Catan',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  playingTime: 120,
  minAge: 10,
  description: 'Trade, build, settle.',
  imageUrl: 'https://example.com/catan.jpg',
  thumbnailUrl: 'https://example.com/catan-thumb.jpg',
  categories: ['Negotiation', 'Economic'],
  mechanics: ['Dice Rolling', 'Trading'],
  designers: ['Klaus Teuber'],
  publishers: ['KOSMOS'],
  complexityRating: 2.32,
  averageRating: 7.14,
};

describe('NewGameClient with BGG integration', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    capturedOnSelect = null;
  });

  it('auto-fills form fields when BGG game is selected', async () => {
    render(<NewGameClient />, { wrapper });
    // Simulate BGG selection via captured callback
    capturedOnSelect?.(bggGameData);

    await waitFor(() => {
      expect(screen.getByDisplayValue('Catan')).toBeInTheDocument();
      expect(screen.getByDisplayValue('1995')).toBeInTheDocument();
      expect(screen.getByDisplayValue('3')).toBeInTheDocument(); // minPlayers
      expect(screen.getByDisplayValue('4')).toBeInTheDocument(); // maxPlayers
    });
  });

  it('submits with BGG metadata fields', async () => {
    render(<NewGameClient />, { wrapper });
    capturedOnSelect?.(bggGameData);

    await waitFor(() => {
      expect(screen.getByDisplayValue('Catan')).toBeInTheDocument();
    });

    // Submit the form
    const submitButton = screen.getByRole('button', { name: /create/i });
    await userEvent.click(submitButton);

    await waitFor(() => {
      expect(mockCreate).toHaveBeenCalledWith(
        expect.objectContaining({
          bggId: 13,
          categories: ['Negotiation', 'Economic'],
          mechanics: ['Dice Rolling', 'Trading'],
          designers: ['Klaus Teuber'],
          publishers: ['KOSMOS'],
          complexityRating: 2.32,
          averageRating: 7.14,
        })
      );
    });
  });

  it('allows manual creation without BGG (regression)', async () => {
    render(<NewGameClient />, { wrapper });

    // Fill ALL required form fields manually
    await userEvent.type(screen.getByLabelText(/title/i), 'My Custom Game');
    await userEvent.type(screen.getByLabelText(/description/i), 'A fun game');
    await userEvent.clear(screen.getByLabelText(/year/i));
    await userEvent.type(screen.getByLabelText(/year/i), '2024');
    await userEvent.clear(screen.getByLabelText(/min.*players/i));
    await userEvent.type(screen.getByLabelText(/min.*players/i), '2');
    await userEvent.clear(screen.getByLabelText(/max.*players/i));
    await userEvent.type(screen.getByLabelText(/max.*players/i), '4');
    await userEvent.clear(screen.getByLabelText(/playing.*time/i));
    await userEvent.type(screen.getByLabelText(/playing.*time/i), '60');

    const submitButton = screen.getByRole('button', { name: /create/i });
    await userEvent.click(submitButton);

    await waitFor(() => {
      expect(mockCreate).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'My Custom Game',
          yearPublished: 2024,
          minPlayers: 2,
          maxPlayers: 4,
        })
      );
      // BGG fields should be empty/undefined
      expect(mockCreate.mock.calls[0][0].bggId).toBeUndefined();
    });
  });

  it('navigates to game detail page after successful creation', async () => {
    render(<NewGameClient />, { wrapper });
    capturedOnSelect?.(bggGameData);

    await waitFor(() => {
      expect(screen.getByDisplayValue('Catan')).toBeInTheDocument();
    });

    const submitButton = screen.getByRole('button', { name: /create/i });
    await userEvent.click(submitButton);

    await waitFor(() => {
      expect(mockPush).toHaveBeenCalledWith('/admin/shared-games/new-game-id');
    });
  });
});
```

- [ ] **Step 2: Run tests**

Run: `cd apps/web && pnpm test -- --run src/app/admin/\\(dashboard\\)/shared-games/new/__tests__/NewGameClient.test.tsx 2>&1 | tail -15`
Expected: All tests PASS

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/\\(dashboard\\)/shared-games/new/__tests__/
git commit -m "test(web): add NewGameClient BGG integration tests"
```

### Task 11: Final verification

- [ ] **Step 1: Run all backend tests**

Run: `cd apps/api && dotnet test --filter "BoundedContext=SharedGameCatalog" --no-restore -v minimal 2>&1 | tail -10`
Expected: All tests PASS

- [ ] **Step 2: Run all frontend tests**

Run: `cd apps/web && pnpm test -- --run 2>&1 | tail -10`
Expected: All tests PASS

- [ ] **Step 3: Run typecheck and lint**

Run: `cd apps/web && pnpm typecheck && pnpm lint 2>&1 | tail -10`
Expected: No errors

- [ ] **Step 4: Final commit with any remaining fixes**

If any fixes were needed, commit them.
