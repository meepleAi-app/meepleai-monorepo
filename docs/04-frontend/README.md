# MeepleAI Frontend Documentation

**Framework**: Next.js 16.0.1 + React 19.2.0 + TypeScript 5.9.3
**UI Library**: Shadcn/UI (Radix + Tailwind CSS 4.1.17)
**Location**: `apps/web/`
**Last Updated**: 2025-12-13T10:59:23.970Z

---

## Quick Navigation

### 🚨 Critical Documentation (Production Blockers)
1. [Internationalization Strategy](./internationalization-strategy.md) - Italian-first implementation
2. [Performance Requirements](./performance-requirements.md) - SLOs and monitoring
3. [Frontend Deployment](../deployment/frontend-deployment.md) - Production deployment guide

### 🏗️ Architecture & Patterns
4. [Frontend Architecture](./architecture.md) - Component patterns, state management, API client
5. [Use Cases & User Journeys](./use-cases.md) - Goal-oriented scenarios

### ✅ Quality & Testing
6. [Accessibility Standards](./accessibility-standards.md) - WCAG 2.1 AA compliance
7. [Testing Strategy](./testing-strategy.md) - Test pyramid, visual regression, coverage

### 🚀 Operations
8. [Disaster Recovery](../deployment/disaster-recovery.md) - Rollback, incident response

---

## Overview

MeepleAI frontend is a Next.js application providing Italian-first AI assistance for board game rules. The system emphasizes **accuracy over speed** with a target >95% accuracy on rule clarifications.

### Key Features

- **RAG-Powered Chat**: Hybrid search (vector + keyword) with citation tracking
- **PDF Upload & Processing**: Multi-file upload with game matching
- **Admin Dashboard**: Analytics, user management, prompt versioning
- **Dual Authentication**: Cookie-based sessions + API key support
- **Real-time Collaboration**: WebSocket-based chat with typing indicators

### Tech Stack

```yaml
Framework: Next.js 16.0.1
  - Pages Router (SSR/SSG)
  - API Routes for backend integration
  - Incremental Static Regeneration

Frontend:
  - React 19.2.0
  - TypeScript 5.9.3 (strict mode)
  - Shadcn/UI (16 components)
  - Tailwind CSS 4.1.17

State Management:
  - React Context (ChatProvider, feature-scoped)
  - React Hook Form (form state)
  - Native fetch (API client)

Testing:
  - Jest 30.2.0 (4,033 tests, 90%+ coverage)
  - Playwright (E2E + visual regression)
  - jest-axe (accessibility validation)
  - @testing-library/react (component testing)

Build & Deploy:
  - pnpm (package manager)
  - Next.js build (optimized bundles)
  - Docker support (production)
```

---

## Project Structure

```
apps/web/
├── src/
│   ├── components/          # Reusable UI components
│   │   ├── accessible/      # WCAG-compliant components
│   │   ├── admin/           # Admin dashboard components
│   │   ├── auth/            # Authentication UI
│   │   ├── chat/            # RAG chat interface
│   │   ├── diff/            # Diff viewer components
│   │   ├── editor/          # Rich text editor
│   │   ├── loading/         # Loading states & animations
│   │   ├── timeline/        # Event timeline components
│   │   └── ui/              # Shadcn/UI primitives
│   ├── hooks/               # Custom React hooks
│   ├── lib/                 # Utilities & API client
│   │   ├── api.ts           # Backend API client
│   │   ├── utils.ts         # Helper functions
│   │   └── animations/      # Animation utilities
│   ├── pages/               # Next.js routes
│   │   ├── admin/           # Admin routes
│   │   ├── auth/            # Authentication routes
│   │   └── *.tsx            # Public routes
│   ├── styles/              # Global styles
│   │   └── globals.css      # Tailwind + Shadcn vars
│   ├── test-utils/          # Testing utilities
│   ├── types/               # TypeScript types
│   └── __tests__/           # Test files
├── public/                  # Static assets
├── components.json          # Shadcn/UI config
├── jest.config.js           # Jest configuration
├── next.config.js           # Next.js configuration
├── package.json             # Dependencies
├── playwright.config.ts     # Playwright config
├── tailwind.config.js       # Tailwind configuration
└── tsconfig.json            # TypeScript config
```

---

## Getting Started

### Prerequisites

- Node.js 24.10.0+
- pnpm 9.15.4+
- Docker (for local backend)

### Development Setup

```bash
# Install dependencies
cd apps/web
pnpm install

# Start development server
pnpm dev
# → http://localhost:3000

# Backend API (required for full functionality)
cd ../../infra
docker compose up postgres qdrant redis
cd ../apps/api/src/Api
dotnet run
# → http://localhost:8080
```

### Available Scripts

```bash
pnpm dev              # Start dev server (port 3000)
pnpm build            # Production build
pnpm start            # Start production server
pnpm test             # Run all tests
pnpm test:watch       # Watch mode
pnpm test:coverage    # Coverage report
pnpm test:e2e         # Playwright E2E tests
pnpm test:e2e:ui      # Playwright UI mode
pnpm test:a11y        # Accessibility tests
pnpm audit:a11y       # Full a11y audit
pnpm lint             # ESLint (max 0 warnings)
pnpm lint:fix         # Auto-fix lint issues
pnpm typecheck        # TypeScript validation
```

