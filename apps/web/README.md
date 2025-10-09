# MeepleAI Web Frontend

Next.js 14 frontend for the MeepleAI board game rules assistant.

## Tech Stack

- **Framework**: Next.js 14 with Pages Router
- **Language**: TypeScript
- **UI**: React 18
- **Testing**: Jest with React Testing Library, Playwright for E2E
- **Package Manager**: pnpm 9

## Development

```bash
# Install dependencies
pnpm install

# Run development server (http://localhost:3000)
pnpm dev

# Build for production
pnpm build

# Start production server
pnpm start

# Run linter
pnpm lint

# Fix linting issues
pnpm lint:fix

# Type checking
pnpm typecheck
```

## Testing

```bash
# Run unit tests
pnpm test

# Run tests in watch mode
pnpm test:watch

# Run tests with coverage
pnpm test:coverage

# Run E2E tests
pnpm test:e2e

# Run E2E tests with UI
pnpm test:e2e:ui

# Show E2E test report
pnpm test:e2e:report
```

### Test Naming Convention

All tests should follow BDD-style naming conventions:

```typescript
it('should [expected behavior] when [condition]')
```

**Examples:**
```typescript
it('should disable upload button when no game is selected')
it('should show error message when API returns 500')
it('should redirect to login when user is not authenticated')
```

For complete testing guidelines including backend patterns, common scenarios, and anti-patterns, see:
- **[Testing Guidelines (README.test.md)](../../README.test.md)** - Comprehensive guide for both frontend and backend testing

### Coverage Requirements

- **Branches**: 90%
- **Functions**: 90%
- **Lines**: 90%
- **Statements**: 90%

## Project Structure

```
src/
  pages/           # Next.js pages
    index.tsx      # Landing/dashboard
    chat.tsx       # AI chat interface
    upload.tsx     # PDF upload and processing
    editor.tsx     # Rule specification editor
    versions.tsx   # Rule version comparison
    admin.tsx      # Admin dashboard
    n8n.tsx        # Workflow management
    logs.tsx       # Activity logs
  lib/             # Shared utilities
    api.ts         # Centralized API client
  __tests__/       # Unit tests
```

## API Client

The centralized API client is available at `@/lib/api`:

```typescript
import api from '@/lib/api';

// GET request
const data = await api.get('/endpoint');

// POST request
const result = await api.post('/endpoint', { body: data });

// PUT request
await api.put('/endpoint/:id', { body: data });

// DELETE request
await api.delete('/endpoint/:id');
```

Features:
- Automatic base URL configuration from `NEXT_PUBLIC_API_BASE`
- Cookie-based authentication (`credentials: "include"`)
- 401 handling for auth failures

## Environment Variables

Create `.env.local` file:

```bash
NEXT_PUBLIC_API_BASE=http://localhost:8080
```

See `infra/env/web.env.dev.example` for all available variables.

## Additional Documentation

- **[Project Overview (CLAUDE.md)](../../CLAUDE.md)** - Complete monorepo documentation
- **[Testing Guidelines (README.test.md)](../../README.test.md)** - Test naming conventions and best practices
