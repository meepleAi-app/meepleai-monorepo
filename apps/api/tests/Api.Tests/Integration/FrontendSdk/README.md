# Frontend SDK Integration Tests

**Issue**: [#1510](https://github.com/username/meepleai-monorepo/issues/1510) - Add API SDK Integration Tests

## Purpose

These integration tests validate the **API contract** that the frontend SDK depends on. They use ASP.NET TestServer to test real HTTP behavior without mocking, ensuring the API correctly handles scenarios the frontend SDK encounters:

- ✅ Retry logic (503, 429, timeouts)
- ✅ Error handling (4xx, 5xx responses)
- ✅ Authentication flows (sessions, API keys, 2FA, OAuth)
- ✅ Request deduplication
- ✅ Circuit breaker behavior
- ✅ Rate limiting
- ✅ Streaming responses (SSE)
- ✅ File uploads (multipart/form-data)

## Architecture

```
xUnit Tests (C#)
    ↓ HttpClient
ASP.NET TestServer (in-process)
    ↓
Real API Pipeline + Testcontainers (Postgres)
```

### Why This Approach?

1. **Tests API Contract**: Validates the API behaves correctly for SDK scenarios
2. **Real HTTP**: Uses TestServer for actual HTTP pipeline testing (middleware, auth, serialization)
3. **Fast**: In-process testing (~50-100ms per test vs 2-5s for external process)
4. **Reliable**: Testcontainers provide consistent test environment
5. **Maintainable**: Leverages existing backend test infrastructure

### What About Frontend SDK Tests?

The frontend SDK at `apps/web/src/lib/api/` already has **comprehensive unit tests** with mocked fetch. Those tests validate:
- ✅ Retry logic implementation
- ✅ Error handling logic
- ✅ Request caching
- ✅ Header generation

**Gap filled by these tests**: Validation that the **API** actually returns the responses the SDK expects.

## Test per classe

- `HttpBehaviorTests.cs`: HTTP methods, headers, concurrency
- `AuthenticationFlowTests.cs`: register, login, sessions, API keys, security
- `ErrorHandlingTests.cs`: 4xx/5xx errors, validation, security
- `EdgeCaseTests.cs`: streaming, uploads, Unicode, rate limiting
- `FrontendSdkTestFactoryTests.cs`: i contratti della factory (reset fra test su tutti gli schemi, client HTTPS di default)

Quanti sono, per classe (dalla radice del repo):

```bash
grep -cE '\[(Fact|Theory)' apps/api/tests/Api.Tests/Integration/FrontendSdk/*Tests.cs
```

Quanti ne passano lo dice l'ultimo run: in CI, il `.trx` degli shard `Core` e `KnowledgeBase` di
`dev-async` (vedi «In CI» qui sotto).

## Test Structure

```
FrontendSdk/
├── README.md                           # This file
├── FrontendSdkTestFactory.cs          # Shared TestServer factory
├── HttpBehaviorTests.cs               # Retry, circuit breaker, dedup
├── AuthenticationFlowTests.cs         # Session, API key, 2FA, OAuth
├── ErrorHandlingTests.cs              # 4xx, 5xx, network errors
└── EdgeCaseTests.cs                   # Streaming, uploads, rate limiting
```

## Running Tests

```bash
# All frontend SDK integration tests
cd apps/api
dotnet test --filter "FullyQualifiedName~FrontendSdk"

# Specific test class
dotnet test --filter "FullyQualifiedName~HttpBehaviorTests"

# Verbose output
dotnet test --filter "FullyQualifiedName~FrontendSdk" --logger "console;verbosity=detailed"
```

### In CI

Sono `Category=Integration` e girano negli shard di `dev-async.yml` e `ci.yml` come gli altri
(#4518): `AuthenticationFlowTests` nello shard `KnowledgeBase` (il filtro è per nome), le altre
classi in `Core`. Con `TEST_POSTGRES_CONNSTRING` impostata la factory non avvia un container: crea
un database proprio (`frontend_sdk_<guid>`) su quel server e lo cancella alla fine, senza toccare il
database condiviso.

Due cose da sapere se scrivi un test qui:

- il client deve parlare HTTPS, perché il cookie di sessione è `secure` e su `http://` non torna
  indietro: il login riesce, la richiesta dopo è 401. `_factory.CreateClient()` senza opzioni usa
  già `FrontendSdkTestFactory.HttpsBaseAddress`; se passi le tue `WebApplicationFactoryClientOptions`
  imposta `BaseAddress = FrontendSdkTestFactory.HttpsBaseAddress`, perché non la ereditano;
- l'isolamento fra test lo dà `await _factory.ResetDatabaseAsync()` in `InitializeAsync`: svuota
  le tabelle di tutti gli schemi utente, non solo di `public` (il modello ne usa altri, per esempio
  `knowledge_base` e `SystemConfiguration`). Il contratto è in `FrontendSdkTestFactoryTests`.

## Test Patterns

### Basic Test Structure

```csharp
[Collection(nameof(FrontendSdkTestCollection))]
[Trait("Category", TestCategories.Integration)]
public class YourTests : IAsyncLifetime
{
    private readonly FrontendSdkTestFactory _factory;
    private HttpClient _client = null!;

    public YourTests(FrontendSdkTestFactory factory)
    {
        _factory = factory;
    }

    public async ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = FrontendSdkTestFactory.HttpsBaseAddress, // cookie di sessione `secure`
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        // Reset database for test isolation
        await _factory.ResetDatabaseAsync();
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task YourTest()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/endpoint");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

### Testing Retry Scenarios

```csharp
[Fact]
public async Task Get_WhenServer503_RetriesAutomatically()
{
    // This test validates that the API returns retry-friendly responses
    // Frontend SDK should see:
    // - First attempt: 503 Service Unavailable
    // - Retry-After header with delay
    // - Eventual success after retry
}
```

### Testing Authentication

```csharp
[Fact]
public async Task Login_WithValid2FA_ReturnsSessionCookie()
{
    // This test validates the API's 2FA flow
    // Frontend SDK should see:
    // - Login returns temp session
    // - 2FA verification required
    // - TOTP verification returns full session
}
```

## Performance

- **Host e database**: si costruiscono una volta per collection (`ICollectionFixture`), quindi una
  volta per shard di CI. Il costo lo paga il primo test che crea un client.
- **Reset fra test**: `FrontendSdkTestFactory.ResetDatabaseAsync`, un solo batch di `DELETE` su
  tutte le tabelle degli schemi utente con i trigger sospesi. Non è Respawn.
- Le durate per test sono nel `.trx` di ogni run: non le riportiamo qui perché invecchiano.

## Future Enhancements

### Phase 2 (Optional - Approach 1)
If needed, we could add **true frontend SDK tests** that run TypeScript code against TestServer:

```
Vitest Tests (Node.js)
    ↓ real fetch()
Node HTTP Proxy
    ↓
TestServer (C#)
```

This would test:
- ✅ Actual TypeScript SDK code
- ✅ fetch() browser-specific behavior
- ✅ JavaScript environment quirks

**Trade-off**: 3-4x more complexity and maintenance overhead for marginal additional coverage.

## References

- **Issue**: [#1510](https://github.com/username/meepleai-monorepo/issues/1510)
- **Frontend SDK**: `apps/web/src/lib/api/`
- **Integration Test Guide**: `docs/02-development/testing/backend/integration-tests-quick-reference.md`
- **Test Writing Guide**: `docs/02-development/testing/test-writing-guide.md`
- **ASP.NET TestServer Docs**: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