---

## Key Concepts

### Component Organization

**Shadcn/UI Components** (`components/ui/`):
- Radix UI primitives with Tailwind styling
- Copy-paste approach (full ownership)
- Variants via class-variance-authority

**Feature Components** (`components/`):
- Domain-specific components
- Compose Shadcn/UI primitives
- Business logic integration

**Accessible Components** (`components/accessible/`):
- WCAG 2.1 AA compliant wrappers
- Screen reader optimized
- Keyboard navigation support

### State Management Philosophy

```
Server State → React Query (future)
  ↓
Component State → React Context (feature-scoped)
  ↓
Form State → React Hook Form
  ↓
URL State → Next.js Router
```

### API Integration Pattern

```typescript
// lib/api.ts - Centralized API client
// Dual auth: Cookie (session) + API Key (programmatic)
// Error handling per bounded context
// Correlation ID propagation for tracing
```

---

## Performance Targets

**Core Web Vitals** (75th percentile):
- LCP (Largest Contentful Paint): <2.5s
- FID (First Input Delay): <100ms
- CLS (Cumulative Layout Shift): <0.1

**Bundle Size**:
- Initial JS: <200KB gzipped
- Per-route chunks: <100KB gzipped

**Time to Interactive**:
- Desktop: <3.5s (aligned with backend P95)
- Mobile 3G: <8s

See [Performance Requirements](./performance-requirements.md) for details.

---

## Accessibility Standards

**Target**: WCAG 2.1 AA (minimum)
**Stretch**: WCAG 2.1 AAA for core user journeys (chat, upload)

**Validation**:
- Automated: jest-axe on every component test
- Manual: Screen reader testing quarterly
- E2E: Playwright accessibility checks in CI

See [Accessibility Standards](./accessibility-standards.md) for details.

---

## Testing Philosophy

**Coverage Target**: 90%+ (enforced in CI)
**Test Pyramid**:
- Unit: Component behavior (Jest + RTL)
- Integration: Page-level flows (Jest + RTL)
- E2E: User journeys (Playwright)
- Visual: Screenshot regression (Playwright)
- Accessibility: Automated + manual (jest-axe + NVDA)

**Quality Gates**:
- All tests passing
- 90%+ coverage maintained
- Zero critical a11y violations
- Lighthouse score >90

See [Testing Strategy](./testing-strategy.md) for details.

---

## Internationalization (i18n)

**Primary Language**: Italian (it-IT)
**Fallback**: English (en-US)

**Strategy**:
- next-i18next for translations
- Server-side locale detection
- Client-side language switching
- 100% Italian UI coverage target

See [Internationalization Strategy](./internationalization-strategy.md) for details.

---

## Contributing

### Development Workflow

1. **Create feature branch**: `git checkout -b feature/your-feature`
2. **Make changes**: Follow TypeScript strict mode, ESLint rules
3. **Write tests**: Maintain 90%+ coverage
4. **Run quality checks**:
   ```bash
   pnpm lint
   pnpm typecheck
   pnpm test
   pnpm test:a11y
   ```
5. **Create PR**: Include test evidence, screenshots for UI changes
6. **Code review**: Approval required, CI must pass

### Code Standards

- **TypeScript**: Strict mode, no `any` types
- **React**: Functional components, hooks pattern
- **Styling**: Tailwind utility classes, Shadcn components
- **Accessibility**: WCAG 2.1 AA minimum
- **Testing**: AAA pattern (Arrange, Act, Assert)

---

## Troubleshooting

### Common Issues

**Build Errors**:
```bash
# Clear Next.js cache
rm -rf .next
pnpm build
```

**Type Errors**:
```bash
# Restart TypeScript server in VS Code
Ctrl+Shift+P → "Restart TS Server"
pnpm typecheck
```

**Test Failures**:
```bash
# Clear Jest cache
pnpm test --clearCache
pnpm test
```

**Shadcn/UI Import Errors**:
- Verify `tsconfig.json` has `"@/*": ["src/*"]` path alias
- Check component exists in `src/components/ui/`
- Run `pnpm install` to ensure dependencies

### Getting Help

- **Documentation**: Check this directory's guides
- **Issues**: Search GitHub issues for similar problems
- **Backend Integration**: See `docs/api/board-game-ai-api-specification.md`
- **Architecture**: See `docs/architecture/board-game-ai-architecture-overview.md`

---

## Related Documentation

### Backend Integration
- [API Specification](../api/board-game-ai-api-specification.md)
- [Backend Architecture](../architecture/board-game-ai-architecture-overview.md)
- [CQRS/MediatR Pattern](../refactoring/ddd-status-and-roadmap.md)

### Quality & Testing
- [Test Writing Guide](../testing/test-writing-guide.md)
- [Security Scanning](../security-scanning.md)
- [SECURITY.md](../SECURITY.md)

### Deployment
- [Frontend Deployment](../deployment/frontend-deployment.md)
- [Disaster Recovery](../deployment/disaster-recovery.md)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-01-15 | Initial comprehensive frontend documentation |

---

**Maintained by**: Frontend Team
**Review Frequency**: Monthly or on major architecture changes

