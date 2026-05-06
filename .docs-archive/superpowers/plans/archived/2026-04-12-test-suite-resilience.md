# Test Suite Resilience Migration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rendere la test suite immune a cambi di lingua UI e refactoring DOM, chiudere i gap di copertura nei bounded context sotto-testati, e risolvere i test saltati con TODO aperti.

**Architecture:** 4 track indipendenti. Track A (fondamenta) deve precedere track B. C e D sono parallele e indipendenti dagli altri.
- **Track A** — Centralizzazione stringhe UI: `test-strings.ts` + convenzione `getByRole` per nuovi test
- **Track B** — Migrazione progressiva selettori: `getByTestId` → `getByRole` nelle aree ad alto impatto
- **Track C** — Coverage gap backend: handler Gamification + handler DatabaseSync  
- **Track D** — Risoluzione test saltati: AgentChatPanel (4 skip) + form.test.tsx (2 skip) + KB validator italiano

**Tech Stack:** Vitest, React Testing Library, @testing-library/user-event, react-intl, xUnit, FluentValidation.TestHelper, Moq

---

## Contesto Critico

**Problema principale frontend:** I componenti React hanno stringhe italiane hardcoded (non usano `useIntl()`). L'infrastruttura i18n esiste (`it.json`, `en.json`, `renderWithIntl`) ma **non è utilizzata dai componenti**. Risultato: 5.529 `getByText` + 2.517 `getByTestId` sono tutti accoppiati a dettagli implementativi.

**Se i testi cambiano da italiano a inglese:** ~2.179 asserzioni sui test frontend si rompono in un colpo solo.

**Soluzione pragmatica:**
1. **Subito** — centralizzare le stringhe in `test-strings.ts`: un solo file da aggiornare invece di 400+ test
2. **Per nuovi test** — usare `getByRole` (linguisticamente neutro)
3. **Lungo termine** — migrare i componenti a `useIntl()` (fuori scope di questo piano)

---

## Mappa dei File

### Track A — Nuovi file
| File | Responsabilità |
|------|---------------|
| `apps/web/src/__tests__/fixtures/test-strings.ts` | Costanti centralizzate per tutte le stringhe UI usate nei test |
| `apps/web/src/__tests__/fixtures/README.md` | Aggiornamento con sezione convezione selettori |

### Track A — File modificati
| File | Modifica |
|------|----------|
| `apps/web/src/__tests__/fixtures/i18n-test-messages.ts` | Aggiungere `msg()` helper export se mancante |

### Track A — Test migrati (area Dashboard + Notifications — alto impatto)
| File | Stringhe da centralizzare |
|------|--------------------------|
| `apps/web/src/app/(authenticated)/dashboard/__tests__/DashboardClient.test.tsx` | `'Nessuna partita in corso'` |
| `apps/web/src/app/(authenticated)/dashboard/__tests__/GreetingRow.test.tsx` | `'Hai una partita in corso'` |
| `apps/web/src/app/(authenticated)/dashboard/__tests__/HeroLiveSession.test.tsx` | `'Nessuna partita'` |
| `apps/web/src/app/(authenticated)/notifications/__tests__/page.test.tsx` | 3 stringhe notifiche |
| `apps/web/src/app/(authenticated)/library/proposals/__tests__/MyProposalsClient.test.tsx` | 3 stringhe proposte |
| `apps/web/src/app/(authenticated)/onboarding/__tests__/page.test.tsx` | `'Benvenuto!'`, `'Errore di rete'` |

### Track B — File migrati (aree getByTestId → getByRole)
| File | Migrazione |
|------|------------|
| `apps/web/src/components/dashboard/__tests__/SessionNavBar.test.tsx` | `getByTestId('session-exit-button')` → `getByRole` |
| `apps/web/src/components/game-detail/__tests__/AgentChatPanel.test.tsx` | placeholder + send button queries |

### Track C — Nuovi file backend
| File | Responsabilità |
|------|---------------|
| `apps/api/tests/Api.Tests/BoundedContexts/Gamification/Application/Queries/GetAchievementsQueryHandlerTests.cs` | Handler: cache hit, cache miss, empty list |
| `apps/api/tests/Api.Tests/BoundedContexts/Gamification/Application/Queries/GetRecentAchievementsQueryHandlerTests.cs` | Handler: top-N recenti, empty |
| `apps/api/tests/Api.Tests/BoundedContexts/Gamification/Application/Validators/GetAchievementsQueryValidatorTests.cs` | UserId required |
| `apps/api/tests/Api.Tests/BoundedContexts/DatabaseSync/Application/Commands/ApplyMigrationsCommandHandlerTests.cs` | Guard clauses, confirmation required |
| `apps/api/tests/Api.Tests/BoundedContexts/DatabaseSync/Application/Commands/SyncTableDataCommandHandlerTests.cs` | Guard clauses, delegation all'infrastruttura |
| `apps/api/tests/Api.Tests/BoundedContexts/DatabaseSync/Application/Commands/PreviewMigrationSqlCommandHandlerTests.cs` | Guard clauses |

