# Contributing to MeepleAI

Thank you for your interest in contributing to MeepleAI! This document provides guidelines and instructions for contributing to the project.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Coding Standards](#coding-standards)
- [Testing Requirements](#testing-requirements)
- [Pull Request Process](#pull-request-process)
- [Issue Guidelines](#issue-guidelines)
- [Commit Message Format](#commit-message-format)

## Code of Conduct

Be respectful, professional, and constructive in all interactions. We aim to foster an inclusive and welcoming environment for all contributors.

## Getting Started

### Prerequisites

- **Git**: Version control
- **.NET 8 SDK**: For backend development
- **Node.js 20+**: For frontend development
- **Docker Desktop**: For running the full stack locally
- **Python 3.9+**: For pre-commit hooks

### Initial Setup

1. **Fork and clone the repository**:
   ```bash
   git clone https://github.com/YOUR_USERNAME/meepleai-monorepo.git
   cd meepleai-monorepo
   ```

2. **Install pre-commit hooks** (required):
   ```bash
   python -m pip install --user -r requirements-dev.txt
   pre-commit install
   ```
   > Windows: Use `py -3 -m pip install --user -r requirements-dev.txt` if needed

3. **Set up environment files**:
   ```bash
   .\scripts\dev-up.ps1
   ```
   This creates `.env.dev` files from templates. Update them with your local secrets.

4. **Start the development stack**:
   ```bash
   cd infra
   docker compose up -d --build
   ```

5. **Verify services are running**:
   - Frontend: http://localhost:3000
   - Backend API: http://localhost:8080
   - n8n: http://localhost:5678

## Development Workflow

We follow **GitHub Flow** for all contributions:

1. **Create or select an issue**: All work should be linked to an issue
2. **Create a feature branch**: `git checkout -b feature/scope-description`
3. **Develop with TDD**: Write tests first when possible
4. **Commit frequently**: Small, descriptive commits
5. **Test locally**: Run all tests before pushing
6. **Create a Pull Request**: Use the PR template
7. **Code Review**: Address feedback
8. **Merge**: Squash merge is preferred

### Branch Naming

Use descriptive branch names with the following prefixes:

- `feature/<scope>-<short-desc>` - New features
- `fix/<scope>-<bug>` - Bug fixes
- `docs/<scope>-<desc>` - Documentation updates
- `chore/<scope>-<desc>` - Maintenance tasks
- `test/<scope>-<desc>` - Test improvements
- `refactor/<scope>-<desc>` - Code refactoring

**Examples**:
- `feature/api-pdf-import`
- `fix/web-auth-redirect`
- `docs/readme-docker-setup`

## Coding Standards

### TypeScript (Frontend)

- **Linting**: ESLint with Next.js config
- **Type Safety**: Strict mode enabled in `tsconfig.json`
- **Style**: Prettier for consistent formatting
- **Naming**:
  - Components: PascalCase (`GameCard.tsx`)
  - Functions: camelCase (`fetchGameRules()`)
  - Constants: UPPER_SNAKE_CASE (`API_BASE_URL`)

**Run checks**:
```bash
cd apps/web
npm run lint
npm run typecheck
npm test
```

### C# (Backend)

- **Nullable Reference Types**: Enabled
- **Analyzers**: StyleCop and built-in analyzers
- **Layering**: Clean architecture (Api, Services, Models)
- **Naming**:
  - Classes/Interfaces: PascalCase (`RagService`, `IRagService`)
  - Methods: PascalCase (`ProcessQueryAsync`)
  - Private fields: `_camelCase` with underscore prefix

**Run checks**:
```bash
cd apps/api
dotnet restore
dotnet build -warnaserror
dotnet test
```

### General Principles

- **Prefer pure functions** over stateful logic
- **Explicit types** over implicit/any
- **Meaningful names** over comments
- **Small, focused** functions and classes
- **DRY** (Don't Repeat Yourself)

## Testing Requirements

All contributions must include appropriate tests:

### Unit Tests

- **Frontend**: Jest with React Testing Library
  ```bash
  cd apps/web
  npm test
  ```
- **Backend**: xUnit with code coverage
  ```bash
  cd apps/api
  dotnet test --collect:"XPlat Code Coverage"
  ```

**Coverage Target**: ≥80% for modified code

### E2E Tests

- **Frontend**: Playwright for user flows
  ```bash
  cd apps/web
  npm run test:e2e
  ```
- **Backend**: Integration tests with `WebApplicationFactory`

### Test Guidelines

- **Arrange-Act-Assert** pattern
- **Descriptive test names**: `Should_ReturnError_When_InvalidGameId`
- **Mock external dependencies** (HTTP, database, file system)
- **Use fixtures** for test data
- **Verifica comportamento single-tenant**: assicurati che i flussi usino il tenant predefinito senza richiedere parametri extra.

## Pull Request Process

### Before Creating a PR

1. **Sync with main**:
   ```bash
   git checkout main
   git pull origin main
   git checkout your-branch
   git rebase main
   ```

2. **Run all checks**:
   - Linting and type checking
   - Unit tests (frontend + backend)
   - E2E tests (if applicable)
   - Pre-commit hooks pass

3. **Review your changes**:
   - No unintended files included
   - No secrets in diff
   - No debug/console logs left in code

### Creating the PR

1. **Use the PR template**: Auto-populated when creating PR
2. **Link the issue**: Use `Closes #123` or `Fixes #123`
3. **Write clear description**:
   - Summary of changes
   - Motivation and context
   - Breaking changes (if any)
   - Test evidence (screenshots, logs)

### PR Checklist

Include this checklist in your PR description:

- [ ] Issue linked (`Fixes #...`)
- [ ] Lint/typecheck ok (web/api)
- [ ] Unit test coverage ≥80% on delta
- [ ] E2E/integration tests pass
- [ ] No secrets in diff; `.env` templates updated if needed
- [ ] Modalità single-tenant verificata (nessun parametro `tenantId` richiesto dal client)
- [ ] No performance regressions
- [ ] Documentation updated (README, CHANGELOG)

### Code Review

- **Be responsive** to feedback
- **Ask questions** if feedback is unclear
- **Discuss alternatives** constructively
- **Update your branch** as you address comments

### Merge Requirements

A PR can be merged when:

✅ All CI checks pass
✅ At least one approving review (for external contributors)
✅ All conversations resolved
✅ Branch up-to-date with main

## Issue Guidelines

### Creating Issues

Use issue templates when available. Include:

- **Clear title**: Descriptive and actionable
- **Context**: What were you trying to do?
- **Steps to reproduce** (for bugs)
- **Expected vs actual behavior**
- **Environment**: OS, browser, versions
- **Root cause hypothesis** (if known)
- **Acceptance criteria**: How to verify the fix

### Issue Labels

- **Type**: `kind/bug`, `kind/feature`, `kind/docs`, `kind/chore`
- **Area**: `area/web`, `area/api`, `area/infra`, `area/docs`
- **Priority**: `priority/p0` (critical), `priority/p1` (high), `priority/p2` (medium), `priority/p3` (low)
- **Tenant**: utilizzare label `tenant/*` solo per lavori legati a futuri scenari multi-tenant

### Issue Workflow

1. **Triage**: Maintainers will label and prioritize
2. **Assignment**: Comment if you want to work on an issue
3. **Work**: Create a branch and reference the issue
4. **Resolution**: PR merged, issue auto-closes

## Commit Message Format

We use **Conventional Commits** for clear history and automated changelogs:

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types

- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Formatting, missing semicolons, etc.
- `refactor`: Code change that neither fixes a bug nor adds a feature
- `perf`: Performance improvement
- `test`: Adding or updating tests
- `chore`: Maintenance (dependencies, build, etc.)

### Scope

The scope should specify the affected component:
- `web`, `api`, `infra`, `docs`, `tests`, `ci`

### Examples

```
feat(api): add PDF import endpoint

Implements the /ingest/pdf endpoint with chunking and
embedding generation for rule documents.

Closes #42
```

```
fix(web): correct authentication redirect loop

Users were stuck in redirect loop after login when
accessing protected routes. Fixed by checking session
state before redirecting.

Fixes #58
```

```
docs(readme): update Docker setup instructions

Add troubleshooting section for common Docker issues
on Windows and macOS.
```

### Footer

Use the footer to reference issues:

- `Closes #123` - Closes an issue (use for PRs)
- `Fixes #123` - Fixes a bug issue
- `Refs #123` - References an issue without closing

## Security

**Never commit secrets!** Pre-commit hooks will help prevent this, but always:

- Use `.env.dev` files locally (not committed)
- Use environment variables in CI/CD
- Rotate any accidentally committed secrets immediately
- Report security vulnerabilities privately (see [SECURITY.md](SECURITY.md))

## Additional Resources

- **Project Architecture**: See [CLAUDE.md](CLAUDE.md)
- **Agent Guidelines**: See [agents.md](agents.md)
- **Security Policy**: See [SECURITY.md](SECURITY.md)
- **API Documentation**: Available in `apps/api/README.md`
- **Frontend Guide**: Available in `apps/web/README.md`

## Questions?

- **GitHub Discussions**: For general questions and ideas
- **GitHub Issues**: For bugs and feature requests
- **Pull Requests**: For code contributions

---

**Thank you for contributing to MeepleAI!**
