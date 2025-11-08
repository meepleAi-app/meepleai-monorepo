# Test Execution Guide

## Quick Reference

### Run All Tests
```bash
cd apps/api
dotnet test
```

### Run Tests Excluding Flaky OCR Tests
```bash
cd apps/api
dotnet test --filter "FullyQualifiedName!~Ocr&FullyQualifiedName!~TesseractOcrService"
```

**Why Exclude OCR Tests?**
- Docnet.Core native library has intermittent crashes (fatal error 0xC0000005)
- Tracked in issue #805
- OCR tests are integration tests, not critical for core functionality validation
- Recommended for CI/CD until native library issue resolved

### Run Only Unit Tests
```bash
cd apps/api
dotnet test --filter "FullyQualifiedName!~Integration"
```

### Run Only Integration Tests
```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~Integration"
```

### Run Specific Test Class
```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~UserManagementServiceTests"
```

## Testcontainers Monitoring (TEST-800 Phase 2)

### Enhanced Retry Logic
Phase 2 added robust connection retry logic for Testcontainers PostgreSQL:
- **Max Wait**: 15 seconds (increased from 10s)
- **Retry Delay**: 300ms (increased from 200ms)
- **Exceptions Handled**: IOException, SocketException, NpgsqlException, InvalidOperationException

### Diagnostic Logging
Look for these messages in test output:
```
✅ [QdrantRagTestFixture] Postgres connection resolved
✅ [QdrantRagTestFixture] Database migrations complete after X retries, Yms
⚠️  [QdrantRagTestFixture] Transient DB error (retry N): <ExceptionType>: <message>
```

### Troubleshooting Testcontainers Failures

**Symptom**: Tests fail with PostgreSQL connection errors
**Diagnosis**:
```bash
# Check Docker is running
docker ps

# Check Testcontainers containers
docker ps --filter "label=org.testcontainers"

# View Testcontainers logs
docker logs <container-id>
```

**Solutions**:
1. Ensure Docker Desktop is running
2. Restart Docker if containers are stuck
3. Increase timeout in QdrantRagTestFixture if needed (currently 15s)
4. Check Docker resource limits (memory, CPU)

## CI/CD Configuration

### Migration Guard (Recommendation #2)
Added `.github/workflows/migration-guard.yml` to prevent accidental migration deletion.

**Triggers**: PRs modifying `apps/api/src/Api/Migrations/**`

**Validation**:
- Compares base branch vs PR branch migrations
- Fails if existing migrations deleted
- References recovery strategies in `CRITICAL-migration-reset-impact.md`

### OCR Test Exclusion in CI (Recommendation #4)
To exclude OCR tests from CI pipeline, update `.github/workflows/ci.yml`:

```yaml
- name: Run API Tests
  run: |
    cd apps/api
    dotnet test \
      --filter "FullyQualifiedName!~Ocr&FullyQualifiedName!~TesseractOcrService" \
      --logger "trx;LogFileName=test-results.trx" \
      --verbosity minimal
```

**Benefits**:
- Prevents CI failures due to OCR native library crashes
- Faster test execution
- More reliable build pipeline

**Trade-off**:
- OCR functionality not validated in CI
- Run OCR tests manually before OCR-related changes

## Test Categories

### Unit Tests (Fast, Isolated)
- No external dependencies
- Use in-memory databases (SQLite)
- Mocked external services
- Run time: ~10-30 seconds
- **Examples**: UserManagementServiceTests, ConfigurationHelperTests

### Integration Tests (Slower, External Dependencies)
- Use Testcontainers (PostgreSQL, Qdrant)
- Real service integrations
- WebApplicationFactory
- Run time: ~1-3 minutes
- **Examples**: N8nWebhookIntegrationTests, TwoFactorAuthEndpointsTests

### OCR Tests (Flaky, Native Dependencies)
- Use Docnet.Core and Tesseract
- Native library dependencies
- Known instability (issue #805)
- **Recommendation**: Run manually or exclude from CI

## Testcontainers Best Practices

### Resource Management
```csharp
// QdrantRagTestFixture implements IAsyncLifetime
public async Task InitializeAsync()
{
    // Setup Testcontainers
    await _container.StartAsync();
}

public async Task DisposeAsync()
{
    // Cleanup
    await _container.StopAsync();
}
```

### Connection Retry Pattern
```csharp
var maxRetries = 3;
var delay = 500;

for (var attempt = 0; attempt < maxRetries; attempt++)
{
    try
    {
        // Database operation
        break;
    }
    catch (Exception ex) when (IsTransientError(ex))
    {
        if (attempt == maxRetries - 1) throw;
        await Task.Delay(delay);
    }
}
```

### Transient Error Detection
```csharp
bool IsTransientError(Exception ex) =>
    ex is IOException ||
    ex is SocketException ||
    ex is NpgsqlException ||
    ex is InvalidOperationException;
```

## Performance Benchmarks

| Test Suite | Count | Duration | Notes |
|------------|-------|----------|-------|
| All Tests (with OCR) | ~2400 | ~4-6 min | May crash on OCR tests |
| All Tests (no OCR) | ~2380 | ~3-5 min | Recommended |
| Unit Tests Only | ~1900 | ~30-60 sec | Fast feedback |
| Integration Tests | ~500 | ~2-4 min | Testcontainers startup overhead |

## Troubleshooting Common Issues

### Issue: "Docker endpoint not found"
```bash
# Start Docker Desktop
# Wait for Docker to fully initialize (30-60 seconds)
docker ps  # Verify Docker is running
```

### Issue: "Migration X has been applied but not known"
- See `docs/issue/CRITICAL-migration-reset-impact.md` for recovery
- Database reset may be required

### Issue: "Testcontainers timeout"
- Increased to 15s in Phase 2 (was 10s)
- Check Docker resource allocation
- Review logs for specific error

### Issue: "Test host crash 0xC0000005"
- OCR native library issue (#805)
- Exclude OCR tests with filter
- Run other tests normally

## Related Documentation
- `docs/issue/test-800-phase2-integration-failures.md` - Phase 2 analysis
- `docs/issue/CRITICAL-migration-reset-impact.md` - Migration safety
- `docs/code-coverage.md` - Coverage measurement
- Issue #805 - OCR crash tracking

---
*Updated: 2025-11-08 (TEST-800 completion)*
*Testcontainers improvements: Phase 2 retry logic*
