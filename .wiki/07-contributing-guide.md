# Contributing Guide - MeepleAI

**Audience**: Anyone who wants to contribute to the MeepleAI project.

## 📋 Table of Contents

1. [Welcome](#welcome)
2. [Code of Conduct](#code-of-conduct)
3. [Getting Started](#getting-started)
4. [How to Contribute](#how-to-contribute)
5. [Development Workflow](#development-workflow)
6. [Coding Standards](#coding-standards)
7. [Pull Request Process](#pull-request-process)
8. [Testing Requirements](#testing-requirements)
9. [Documentation](#documentation)
10. [Community](#community)

## 👋 Welcome

Thank you for considering contributing to MeepleAI! We're excited to have you join our community. Whether you're fixing bugs, adding features, improving documentation, or helping with testing, your contributions are valued.

### Ways to Contribute

- 🐛 **Bug Reports**: Found a bug? Let us know!
- ✨ **Feature Requests**: Have an idea? We'd love to hear it!
- 🔧 **Code Contributions**: Fix bugs or implement features
- 📝 **Documentation**: Improve guides, add examples, fix typos
- 🧪 **Testing**: Write tests, improve coverage
- 🎨 **Design**: UI/UX improvements
- 🌍 **Translation**: Help with internationalization
- 💬 **Community**: Answer questions, help others

## 📜 Code of Conduct

### Our Pledge

We are committed to providing a welcoming and inclusive environment for everyone, regardless of:
- Experience level
- Gender identity and expression
- Sexual orientation
- Disability
- Personal appearance
- Body size
- Race
- Ethnicity
- Age
- Religion
- Nationality

### Our Standards

**Positive behavior**:
- Using welcoming and inclusive language
- Being respectful of differing viewpoints
- Gracefully accepting constructive criticism
- Focusing on what is best for the community
- Showing empathy towards others

**Unacceptable behavior**:
- Harassment, trolling, or insulting comments
- Public or private harassment
- Publishing others' private information
- Other conduct inappropriate in a professional setting

### Enforcement

Violations of the code of conduct may be reported to the project maintainers. All complaints will be reviewed and investigated promptly and fairly.

## 🚀 Getting Started

### Prerequisites

Before contributing, ensure you have:
1. **Git** installed
2. **.NET 9 SDK** installed
3. **Node.js 20+** and **pnpm** installed
4. **Docker Desktop** running
5. A **GitHub account**

### Setup Development Environment

**1. Fork the Repository**:
- Go to https://github.com/DegrassiAaron/meepleai-monorepo
- Click "Fork" in the top right
- This creates a copy under your account

**2. Clone Your Fork**:
```bash
git clone https://github.com/YOUR_USERNAME/meepleai-monorepo.git
cd meepleai-monorepo
```

**3. Add Upstream Remote**:
```bash
git remote add upstream https://github.com/DegrassiAaron/meepleai-monorepo.git
```

**4. Setup Environment**:
```bash
# Start services
cd infra
docker compose up -d

# Configure environment
cp env/.env.example env/.env.dev
# Edit env/.env.dev with your values

# Start API
cd ../apps/api/src/Api
dotnet restore
dotnet build
dotnet run

# Start Web (in new terminal)
cd apps/web
pnpm install
pnpm dev
```

**5. Verify Setup**:
```bash
# API health check
curl http://localhost:5080/health

# Frontend
curl http://localhost:3000
```

### Finding Issues to Work On

**Good First Issues**:
- Look for issues labeled `good first issue`
- These are beginner-friendly and well-documented
- Perfect for first-time contributors

**Help Wanted**:
- Issues labeled `help wanted` need contributors
- May require more experience
- Often have detailed requirements

**Browse Issues**: https://github.com/DegrassiAaron/meepleai-monorepo/issues

## 🤝 How to Contribute

### Reporting Bugs

**Before submitting**:
1. Check existing issues to avoid duplicates
2. Verify the bug exists in the latest version
3. Gather relevant information

**Bug Report Template**:
```markdown
**Description**
A clear description of the bug.

**Steps to Reproduce**
1. Go to '...'
2. Click on '...'
3. Scroll down to '...'
4. See error

**Expected Behavior**
What you expected to happen.

**Actual Behavior**
What actually happened.

**Screenshots**
If applicable, add screenshots.

**Environment**
- OS: [e.g., Windows 11, macOS 14, Ubuntu 22.04]
- Browser: [e.g., Chrome 119, Firefox 120]
- Version: [e.g., 1.0.0]

**Additional Context**
Any other relevant information.
```

### Requesting Features

**Feature Request Template**:
```markdown
**Problem Statement**
What problem does this solve?

**Proposed Solution**
How would you like to solve it?

**Alternatives Considered**
What other options did you consider?

**Use Cases**
Who would benefit from this feature?

**Additional Context**
Mockups, examples, related features, etc.
```

### Contributing Code

See [Development Workflow](#development-workflow) below.

### Contributing Documentation

**Types of Documentation**:
- **Guides**: Step-by-step tutorials
- **Reference**: API docs, architecture docs
- **Examples**: Code samples, use cases
- **Wiki**: User guides (this!)

**Documentation Standards**:
- Use clear, concise language
- Include code examples
- Add screenshots where helpful
- Keep it up-to-date with code changes

## 🔄 Development Workflow

### 1. Create a Feature Branch

```bash
# Ensure you're on main and up-to-date
git checkout main
git pull upstream main

# Create feature branch
git checkout -b feature/your-feature-name

# Or for bug fixes
git checkout -b fix/bug-description
```

**Branch Naming**:
- `feature/` - New features
- `fix/` - Bug fixes
- `docs/` - Documentation updates
- `refactor/` - Code refactoring
- `test/` - Adding/updating tests
- `chore/` - Maintenance tasks

### 2. Make Your Changes

Follow the [Coding Standards](#coding-standards) section below.

**Development Cycle**:
1. Write code
2. Write tests
3. Run tests locally
4. Fix any issues
5. Commit changes

### 3. Test Your Changes

```bash
# Backend tests
cd apps/api
dotnet test

# Frontend tests
cd apps/web
pnpm test

# E2E tests
pnpm test:e2e

# Lint
pnpm lint

# Type check
pnpm typecheck
```

**Quality Requirements**:
- All tests must pass ✅
- Coverage must be ≥90% ✅
- No linting errors ✅
- Type check passes ✅

### 4. Commit Your Changes

**Commit Message Format**:
```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Formatting, missing semicolons, etc.
- `refactor`: Code change that neither fixes a bug nor adds a feature
- `test`: Adding tests
- `chore`: Updating build tasks, package manager configs, etc.

**Examples**:
```bash
git commit -m "feat(auth): Add 2FA support with TOTP"

git commit -m "fix(rag): Fix hallucination detection in validation layer

The validation was incorrectly flagging valid responses. Updated
the forbidden keywords list and improved confidence scoring.

Fixes #123"

git commit -m "docs(wiki): Add user guide for 2FA setup"
```

### 5. Push to Your Fork

```bash
git push origin feature/your-feature-name
```

### 6. Create Pull Request

**On GitHub**:
1. Go to your fork
2. Click "Pull Request"
3. Select your branch
4. Fill out the PR template
5. Click "Create Pull Request"

**PR Title**: Same format as commit messages
```
feat(auth): Add 2FA support
```

**PR Description Template**:
```markdown
## Description
Brief description of the changes.

## Type of Change
- [ ] Bug fix (non-breaking change fixing an issue)
- [ ] New feature (non-breaking change adding functionality)
- [ ] Breaking change (fix or feature causing existing functionality to change)
- [ ] Documentation update

## Related Issues
Fixes #123
Related to #456

## Changes Made
- Added 2FA support with TOTP
- Updated user settings UI
- Added tests for 2FA flow

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] E2E tests added/updated
- [ ] Manual testing completed

## Checklist
- [ ] Code follows project style guidelines
- [ ] Self-review completed
- [ ] Comments added for complex code
- [ ] Documentation updated
- [ ] No new warnings generated
- [ ] Tests added and passing
- [ ] Coverage maintained (≥90%)

## Screenshots (if applicable)
![Screenshot description](url)

## Additional Notes
Any additional information for reviewers.
```

## 📐 Coding Standards

### C# / Backend

**Style Guide**:
```csharp
// ✅ Good
public class GameService : IGameService
{
    private readonly IGameRepository _repository;
    private readonly ILogger<GameService> _logger;

    public GameService(
        IGameRepository repository,
        ILogger<GameService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Game> GetGameAsync(int gameId, CancellationToken ct)
    {
        _logger.LogInformation("Getting game {GameId}", gameId);

        var game = await _repository.GetByIdAsync(gameId, ct);

        if (game == null)
            throw new NotFoundException($"Game {gameId} not found");

        return game;
    }
}

// ❌ Bad
public class gameservice  // PascalCase required
{
    IGameRepository repo;  // No access modifier, no naming convention

    public Game GetGame(int id)  // Not async, no cancellation token
    {
        return repo.GetById(id);  // Blocking call
    }
}
```

**Best Practices**:
- ✅ Use PascalCase for classes, methods, properties
- ✅ Use camelCase for parameters, local variables
- ✅ Prefix interfaces with `I`
- ✅ Use `var` when type is obvious
- ✅ Async methods end with `Async`
- ✅ Always pass `CancellationToken`
- ✅ Use `ILogger` for logging
- ✅ Dispose resources with `using`
- ✅ Use nullable reference types

### TypeScript / Frontend

**Style Guide**:
```typescript
// ✅ Good
import { useState } from 'react'
import { Button } from '@/components/ui/button'

interface GameCardProps {
  gameId: number
  title: string
  onSelect?: (id: number) => void
}

export const GameCard: React.FC<GameCardProps> = ({ gameId, title, onSelect }) => {
  const [isSelected, setIsSelected] = useState(false)

  const handleClick = () => {
    setIsSelected(true)
    onSelect?.(gameId)
  }

  return (
    <div>
      <h3>{title}</h3>
      <Button onClick={handleClick}>Select</Button>
    </div>
  )
}

// ❌ Bad
export const gamecard = (props) => {  // PascalCase required, no types
  var selected = false  // Use const, not var

  return (
    <div onClick={() => props.onSelect(props.id)}>  // Use destructuring
      {props.title}
    </div>
  )
}
```

**Best Practices**:
- ✅ Use TypeScript, avoid `any`
- ✅ PascalCase for components, types, interfaces
- ✅ camelCase for variables, functions
- ✅ Prefer `const` over `let`, never `var`
- ✅ Use arrow functions
- ✅ Extract reusable components
- ✅ Use Shadcn/UI components
- ✅ Handle loading and error states

### Git Commits

**Do**:
- ✅ Write clear, descriptive messages
- ✅ Use conventional commit format
- ✅ Keep commits focused and atomic
- ✅ Reference issues when applicable

**Don't**:
- ❌ Commit commented-out code
- ❌ Commit debug statements
- ❌ Commit merge conflicts
- ❌ Use vague messages ("fix stuff", "wip")

## 🔍 Pull Request Process

### 1. PR Submission

**Before submitting**:
- All tests pass locally
- Code is linted and formatted
- Documentation is updated
- Branch is up-to-date with main

### 2. Code Review

**What reviewers check**:
- Code quality and style
- Test coverage
- Documentation
- Performance implications
- Security considerations
- Breaking changes

**Review Process**:
1. Automated checks run (CI/CD)
2. Maintainers review code
3. Feedback provided (if needed)
4. Author addresses feedback
5. Re-review (if changes made)
6. Approval

**Addressing Feedback**:
```bash
# Make requested changes
# Commit changes
git add .
git commit -m "Address review feedback"

# Push to same branch
git push origin feature/your-feature-name
```

### 3. Merging

**Merge Criteria**:
- ✅ All checks passing
- ✅ At least one approval
- ✅ No unresolved conversations
- ✅ Branch up-to-date with main
- ✅ No merge conflicts

**Merge Methods**:
- **Squash and Merge** (default): Combines all commits into one
- **Rebase and Merge**: Maintains individual commits
- **Merge Commit**: Creates a merge commit

### 4. Post-Merge

**After merge**:
- Delete feature branch
- Update local repository
- Close related issues

```bash
# Update local main
git checkout main
git pull upstream main

# Delete local branch
git branch -d feature/your-feature-name

# Delete remote branch (if not auto-deleted)
git push origin --delete feature/your-feature-name
```

## 🧪 Testing Requirements

### Coverage Requirements

All contributions must maintain **≥90% test coverage**.

### Test Types

**Unit Tests** (Required):
```csharp
[Fact]
public async Task GetGame_ValidId_ReturnsGame()
{
    // Arrange
    var repository = new Mock<IGameRepository>();
    var game = new Game { Id = 1, Title = "Catan" };
    repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
        .ReturnsAsync(game);

    // Act
    var result = await handler.Handle(new GetGameQuery(1), CancellationToken.None);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("Catan", result.Title);
}
```

**Integration Tests** (Recommended):
```csharp
[Fact]
public async Task CreateGame_ValidData_SavesToDatabase()
{
    // Uses Testcontainers for real database
    var game = new Game { Title = "Catan" };
    await _repository.AddAsync(game, CancellationToken.None);

    var saved = await _repository.GetByIdAsync(game.Id, CancellationToken.None);
    Assert.NotNull(saved);
}
```

**E2E Tests** (For UI changes):
```typescript
test('user can create a game', async ({ page }) => {
  await page.goto('/games/new')
  await page.fill('input[name="title"]', 'Catan')
  await page.click('button[type="submit"]')
  await expect(page.locator('text=Game created')).toBeVisible()
})
```

### Running Tests

```bash
# Backend (all tests)
cd apps/api
dotnet test

# Backend (specific test)
dotnet test --filter "FullyQualifiedName~GameTests"

# Frontend (all tests)
cd apps/web
pnpm test

# Frontend (watch mode)
pnpm test --watch

# E2E
pnpm test:e2e

# Coverage report
dotnet test /p:CollectCoverage=true
pnpm test --coverage
```

## 📝 Documentation

### Required Documentation

**For all code contributions**:
- XML comments for public APIs (C#)
- JSDoc comments for public functions (TypeScript)
- README updates (if adding new features)
- Wiki updates (if affecting user workflows)

**Examples**:

**C# XML Comments**:
```csharp
/// <summary>
/// Retrieves a game by its unique identifier.
/// </summary>
/// <param name="gameId">The unique identifier of the game.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>The game if found, null otherwise.</returns>
/// <exception cref="NotFoundException">Thrown when game is not found.</exception>
public async Task<Game> GetGameAsync(int gameId, CancellationToken ct)
{
    // Implementation
}
```

**TypeScript JSDoc**:
```typescript
/**
 * Fetches a game by ID from the API.
 * @param gameId - The unique identifier of the game
 * @returns Promise resolving to the game data
 * @throws Error if the request fails
 */
export async function getGame(gameId: number): Promise<Game> {
  // Implementation
}
```

### Documentation Updates

**When to update docs**:
- Adding new features
- Changing APIs
- Modifying workflows
- Fixing bugs that affect documented behavior

**Where to update**:
- `README.md` - Project overview
- `CLAUDE.md` - Quick reference
- `.wiki/` - User-facing guides
- `docs/` - Technical documentation

## 💬 Community

### Getting Help

**Channels**:
1. **GitHub Discussions**: Ask questions, share ideas
2. **GitHub Issues**: Report bugs, request features
3. **Discord/Slack**: Real-time chat (if available)

**Before asking**:
- Search existing issues/discussions
- Check the documentation
- Try debugging yourself

**When asking**:
- Provide context
- Include error messages
- Share minimal reproducible example
- Be respectful and patient

### Recognition

**Contributors are recognized**:
- In the README.md contributors section
- In release notes
- On the project website (if available)

**Ways to earn recognition**:
- Quality code contributions
- Helpful code reviews
- Great documentation
- Assisting other contributors
- Bug reports and testing

## 🎉 Thank You!

Thank you for contributing to MeepleAI! Your efforts help make this project better for everyone. We appreciate your time, skills, and dedication.

### Additional Resources

- **[Developer Guide](./02-developer-guide.md)** - Development setup and workflow
- **[Testing Guide](./03-testing-guide.md)** - Testing procedures
- **[Architecture Guide](./06-architecture-guide.md)** - System architecture
- **[Main Documentation](../docs/INDEX.md)** - Complete documentation

---

**Version**: 1.0-rc
**Last Updated**: 2025-11-15
**For**: Contributors

**Happy Contributing! 🚀**