### Track D — File modificati
| File | Modifica |
|------|----------|
| `apps/web/src/components/game-detail/__tests__/AgentChatPanel.test.tsx` | Risolvere 4 `it.skip` o documentarli come E2E |
| `apps/web/src/components/ui/__tests__/form.test.tsx` | Risolvere 2 `it.skip` (Issue #1881) |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Validators/AgentDefinition/CreateAgentDefinitionCommandValidator.cs` | Tradurre messaggio italiano |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Validators/AgentDefinition/UpdateAgentDefinitionCommandValidator.cs` | Tradurre messaggio italiano |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/AgentDefinition/CreateAgentDefinitionCommandValidatorKbCardsTests.cs` | Aggiornare asserzione messaggio |

---

## TRACK A — Centralizzazione Stringhe UI

### Task A1: Creare test-strings.ts con costanti centralizzate

**Files:**
- Create: `apps/web/src/__tests__/fixtures/test-strings.ts`

- [ ] **Step 1: Creare il file con le stringhe ad alta frequenza**

```typescript
// apps/web/src/__tests__/fixtures/test-strings.ts
/**
 * Centralised UI string constants for test assertions.
 *
 * RATIONALE: Components use hardcoded Italian strings (not useIntl).
 * Centralising here means a language change requires updating ONE file,
 * not 400+ test files.
 *
 * CONVENTION: When writing NEW tests, prefer getByRole over getByText.
 * Use these constants only when a role-based selector is not available.
 *
 * @see docs/superpowers/plans/2026-04-12-test-suite-resilience.md
 */

// ─── Empty States ────────────────────────────────────────────────────────────
export const EMPTY = {
  notifications: 'Nessuna notifica',
  notificationsOfType: 'Nessuna notifica di questo tipo',
  notificationsUnread: 'Nessuna notifica non letta',
  sessions: 'Nessuna partita',
  sessionsActive: 'Nessuna partita in corso',
  proposals: 'Nessuna Proposta',
  photos: 'Nessuna foto ancora',
  tiers: 'Nessun tier trovato.',
  toolkit: 'Nessun toolkit configurato',
} as const;

// ─── Loading States ───────────────────────────────────────────────────────────
export const LOADING = {
  proposals: 'Caricamento proposte...',
  tiers: 'Caricamento tier',
  saving: 'Salvataggio',
} as const;

// ─── Error States ─────────────────────────────────────────────────────────────
export const ERROR = {
  loading: 'Errore di Caricamento',
  network: 'Errore di rete',
  tiersLoad: 'Errore nel caricamento dei tier.',
} as const;

// ─── Greetings / Onboarding ───────────────────────────────────────────────────
export const GREETING = {
  welcome: 'Benvenuto!',
  activeSession: 'Hai una partita in corso',
} as const;

// ─── Profile / Library ───────────────────────────────────────────────────────
export const PROFILE = {
  gameHistory: 'Storia di gioco',
} as const;

// ─── Admin ───────────────────────────────────────────────────────────────────
export const ADMIN = {
  editTierPrefix: 'Modifica tier: ',
} as const;

// ─── Session ─────────────────────────────────────────────────────────────────
export const SESSION = {
  exitButton: '← Esci',
} as const;
```

- [ ] **Step 2: Verificare che il file compila**

```bash
cd apps/web
npx tsc --noEmit --project tsconfig.json 2>&1 | grep "test-strings" || echo "OK"
```

Atteso: nessun errore di compilazione.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/__tests__/fixtures/test-strings.ts
git commit -m "test(infra): add test-strings.ts for centralized UI string constants"
```

---

### Task A2: Migrare Dashboard + GreetingRow — stringhe sessione

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/__tests__/DashboardClient.test.tsx`
- Modify: `apps/web/src/app/(authenticated)/dashboard/__tests__/GreetingRow.test.tsx`
- Modify: `apps/web/src/app/(authenticated)/dashboard/__tests__/HeroLiveSession.test.tsx`

- [ ] **Step 1: Aprire DashboardClient.test.tsx e aggiungere l'import**

```typescript
// In cima al file, dopo gli import esistenti
import { EMPTY } from '../../../../__tests__/fixtures/test-strings';
```

- [ ] **Step 2: Sostituire la stringa hardcoded**

Trovare (esatto):
```typescript
expect(screen.getByText(/Nessuna partita in corso/i)).toBeInTheDocument();
```

Sostituire con:
```typescript
expect(screen.getByText(new RegExp(EMPTY.sessionsActive, 'i'))).toBeInTheDocument();
```

- [ ] **Step 3: Stessa migrazione in GreetingRow.test.tsx**

Trovare:
```typescript
expect(screen.getByText('Hai una partita in corso')).toBeInTheDocument();
```

Sostituire con:
```typescript
import { GREETING } from '../../../../__tests__/fixtures/test-strings';
// ...
expect(screen.getByText(GREETING.activeSession)).toBeInTheDocument();
```

- [ ] **Step 4: Stessa migrazione in HeroLiveSession.test.tsx**

Trovare:
```typescript
expect(screen.getByText(/Nessuna partita/i)).toBeInTheDocument();
```

Sostituire con:
```typescript
import { EMPTY } from '../../../../__tests__/fixtures/test-strings';
// ...
expect(screen.getByText(new RegExp(EMPTY.sessions, 'i'))).toBeInTheDocument();
```

- [ ] **Step 5: Eseguire i test**

```bash
cd apps/web
pnpm test -- --run src/app/\(authenticated\)/dashboard/__tests__/
```

Atteso: tutti i test passano.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/__tests__/
git commit -m "test(dashboard): centralize Italian string constants via test-strings.ts"
```

---

### Task A3: Migrare Notifications + Proposals + Onboarding

**Files:**
- Modify: `apps/web/src/app/(authenticated)/notifications/__tests__/page.test.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/proposals/__tests__/MyProposalsClient.test.tsx`
- Modify: `apps/web/src/app/(authenticated)/onboarding/__tests__/page.test.tsx`

- [ ] **Step 1: Migrare notifications/page.test.tsx**

Aggiungere import:
```typescript
import { EMPTY } from '../../../../__tests__/fixtures/test-strings';
```

Sostituzioni (cerca e sostituisci nel file):

| Da | A |
|----|---|
| `getByText('Nessuna notifica')` | `getByText(EMPTY.notifications)` |
| `getByText('Nessuna notifica di questo tipo')` | `getByText(EMPTY.notificationsOfType)` |
| `getByText('Nessuna notifica non letta')` | `getByText(EMPTY.notificationsUnread)` |

- [ ] **Step 2: Migrare MyProposalsClient.test.tsx**

Aggiungere import:
```typescript
import { EMPTY, LOADING, ERROR } from '../../../../../__tests__/fixtures/test-strings';
```

Sostituzioni:

| Da | A |
|----|---|
| `getByText('Caricamento proposte...')` | `getByText(LOADING.proposals)` |
| `getByText('Errore di Caricamento')` | `getByText(ERROR.loading)` |
| `getByText('Nessuna Proposta')` | `getByText(EMPTY.proposals)` |

- [ ] **Step 3: Migrare onboarding/page.test.tsx**

Aggiungere import:
```typescript
import { GREETING, ERROR } from '../../../../__tests__/fixtures/test-strings';
```

Sostituzioni:

| Da | A |
|----|---|
| `getByText('Benvenuto!')` | `getByText(GREETING.welcome)` |
| `toHaveTextContent('Errore di rete')` | `toHaveTextContent(ERROR.network)` |

- [ ] **Step 4: Eseguire tutti i test migrati**

```bash
cd apps/web
pnpm test -- --run "notifications|proposals|onboarding"
```

Atteso: tutti passano.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/notifications/__tests__/ \
        apps/web/src/app/\(authenticated\)/library/proposals/__tests__/ \
        apps/web/src/app/\(authenticated\)/onboarding/__tests__/
git commit -m "test(notifications,proposals,onboarding): centralize Italian string constants"
```

---

### Task A4: Migrare Admin Tiers + Profile

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/config/tiers/__tests__/page.test.tsx`
- Modify: `apps/web/src/app/(authenticated)/profile/__tests__/page.test.tsx`

- [ ] **Step 1: Migrare admin tiers**

Aggiungere import:
```typescript
import { EMPTY, LOADING, ERROR, ADMIN } from '../../../../../../__tests__/fixtures/test-strings';
```

Sostituzioni:

| Da | A |
|----|---|
| `getByText(/caricamento tier/i)` | `getByText(new RegExp(LOADING.tiers, 'i'))` |
| `getByText('Nessun tier trovato.')` | `getByText(EMPTY.tiers)` |
| `getByText('Modifica tier: free')` | `getByText(\`${ADMIN.editTierPrefix}free\`)` |
| `getByText('Errore nel caricamento dei tier.')` | `getByText(ERROR.tiersLoad)` |

- [ ] **Step 2: Migrare profile**

Aggiungere import:
```typescript
import { PROFILE } from '../../../../__tests__/fixtures/test-strings';
```

Sostituzione:

| Da | A |
|----|---|
| `getByText('Storia di gioco')` | `getByText(PROFILE.gameHistory)` |

- [ ] **Step 3: Eseguire i test**

```bash
cd apps/web
pnpm test -- --run "tiers|profile"
```

Atteso: tutti passano.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/config/tiers/__tests__/ \
        apps/web/src/app/\(authenticated\)/profile/__tests__/
git commit -m "test(admin,profile): centralize Italian string constants"
```

---

### Task A5: Aggiornare README fixtures con convenzione selettori

**Files:**
- Modify: `apps/web/src/__tests__/fixtures/README.md`

- [ ] **Step 1: Aggiungere sezione convenzione**

Aprire il file. Aggiungere alla fine:

```markdown
## Convenzione Selettori (2026-04-12)

### Priorità selettori per nuovi test

1. **`getByRole`** ✅ preferito — semantico, accessibile, resistente a cambi di lingua e DOM
   ```tsx
   getByRole('button', { name: /salva/i })
   getByRole('alert')
   getByRole('status')
   getByRole('dialog', { name: /conferma/i })
   ```

2. **`getByLabelText`** ✅ ottimo per form
   ```tsx
   getByLabelText(/email/i)
   getByLabelText(/password/i)
   ```

3. **`getByText(UI_CONSTANT)`** ⚠️ accettabile per empty state/messaggi
   ```tsx
   import { EMPTY, ERROR } from '../fixtures/test-strings';
   getByText(EMPTY.notifications)
   ```

4. **`getByText('stringa italiana hardcoded')`** ❌ da evitare nei nuovi test
5. **`getByTestId`** ❌ ultima risorsa — documenta il motivo nel commento

### Quando usare test-strings.ts

Usa le costanti in `test-strings.ts` quando:
- Il componente non ha un `role` semantico appropriato
- Il testo è un messaggio di stato (empty state, errore, caricamento)
- Il componente non usa `useIntl()` / `FormattedMessage`

Non serve per:
- Titoli di giochi o dati utente nei test (invarianti di test, non UI)
- Contenuto dinamico costruito nel test stesso
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/__tests__/fixtures/README.md
git commit -m "docs(test-fixtures): add selector convention guidelines"
```

---

## TRACK B — Migrazione Selettori: getByTestId → getByRole

### Task B1: Migrare SessionNavBar — exit button

**Files:**
- Modify: `apps/web/src/components/dashboard/__tests__/SessionNavBar.test.tsx`

Contesto: il test usa `getByTestId('session-exit-button')` + `toHaveTextContent('← Esci')`.
Il bottone ha già `data-testid` ma il testo "← Esci" è accessibile via `getByRole`.

- [ ] **Step 1: Leggere il componente SessionNavBar**

```bash
find apps/web/src -name "SessionNavBar*" -not -path "*test*" -not -path "*node_modules*"
```

Aprire il file trovato. Verificare che il bottone exit abbia attributo accessibile (aria-label, testo visibile).

- [ ] **Step 2: Sostituire getByTestId con getByRole nel test**

Nel file `SessionNavBar.test.tsx`:

Trovare:
```typescript
expect(screen.getByTestId('session-exit-button')).toBeInTheDocument();
expect(screen.getByTestId('session-exit-button')).toHaveTextContent('← Esci');
```

Sostituire con:
```typescript
// getByRole è preferito a getByTestId — resiste a refactoring DOM
expect(screen.getByRole('button', { name: /esci/i })).toBeInTheDocument();
```

Trovare:
```typescript
fireEvent.click(screen.getByTestId('session-exit-button'));
```

Sostituire con:
```typescript
fireEvent.click(screen.getByRole('button', { name: /esci/i }));
```

- [ ] **Step 3: Eseguire il test**

```bash
cd apps/web
pnpm test -- --run "SessionNavBar"
```

Atteso: tutti i test passano. Se fallisce con "unable to find role button", verificare che il componente renda un `<button>` (non `<div role="button">`).

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/dashboard/__tests__/SessionNavBar.test.tsx
git commit -m "test(session-navbar): replace getByTestId with getByRole for exit button"
```

---

### Task B2: Aggiungere lint rule per getByTestId (opzionale ma consigliato)

**Files:**
- Modify: `apps/web/.eslintrc.js` oppure `apps/web/eslint.config.mjs`

- [ ] **Step 1: Verificare il formato ESLint usato**

```bash
ls apps/web/.eslint* apps/web/eslint.config* 2>/dev/null
```

- [ ] **Step 2: Aggiungere regola (solo per file .test.)**

Se il progetto usa flat config (`eslint.config.mjs`), aggiungere:

```javascript
// In eslint.config.mjs — sezione files per test
{
  files: ['**/*.test.tsx', '**/*.test.ts'],
  rules: {
    // Promuove l'uso di getByRole invece di getByTestId
    // Severità: warn (non error) per non bloccare PR esistenti
    'no-restricted-syntax': [
      'warn',
      {
        selector: "CallExpression[callee.property.name='getByTestId']",
        message:
          'Prefer getByRole() over getByTestId(). Use getByTestId only as last resort and add a comment explaining why.',
      },
    ],
  },
},
```

Se il progetto usa `.eslintrc.js`, aggiungere nella sezione `overrides` per pattern `**/*.test.*`.

- [ ] **Step 3: Verificare che la regola non spammi errori preesistenti**

```bash
cd apps/web
pnpm lint 2>&1 | grep "getByTestId" | wc -l
```

Se > 50 warning, ridurre a `off` e documentare il debito in un commento nel file.

- [ ] **Step 4: Commit**

```bash
git add apps/web/.eslintrc.js  # o eslint.config.mjs
git commit -m "chore(lint): add warn rule to prefer getByRole over getByTestId in tests"
```

---

## TRACK C — Coverage Gap Backend

### Task C1: GetAchievementsQueryHandler tests

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Gamification/Application/Queries/GetAchievementsQueryHandlerTests.cs`

L'handler usa `IHybridCacheService` con GetOrCreate. Pattern: mock cache + mock repos.

- [ ] **Step 1: Scrivere i test**

```csharp
using Api.BoundedContexts.Gamification.Application.DTOs;
using Api.BoundedContexts.Gamification.Application.Queries.GetAchievements;
using Api.BoundedContexts.Gamification.Domain.Entities;
using Api.BoundedContexts.Gamification.Domain.Enums;
using Api.BoundedContexts.Gamification.Domain.Repositories;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Gamification.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Gamification")]
public sealed class GetAchievementsQueryHandlerTests
{
    private readonly Mock<IAchievementRepository> _achievementRepo = new();
    private readonly Mock<IUserAchievementRepository> _userAchievementRepo = new();
    private readonly Mock<IHybridCacheService> _cache = new();
    private readonly GetAchievementsQueryHandler _sut;

    public GetAchievementsQueryHandlerTests()
    {
        _sut = new GetAchievementsQueryHandler(
            _achievementRepo.Object,
            _userAchievementRepo.Object,
            _cache.Object,
            new Mock<ILogger<GetAchievementsQueryHandler>>().Object);
    }

    [Fact]
    public void Constructor_NullAchievementRepository_ThrowsArgumentNullException()
    {
        var act = () => new GetAchievementsQueryHandler(
            null!,
            _userAchievementRepo.Object,
            _cache.Object,
            new Mock<ILogger<GetAchievementsQueryHandler>>().Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("achievementRepository");
    }

    [Fact]
    public void Constructor_NullUserAchievementRepository_ThrowsArgumentNullException()
    {
        var act = () => new GetAchievementsQueryHandler(
            _achievementRepo.Object,
            null!,
            _cache.Object,
            new Mock<ILogger<GetAchievementsQueryHandler>>().Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("userAchievementRepository");
    }

    [Fact]
    public void Constructor_NullCache_ThrowsArgumentNullException()
    {
        var act = () => new GetAchievementsQueryHandler(
            _achievementRepo.Object,
            _userAchievementRepo.Object,
            null!,
            new Mock<ILogger<GetAchievementsQueryHandler>>().Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("cache");
    }

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        var act = async () => await _sut.Handle(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_ValidQuery_ReturnsCachedResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetAchievementsQuery(userId);

        var expectedResult = new List<AchievementDto>
        {
            new() { Id = Guid.NewGuid(), Code = "FIRST_GAME", Name = "First Game", Points = 10 }
        };

        _cache
            .Setup(c => c.GetOrCreateAsync<List<AchievementDto>>(
                It.Is<string>(k => k.Contains(userId.ToString())),
                It.IsAny<Func<CancellationToken, Task<List<AchievementDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(expectedResult);
        _achievementRepo.Verify(r => r.GetActiveAsync(It.IsAny<CancellationToken>()), Times.Never,
            because: "cache hit should not trigger repository calls");
    }

    [Fact]
    public async Task Handle_CacheMiss_ComputesFromRepositories()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var achievementId = Guid.NewGuid();
        var query = new GetAchievementsQuery(userId);

        var achievements = new List<Achievement>
        {
            Achievement.Create("FIRST_GAME", "First Game", "Play your first game",
                null, 10, AchievementRarity.Common, AchievementCategory.Milestones, 1)
        };

        _achievementRepo
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(achievements);

        _userAchievementRepo
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserAchievement>());

        // Cache miss: invoke the factory
        _cache
            .Setup(c => c.GetOrCreateAsync<List<AchievementDto>>(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<AchievementDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string _, Func<CancellationToken, Task<List<AchievementDto>>> factory,
                string[] __, TimeSpan? ___, CancellationToken ct) => await factory(ct));

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Code.Should().Be("FIRST_GAME");
        result[0].IsUnlocked.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UserHasUnlockedAchievement_ReturnsIsUnlockedTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetAchievementsQuery(userId);

        var achievement = Achievement.Create("VETERAN", "Veteran", "Play 100 games",
            null, 50, AchievementRarity.Rare, AchievementCategory.Milestones, 100);

        var userAchievement = UserAchievement.Create(userId, achievement.Id, progress: 100);
        userAchievement.Unlock();

        _achievementRepo
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Achievement> { achievement });

        _userAchievementRepo
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserAchievement> { userAchievement });

        _cache
            .Setup(c => c.GetOrCreateAsync<List<AchievementDto>>(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<AchievementDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string _, Func<CancellationToken, Task<List<AchievementDto>>> factory,
                string[] __, TimeSpan? ___, CancellationToken ct) => await factory(ct));

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].IsUnlocked.Should().BeTrue();
        result[0].UnlockedAt.Should().NotBeNull();
    }
}
```

> **Nota:** Se `Achievement.Create` o `UserAchievement.Create` hanno signature diverse, leggere i sorgenti in `apps/api/src/Api/BoundedContexts/Gamification/Domain/Entities/` e adattare gli argomenti. Non usare `new Achievement { ... }` — usa sempre il factory method.

- [ ] **Step 2: Eseguire i nuovi test**

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~GetAchievementsQueryHandlerTests" --verbosity normal
```

Atteso: tutti i test passano. Se `Achievement.Create` non esiste o ha firma diversa, adattare.

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/Gamification/Application/Queries/GetAchievementsQueryHandlerTests.cs
git commit -m "test(gamification): add GetAchievementsQueryHandler unit tests (cache hit/miss, unlock)"
```

---

### Task C2: GetRecentAchievementsQueryHandler tests + GetAchievementsQueryValidator tests

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Gamification/Application/Queries/GetRecentAchievementsQueryHandlerTests.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Gamification/Application/Validators/GetAchievementsQueryValidatorTests.cs`

- [ ] **Step 1: Leggere GetRecentAchievementsQueryHandler**

```bash
cat apps/api/src/Api/BoundedContexts/Gamification/Application/Queries/GetRecentAchievements/GetRecentAchievementsQueryHandler.cs
cat apps/api/src/Api/BoundedContexts/Gamification/Application/Queries/GetAchievements/GetAchievementsQueryValidator.cs
```

- [ ] **Step 2: Scrivere GetRecentAchievementsQueryHandlerTests**

Struttura da replicare su GetRecentAchievements (stessa architettura di GetAchievements):

```csharp
using Api.BoundedContexts.Gamification.Application.DTOs;
using Api.BoundedContexts.Gamification.Application.Queries.GetRecentAchievements;
using Api.BoundedContexts.Gamification.Domain.Repositories;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Gamification.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Gamification")]
public sealed class GetRecentAchievementsQueryHandlerTests
{
    private readonly Mock<IUserAchievementRepository> _userAchievementRepo = new();
    private readonly Mock<IAchievementRepository> _achievementRepo = new();

    // Adattare il costruttore leggendo il sorgente dell'handler
    // Pattern identico a GetAchievementsQueryHandler

    [Fact]
    public async Task Handle_ReturnsRecentUnlockedAchievements()
    {
        // Arrange: preparare 3 userAchievement sbloccati, verificare che ne tornino max N
        // Act + Assert: result.Count <= query.MaxCount
        // (Implementare dopo aver letto il sorgente del handler)
        await Task.CompletedTask; // placeholder — da completare
    }

    [Fact]
    public async Task Handle_NoRecentAchievements_ReturnsEmptyList()
    {
        // Arrange: nessun UserAchievement sbloccato
        // Act + Assert: result è lista vuota, non exception
        await Task.CompletedTask; // placeholder — da completare
    }
}
```

> **Nota:** Leggere il sorgente di `GetRecentAchievementsQueryHandler.cs` prima di scrivere i test. Il pattern è identico a `GetAchievements` — replicare lo stesso approccio mock con i repository corretti.

- [ ] **Step 3: Scrivere GetAchievementsQueryValidatorTests**

```csharp
using Api.BoundedContexts.Gamification.Application.Queries.GetAchievements;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Gamification.Application.Validators;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Gamification")]
public sealed class GetAchievementsQueryValidatorTests
{
    private readonly GetAchievementsQueryValidator _validator = new();

    [Fact]
    public async Task Validate_ValidUserId_NoErrors()
    {
        var query = new GetAchievementsQuery(Guid.NewGuid());

        var result = await _validator.TestValidateAsync(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_EmptyUserId_HasValidationError()
    {
        var query = new GetAchievementsQuery(Guid.Empty);

        var result = await _validator.TestValidateAsync(query);

        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }
}
```

- [ ] **Step 4: Eseguire i nuovi test**

```bash
cd apps/api
dotnet test --filter "BoundedContext=Gamification" --verbosity normal
```

Atteso: tutti i Gamification test passano.

- [ ] **Step 5: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/Gamification/
git commit -m "test(gamification): add RecentAchievements handler + Achievements validator tests"
```

---

### Task C3: DatabaseSync — handler tests per operazioni di migrazione

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/DatabaseSync/Application/Commands/ApplyMigrationsCommandHandlerTests.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/DatabaseSync/Application/Commands/SyncTableDataCommandHandlerTests.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/DatabaseSync/Application/Commands/PreviewMigrationSqlCommandHandlerTests.cs`

**Prerequisito:** Leggere i 3 handler:
```bash
cat apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Commands/ApplyMigrationsHandler.cs
cat apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Commands/SyncTableDataHandler.cs
cat apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Commands/PreviewMigrationSqlHandler.cs
```

- [ ] **Step 1: Scrivere ApplyMigrationsCommandHandlerTests**

```csharp
using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DatabaseSync.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DatabaseSync")]
public sealed class ApplyMigrationsCommandHandlerTests
{
    private readonly Mock<IRemoteDatabaseConnector> _connector = new();

    // Adattare il costruttore dopo aver letto ApplyMigrationsHandler.cs
    // Pattern: guard clauses, delega a IRemoteDatabaseConnector

    [Fact]
    public void Constructor_NullConnector_ThrowsArgumentNullException()
    {
        // Leggere il costruttore dell'handler e testare tutti i parametri nullable
        // Adattare alla signature reale
        var act = () => new ApplyMigrationsHandler(
            null!,
            new Mock<ILogger<ApplyMigrationsHandler>>().Object);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Adattare leggendo il sorgente del handler per costruttore corretto
        var handler = CreateHandler();

        var act = async () => await handler.Handle(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_ValidCommand_DelegatesToConnector()
    {
        // Arrange
        var command = new ApplyMigrationsCommand(
            Direction: SyncDirection.LocalToRemote,
            Confirmation: "CONFIRM",
            AdminUserId: Guid.NewGuid());

        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: il connector viene chiamato (adattare il metodo dopo aver letto il sorgente)
        _connector.Verify(
            c => c.ApplyMigrationsAsync(
                It.IsAny<SyncDirection>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private ApplyMigrationsHandler CreateHandler() =>
        new(_connector.Object, new Mock<ILogger<ApplyMigrationsHandler>>().Object);
    // Adattare il costruttore alla signature reale dell'handler
}
```

> **IMPORTANTE:** La signature di `ApplyMigrationsHandler`, `SyncTableDataHandler`, e `PreviewMigrationSqlHandler` deve essere letta dai sorgenti prima di completare i test. Il pattern è sempre: guard clause su null, delega all'infrastruttura. Adattare i mock al contratto reale.

- [ ] **Step 2: Scrivere SyncTableDataCommandHandlerTests (stesso pattern)**

Struttura identica: constructor null check + Handle null + Handle valid delegates to `IRemoteDatabaseConnector.SyncTableDataAsync(...)`.

- [ ] **Step 3: Scrivere PreviewMigrationSqlCommandHandlerTests (stesso pattern)**

Struttura identica: constructor null check + Handle null + Handle valid returns SQL preview string.

- [ ] **Step 4: Eseguire i nuovi test**

```bash
cd apps/api
dotnet test --filter "BoundedContext=DatabaseSync" --verbosity normal
```

Atteso: tutti passano. Se fallisce su `new ApplyMigrationsHandler(null!, ...)` perché il costruttore ha più parametri, adattare leggendo il sorgente.

- [ ] **Step 5: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/DatabaseSync/Application/Commands/
git commit -m "test(database-sync): add handler tests for ApplyMigrations, SyncTableData, PreviewMigrationSql"
```

---

## TRACK D — Risoluzione Test Saltati

### Task D1: Tradurre messaggio italiano in KnowledgeBase validator

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Validators/AgentDefinition/CreateAgentDefinitionCommandValidator.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Validators/AgentDefinition/UpdateAgentDefinitionCommandValidator.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/AgentDefinition/CreateAgentDefinitionCommandValidatorKbCardsTests.cs`

Questo è l'unico messaggio italiano nei validator backend.

- [ ] **Step 1: Modificare CreateAgentDefinitionCommandValidator.cs**

Trovare:
```csharp
.WithMessage("L'agent deve avere almeno 1 KB card del game associato.");
```

Sostituire con:
```csharp
.WithMessage("The agent must have at least 1 KB card associated with the game.");
```

- [ ] **Step 2: Stessa modifica in UpdateAgentDefinitionCommandValidator.cs**

Trovare (stessa stringa):
```csharp
.WithMessage("L'agent deve avere almeno 1 KB card del game associato.");
```

Sostituire con:
```csharp
.WithMessage("The agent must have at least 1 KB card associated with the game.");
```

- [ ] **Step 3: Aggiornare il test**

In `CreateAgentDefinitionCommandValidatorKbCardsTests.cs`, trovare:
```csharp
.WithErrorMessage("L'agent deve avere almeno 1 KB card del game associato.");
```

Sostituire con:
```csharp
.WithErrorMessage("The agent must have at least 1 KB card associated with the game.");
```

- [ ] **Step 4: Cercare se ci sono altri test che testano UpdateAgentDefinitionCommandValidator con lo stesso messaggio**

```bash
cd apps/api
grep -r "L'agent deve avere" tests/ --include="*.cs"
```

Se trovati, aggiornare allo stesso modo.

- [ ] **Step 5: Eseguire i test KB**

```bash
cd apps/api
dotnet test --filter "BoundedContext=KnowledgeBase" --verbosity normal
```

Atteso: tutti passano.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Validators/AgentDefinition/ \
        apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/AgentDefinition/CreateAgentDefinitionCommandValidatorKbCardsTests.cs
git commit -m "fix(kb-validator): translate Italian error message to English"
```

---

### Task D2: AgentChatPanel — risolvere 4 skip

**Files:**
- Modify: `apps/web/src/components/game-detail/__tests__/AgentChatPanel.test.tsx`

- [ ] **Step 1: Analizzare i 4 skip**

```bash
grep -n "it.skip\|TODO" apps/web/src/components/game-detail/__tests__/AgentChatPanel.test.tsx
```

Output atteso:
- Linea ~100: `it.skip('should render agent mode selector')` — TODO: component structure investigation
- Linea ~421: `it.skip('should disable input while typing')` — UX non ancora implementata
- Linea ~463: `it.skip('should show typing indicator')` — testid query non funziona
- Linea ~567: `it.skip('should call onPdfReferenceClick')` — PDF reference non cliccabile

- [ ] **Step 2: Leggere il componente AgentChatPanel**

```bash
find apps/web/src/components/game-detail -name "AgentChatPanel*" -not -path "*__tests__*" | head -5
```

Aprire il file trovato e verificare:
1. Esiste un "agent mode selector"? (dropdown, radio, tabs)
2. Il bottone send viene disabilitato durante la chiamata API?
3. Esiste un typing indicator (skeleton, spinner)?
4. I citation/PDF reference hanno onClick?

- [ ] **Step 3: Classificare ogni skip**

Per ogni skip, scegliere:
- **A) Risolvere**: il comportamento esiste, il test è mal scritto
- **B) Documentare come E2E**: troppo complesso per unit test, coprire con Playwright
- **C) Rimuovere**: il comportamento non è implementato, il test non ha valore ora

Regola: non lasciare skip senza classificazione esplicita.

- [ ] **Step 4a: Risolvere "agent mode selector" (se il componente ha il selector)**

Se il componente ha un selector (es. `<select>`, `<RadioGroup>`, tabs):
```typescript
// Sostituire it.skip con:
it('should render agent mode selector', () => {
  renderPanel();

  // Adattare al tipo di selector trovato nel componente:
  // Per select/combobox:
  expect(screen.getByRole('combobox', { name: /mode/i })).toBeInTheDocument();
  // Per radio group:
  expect(screen.getByRole('radiogroup')).toBeInTheDocument();
  // Per tabs:
  expect(screen.getByRole('tablist')).toBeInTheDocument();
});
```

Se il componente NON ha il selector → rimuovere il test con commento:
```typescript
// REMOVED: Agent mode selector not yet implemented in component.
// Add test when feature is implemented.
```

- [ ] **Step 4b: Classificare "disable input while typing"**

Il commento dice "Component does not currently disable input during API call — nice-to-have UX improvement".

Questo skip è un test per una feature non implementata. Azione: **rimuovere** il test e aprire un issue separato se la feature è desiderata.

```typescript
// REMOVED (2026-04-12): Component does not disable input during streaming.
// This is a future UX improvement. Track in GitHub issue if needed.
```

- [ ] **Step 4c: Risolvere "show typing indicator"**

Il TODO dice "testid query with regex not finding elements". Leggere il componente e trovare l'indicatore corretto:

```bash
grep -n "typing\|indicator\|loading\|skeleton\|spinner" \
  apps/web/src/components/game-detail/AgentChatPanel.tsx 2>/dev/null || \
  find apps/web/src/components/game-detail -name "*.tsx" | xargs grep -l "typing\|indicator" 2>/dev/null
```

Se l'indicatore esiste, sostituire il testid con `getByRole`:
```typescript
it('should show typing indicator while waiting for response', async () => {
  // Usare getByRole o getByLabelText invece di testid
  // es: getByRole('status', { name: /loading/i })
  // Adattare dopo aver trovato l'elemento nel componente
});
```

- [ ] **Step 4d: Classificare "onPdfReferenceClick"**

Il TODO dice "PDF reference not clickable - component investigation required". Leggere il componente e verificare se le citation/reference hanno handler click. Se non hanno — rimuovere con commento. Se hanno — correggere il selettore.

- [ ] **Step 5: Eseguire i test AgentChatPanel**

```bash
cd apps/web
pnpm test -- --run "AgentChatPanel"
```

Atteso: 0 skip, tutti i test attivi passano.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/game-detail/__tests__/AgentChatPanel.test.tsx
git commit -m "test(agent-chat-panel): resolve 4 skipped tests — fix or remove with documented rationale"
```

---

### Task D3: form.test.tsx — risolvere 2 skip (Issue #1881)

**Files:**
- Modify: `apps/web/src/components/ui/__tests__/form.test.tsx`

Contesto: i 2 skip sono race condition tra react-hook-form + zod validation in jsdom. Il problema è che `handleSubmit` è asincrono e le asserzioni arrivano prima del completamento.

- [ ] **Step 1: Leggere il TestForm usato nel test**

```bash
grep -n "TestForm\|function Test\|const Test" apps/web/src/components/ui/__tests__/form.test.tsx | head -10
```

Trovare la definizione del `TestForm` nel file e assicurarsi che usi `mode: 'onSubmit'` in `useForm`.

- [ ] **Step 2: Risolvere "should submit form with valid data"**

Sostituire il blocco `it.skip` con:

```typescript
it('should submit form with valid data', async () => {
  const user = userEvent.setup();
  const mockSubmit = vi.fn();
  render(<TestForm onSubmit={mockSubmit} />);

  const usernameInput = screen.getByLabelText(/username/i);
  const emailInput = screen.getByLabelText(/email/i);

  await user.type(usernameInput, 'testuser');
  await user.type(emailInput, 'test@example.com');

  const submitButton = screen.getByRole('button', { name: /submit/i });
  await user.click(submitButton);

  // waitFor con timeout generoso per gestire la validazione asincrona di zod
  await waitFor(
    () => {
      expect(mockSubmit).toHaveBeenCalledWith(
        expect.objectContaining({
          username: 'testuser',
          email: 'test@example.com',
        }),
        expect.anything() // react-hook-form passa l'event come secondo argomento
      );
    },
    { timeout: 3000, interval: 100 }
  );
});
```

La chiave: `expect.anything()` come secondo argomento perché react-hook-form passa l'evento a `handleSubmit(onSubmit)(event)`.

- [ ] **Step 3: Risolvere "should submit form on Enter key"**

```typescript
it('should submit form on Enter key in input field', async () => {
  const user = userEvent.setup();
  const mockSubmit = vi.fn();
  render(<TestForm onSubmit={mockSubmit} />);

  const usernameInput = screen.getByLabelText(/username/i);
  const emailInput = screen.getByLabelText(/email/i);

  await user.type(usernameInput, 'testuser');
  await user.type(emailInput, 'test@example.com');
  // Premere Enter sull'ultimo campo per il submit
  await user.keyboard('{Enter}');

  await waitFor(
    () => {
      expect(mockSubmit).toHaveBeenCalledWith(
        expect.objectContaining({
          username: 'testuser',
          email: 'test@example.com',
        }),
        expect.anything()
      );
    },
    { timeout: 3000, interval: 100 }
  );
});
```

- [ ] **Step 4: Eseguire il test**

```bash
cd apps/web
pnpm test -- --run "form.test"
```

Atteso: 0 skip, tutti passano. Se ancora flaky, aumentare `timeout` a 5000 o aggiungere `vi.useFakeTimers()` + `vi.runAllTimersAsync()` prima del click.

- [ ] **Step 5: Se ancora flaky dopo 3 tentativi**

Documentare come E2E invece di skipparli:

```typescript
it.skip('should submit form with valid data', () => {
  // DEFERRED TO E2E: react-hook-form + zod timing is unreliable in jsdom.
  // Covered by Playwright form submission tests.
  // Issue #1881 — revisit if @testing-library/user-event v15 improves timing.
});
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/__tests__/form.test.tsx
git commit -m "test(form): resolve 2 skipped submit tests (Issue #1881)"
```

---

## Self-Review

### Copertura dei requisiti del panel

| Priorità Panel | Task Corrispondente | Status |
|---|---|---|
| 🔴 P1 — Asserzioni italiane hardcoded (~2.179) | A1→A5: test-strings.ts + migrazione aree core | ✅ Coperto per aree ad alto impatto. Migrazione completa = lavoro continuativo. |
| 🟡 P2 — getByTestId overuse (2.973) | B1: SessionNavBar, B2: lint rule | ✅ Coperto come convenzione + 1 esempio |
| 🟡 P3 — Coverage gap Gamification | C1+C2: handler + validator tests | ✅ Coperto |
| 🟡 P3 — Coverage gap DatabaseSync | C3: 3 handler tests mancanti | ✅ Coperto |
| 🟡 P4 — AgentChatPanel 4 skip | D2 | ✅ Coperto |
| 🟢 P5 — form.test.tsx 2 skip | D3 | ✅ Coperto |
| 🟢 P6 — KB validator italiano | D1 | ✅ Coperto |

### Note sui limiti del piano

1. **Migrazione test-strings non è completa** — il piano copre ~20 file ad alto impatto. La migrazione completa dei ~400 file è un lavoro continuativo: adottare la convenzione per ogni file toccato durante sviluppo normale.

2. **getByRole migration** — il piano stabilisce la convenzione e un esempio. Migrare 2.973 testId in blocco non è sostenibile; la regola lint + la pratica incrementale è la strategia corretta.

3. **Gamification handler tests** — i mock del `IHybridCacheService` richiedono di adattare la signature al contratto reale. Il piano include note esplicite dove leggere i sorgenti.

4. **DatabaseSync handler tests** — le signature degli handler devono essere lette prima di implementare. Il piano specifica dove trovare le informazioni.

---

*Salvato: `docs/superpowers/plans/2026-04-12-test-suite-resilience.md`*
*Data: 2026-04-12*
*Panel: Lisa Crispin, Gojko Adzic, Martin Fowler, Karl Wiegers*
