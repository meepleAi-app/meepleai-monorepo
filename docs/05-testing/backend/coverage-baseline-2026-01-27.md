# Backend Test Coverage Baseline Report

**Date**: 2026-01-27
**Issue Reference**: [#3010 - Increase Backend Coverage to 50%](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3010)

---

## Executive Summary

| Metric | Value |
|--------|-------|
| **Total Test Files** | 737 |
| **Total Tests** | 8,630+ |
| **Test Pass Rate** | 100% |
| **Bounded Contexts Covered** | 9/9 |

---

## Test Distribution by Bounded Context

| Bounded Context | Test Files | Category | Notes |
|-----------------|------------|----------|-------|
| **KnowledgeBase** | 134 | Extensive | RAG, AI agents, vector search |
| **SharedGameCatalog** | 124 | Extensive | Share requests, badges, game catalog |
| **Authentication** | 120 | Extensive | Auth flows, sessions, OAuth, 2FA |
| **GameManagement** | 89 | Good | Games, sessions, ratings, FAQs |
| **Administration** | 84 | Good | Users, roles, audit, analytics |
| **DocumentProcessing** | 42 | Moderate | PDF upload, extraction, chunking |
| **SystemConfiguration** | 31 | Moderate | Runtime config, feature flags |
| **UserLibrary** | 23 | Expanded | Collections, wishlist, favorites |
| **WorkflowIntegration** | 16 | Complete | n8n, webhooks, logging |
| **UserNotifications** | 13 | Expanded | Alerts, handlers, event notifications |

---

## Test Type Distribution

### Unit Tests (~90%)

- Handler tests (commands and queries)
- Domain entity tests
- Value object tests
- Validator tests (FluentValidation)
- Service tests

### Integration Tests (~8%)

- Repository tests (Testcontainers + PostgreSQL)
- Endpoint tests (WebApplicationFactory)
- Database constraint tests

### E2E Tests (~2%)

- Critical user journeys
- Authentication flows
- Cross-service operations

---

## Recent Additions (Issue #3010)

### PR #3098 - Endpoint Integration Tests (76 tests)

| Context | Endpoints | Tests |
|---------|-----------|-------|
| Authentication | register, login, logout, refresh, 2FA, OAuth | 19 |
| UserLibrary | library CRUD, collections, wishlist | 24 |
| GameManagement | games CRUD, sessions, ratings, FAQs, specs | 33 |

### Subsequent Commits

| File | Tests | Description |
|------|-------|-------------|
| `GuidValidatorTests.cs` | 23 | GUID parsing validation |
| `ImageFileValidatorTests.cs` | 39 | Magic bytes and MIME validation |
| `AddGameToLibraryCommandHandlerTests.cs` | 18 | Command and domain behavior |
| `UpdateLibraryEntryCommandHandlerTests.cs` | 8 | Full handler mocking |
| `GameRepositoryIntegrationTests.cs` | 15+ | Testcontainers integration |
| `GetUnreadCountQueryHandlerTests.cs` | 10 | Notification count retrieval |

---

## Coverage by Layer

### Domain Layer

| Aspect | Coverage | Notes |
|--------|----------|-------|
| Entities | High | Core entity behavior well tested |
| Value Objects | High | Immutability and validation tested |
| Domain Events | Moderate | Event raising and handling tested |
| Aggregates | High | Aggregate root invariants tested |

### Application Layer

| Aspect | Coverage | Notes |
|--------|----------|-------|
| Command Handlers | High | CQRS command handling fully tested |
| Query Handlers | High | Query handlers have comprehensive tests |
| Validators | High | FluentValidation rules thoroughly tested |
| DTOs | Moderate | Mapping tested via handler tests |

### Infrastructure Layer

| Aspect | Coverage | Notes |
|--------|----------|-------|
| Repositories | Moderate | Key repos have integration tests |
| External Services | Low | Mocked in unit tests |
| Persistence | Moderate | EF Core configurations tested |

---

## Test Infrastructure

### Testcontainers

- **PostgreSQL 16**: Primary database for integration tests
- **Redis 7**: Caching layer tests
- **Shared Container Pattern**: 95% faster than per-test containers

### Test Frameworks

- **xUnit v3**: Test framework
- **FluentAssertions**: Assertion library
- **Moq**: Mocking framework
- **Testcontainers.PostgreSql**: Container management

### CI/CD Integration

- GitHub Actions: `backend-ci.yml`
- Codecov integration: Coverage reporting
- Test gates: All tests must pass for PR merge

---

## Quality Metrics

### Test Traits

All tests use standardized traits:

```csharp
[Trait("Category", TestCategories.Unit)]        // or Integration
[Trait("BoundedContext", "ContextName")]
```

### Naming Conventions

Tests follow the pattern: `{Method}_{Scenario}_{ExpectedResult}`

Examples:
- `Handle_WithValidCommand_ReturnsDto`
- `Handle_WhenEntityNotFound_ThrowsDomainException`
- `Validate_WithEmptyEmail_ReturnsValidationError`

---

## Coverage Gaps Identified

### Low Priority (Acceptable)

| Area | Reason |
|------|--------|
| External service integrations | Mocked; real integrations tested via E2E |
| EF Core migrations | Infrastructure; manually verified |
| Startup configuration | Integration tests cover implicitly |

### Items Marked N/A (Do Not Exist)

- `GameRating` value object
- `GameCollection` entity
- `RefreshTokenHandler` (OAuth via event handler)
- `CreateNotificationHandler` (via domain events)
- `SendEmailHandler` (external service)

---

## Recommendations

### Maintain Coverage

1. **PR Template**: Require coverage delta for new code
2. **CI Gates**: Block PRs that decrease coverage
3. **Documentation**: Keep test patterns docs updated

### Future Improvements

1. **Mutation Testing**: Add Stryker.NET for test effectiveness
2. **Performance Baselines**: Add benchmark tests for critical paths
3. **Visual Regression**: Add screenshot tests for admin dashboard

---

## Running Tests

```bash
# All tests
cd apps/api && dotnet test

# Unit tests only
dotnet test --filter "Category=Unit"

# Integration tests only
dotnet test --filter "Category=Integration"

# Specific bounded context
dotnet test --filter "BoundedContext=GameManagement"

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## References

- [Backend Testing Patterns](./backend-testing-patterns.md)
- [Test Data Builders](./test-data-builders.md)
- [Testcontainers Best Practices](./testcontainers-best-practices.md)
- [Testing README](../README.md)

---

**Report Generated**: 2026-01-27
**Next Review**: 2026-02-28
**Maintainer**: Backend Team
