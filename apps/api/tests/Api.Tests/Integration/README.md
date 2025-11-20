# Integration Tests

## Test Organization

Integration tests are organized by bounded context and dependencies using xUnit traits (tags).

## Running Tests Selectively

### By Category

```bash
# Run all integration tests
dotnet test --filter "Category=Integration"

# Run only E2E tests (end-to-end with real services)
dotnet test --filter "Category=E2E"

# Run cross-context workflow tests
dotnet test --filter "Category=CrossContext"
```

### By Dependency

```bash
# Run tests that require PostgreSQL
dotnet test --filter "Dependency=PostgreSQL"

# Run tests that require Testcontainers (Docker)
dotnet test --filter "Dependency=Testcontainers"
```

### By Bounded Context

```bash
# Run Authentication tests
dotnet test --filter "BoundedContext=Authentication"

# Run DocumentProcessing tests
dotnet test --filter "BoundedContext=DocumentProcessing"

# Run FullStack tests
dotnet test --filter "BoundedContext=FullStack"
```

### Combined Filters

```bash
# Run PostgreSQL integration tests excluding E2E
dotnet test --filter "Dependency=PostgreSQL&Category!=E2E"

# Run Authentication integration tests
dotnet test --filter "BoundedContext=Authentication&Category=Integration"
```

## Available Tags

### Categories
- **Integration**: Database-backed integration tests
- **E2E**: End-to-end tests with real external services
- **CrossContext**: Tests spanning multiple bounded contexts

### Dependencies
- **PostgreSQL**: Requires PostgreSQL database (via Testcontainers)
- **Testcontainers**: Requires Docker for service containers

### Bounded Contexts
- **Authentication**: OAuth, sessions, API keys, 2FA
- **DocumentProcessing**: PDF processing pipeline
- **FullStack**: Cross-context workflows

## Test Patterns

### Integration Test Base
```csharp
[Trait("Category", "Integration")]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
public class MyIntegrationTests : IntegrationTestBase<MyRepository>
{
    protected override string DatabaseName => "my_test_db";

    [Fact]
    public async Task MyTest()
    {
        // Arrange
        await ResetDatabaseAsync(); // Ensure test isolation

        // Act & Assert
        // ...
    }
}
```

### Performance Considerations
- **Fast**: Unit tests + Integration tests without Testcontainers (~2min)
- **Medium**: + E2E tests with Testcontainers (~5min)
- **Full**: All tests including performance/quality (~15min)

## CI/CD Integration

GitHub Actions workflows can use these filters to run test subsets:

```yaml
# Fast CI (on PR)
- run: dotnet test --filter "Category=Integration&Dependency!=Testcontainers"

# Full CI (on main)
- run: dotnet test --filter "Category=Integration|Category=E2E"
```

## Documentation

For more information:
- **Testing Guide**: `docs/02-development/testing/core/testing-guide.md`
- **Backend Coverage**: `docs/02-development/testing/backend/backend-code-coverage.md`
