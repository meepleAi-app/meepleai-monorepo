# Testing Documentation

Comprehensive testing documentation for MeepleAI, organized by testing category.

## Organization

This directory is organized into 5 main categories:

### 📚 Core (`core/`)
Main testing guides and strategies that apply across the entire application.

- **[testing-guide.md](core/testing-guide.md)** - Complete testing guide with all patterns and practices
- **[testing-strategy.md](core/testing-strategy.md)** - Overall testing strategy and test pyramid
- **[testing-quick-reference.md](core/testing-quick-reference.md)** - Quick reference for common testing tasks
- **[testing-checkpoint-guide.md](core/testing-checkpoint-guide.md)** - Testing checkpoints and checklists

### 🔧 Backend (`backend/`)
Backend-specific testing documentation (C#, xUnit, Testcontainers).

- **[backend-code-coverage.md](backend/backend-code-coverage.md)** - Code coverage setup and requirements
- **[integration-tests-quick-reference.md](backend/integration-tests-quick-reference.md)** - Quick reference for integration tests

### ⚛️ Frontend (`frontend/`)
Frontend-specific testing documentation (React, Jest, Playwright).

- **[testing-react-19-patterns.md](frontend/testing-react-19-patterns.md)** - React 19 testing patterns
- **[worker-mocking-patterns.md](frontend/worker-mocking-patterns.md)** - Web Worker mocking strategies
- **[code-coverage.md](frontend/code-coverage.md)** - Frontend code coverage setup

### ⚡ Performance (`performance/`)
Performance and load testing documentation.

- **[k6-performance-testing.md](performance/k6-performance-testing.md)** - k6 load testing guide
- **[performance-testing-guide.md](performance/performance-testing.md)** - Performance testing strategies
- **[integration-tests-performance-guide.md](performance/integration-tests-performance-guide.md)** - Integration test performance optimization

### 🎯 Specialized (`specialized/`)
Domain-specific and specialized testing guides.

- **[testing-specialized.md](specialized/testing-specialized.md)** - Specialized testing scenarios
- **[manual-testing-guide.md](specialized/manual-testing-guide.md)** - Manual testing procedures
- **[bgai-031-validation-test-coverage.md](specialized/bgai-031-validation-test-coverage.md)** - Validation test coverage
- **[bgai-039-validation-accuracy-baseline.md](specialized/bgai-039-validation-accuracy-baseline.md)** - Accuracy baseline metrics

## Quick Start

1. **New to testing?** Start with [testing-guide.md](core/testing-guide.md)
2. **Need quick commands?** Check [testing-quick-reference.md](core/testing-quick-reference.md)
3. **Backend testing?** See [backend/](backend/)
4. **Frontend testing?** See [frontend/](frontend/)
5. **Performance testing?** See [performance/](performance/)

## Coverage Requirements

- **Backend**: 90%+ (enforced by CI)
- **Frontend**: 90%+ (enforced by CI)
- **Integration**: All critical paths covered
- **E2E**: User journey scenarios

## Running Tests

### Backend
```bash
dotnet test                                          # All tests
dotnet test --filter "Category=Integration"         # Integration only
./tools/run-backend-coverage.sh --html --open        # With coverage
```

### Frontend
```bash
pnpm test                                            # Unit tests
pnpm test:e2e                                        # E2E tests
./tools/run-frontend-coverage.sh --open              # With coverage
```

### Performance
```bash
cd tests/k6
npm run test:smoke                                   # Smoke test
npm run test:load                                    # Load test
```

## Related Documentation

- [Frontend Testing Strategy](../../04-frontend/testing-strategy.md)
- [CI/CD Pipeline](../../05-operations/README.md)
- [Code Reviews](../../code-reviews/README.md)
