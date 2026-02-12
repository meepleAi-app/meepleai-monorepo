# [Feature/System] - Troubleshooting Guide

## Quick Diagnosis Table

| Symptom | Likely Cause | Quick Fix | Deep Fix |
|---------|--------------|-----------|----------|
| **Build fails** | Missing dependencies | `dotnet restore` / `pnpm install` | Check `.csproj` / `package.json` |
| **Tests fail** | DB not running | `docker compose up -d postgres` | Check connection string |
| **API 500 error** | Unhandled exception | Check logs | Add try-catch + proper exception |
| **API 404 error** | Route not mapped | Check endpoint registration | Verify routing configuration |
| **Slow queries** | Missing indexes | Add `[Index]` attribute | Analyze query plan |
| **Auth fails** | Invalid token | Check token expiry | Verify JWT configuration |
| **Cache stale** | No invalidation | Manual cache clear | Add cache invalidation logic |
| **Hydration error** | SSR/Client mismatch | Remove `Math.random()` | Use deterministic data |

## Decision Tree: Build Issues

```
Build fails?
├─ Backend (.NET)?
│   ├─ Missing references?
│   │   ├─ Run: dotnet restore
│   │   └─ Check: .csproj for package versions
│   │
│   ├─ EF Core migration error?
│   │   ├─ Check: Docker postgres running
│   │   ├─ Run: dotnet ef database update
│   │   └─ Verify: Connection string in secrets
│   │
│   ├─ Compilation error?
│   │   ├─ Check: Sonar warnings (CS*****)
│   │   ├─ Fix: Unused usings, nullable warnings
│   │   └─ Run: dotnet clean && dotnet build
│   │
│   └─ Test failures?
│       ├─ Check: Testhost blocking (taskkill)
│       ├─ Run: dotnet test --no-build
│       └─ Fix: Failing test assertions
│
└─ Frontend (Next.js)?
    ├─ Missing dependencies?
    │   ├─ Run: pnpm install
    │   └─ Check: lockfile consistency
    │
    ├─ Type errors?
    │   ├─ Run: pnpm typecheck
    │   ├─ Fix: TypeScript errors
    │   └─ Check: tsconfig.json paths
    │
    ├─ Build cache issues?
    │   ├─ Run: rm -rf .next
    │   ├─ Run: pnpm build
    │   └─ Check: next.config.js
    │
    └─ Environment variables?
        ├─ Check: .env.local exists
        ├─ Verify: Required vars set
        └─ Restart: pnpm dev
```

## Decision Tree: Runtime Errors

```
Runtime error?
├─ API errors (Backend)?
│   ├─ 401 Unauthorized?
│   │   ├─ Check: Token in Authorization header
│   │   ├─ Verify: Token not expired
│   │   ├─ Test: Get fresh token via /auth/login
│   │   └─ Debug: JWT configuration in secrets
│   │
│   ├─ 403 Forbidden?
│   │   ├─ Check: User roles/permissions
│   │   ├─ Verify: Endpoint authorization policy
│   │   └─ Debug: Claims in token
│   │
│   ├─ 404 Not Found?
│   │   ├─ Check: Endpoint URL spelling
│   │   ├─ Verify: Route is mapped (Routing/)
│   │   ├─ Check: HTTP method (GET/POST/PUT/DELETE)
│   │   └─ Debug: Scalar API docs (:8080/scalar/v1)
│   │
│   ├─ 409 Conflict?
│   │   ├─ Check: Business rule violation
│   │   ├─ Verify: Unique constraints
│   │   └─ Fix: Handle ConflictException
│   │
│   └─ 500 Internal Error?
│       ├─ Check: Application logs (docker logs)
│       ├─ Verify: Database connectivity
│       ├─ Debug: Exception stack trace
│       └─ Fix: Add proper error handling
│
└─ UI errors (Frontend)?
    ├─ Hydration mismatch?
    │   ├─ Check: Math.random() usage
    │   ├─ Fix: Use deterministic data
    │   ├─ Verify: SSR vs client rendering
    │   └─ Debug: Suppress warnings temporarily
    │
    ├─ API call fails?
    │   ├─ Check: Network tab (DevTools)
    │   ├─ Verify: Endpoint URL correct
    │   ├─ Check: CORS configuration
    │   └─ Debug: Response body/status
    │
    ├─ State not updating?
    │   ├─ Check: Zustand store mutations
    │   ├─ Verify: React Query cache
    │   ├─ Debug: React DevTools
    │   └─ Fix: Immutable state updates
    │
    └─ Component not rendering?
        ├─ Check: Conditional logic
        ├─ Verify: Data availability
        ├─ Debug: Console errors
        └─ Fix: Loading/error states
```

## Common Error Messages

### Backend Errors

| Error Message | Cause | Solution |
|---------------|-------|----------|
| `Unable to connect to database` | Postgres not running | `docker compose up -d postgres` |
| `The entity type 'X' requires a primary key` | Missing `[Key]` or `Id` property | Add `public Guid Id { get; set; }` |
| `A circular dependency was detected` | DI configuration issue | Check service registrations |
| `No service for type 'IX' has been registered` | Missing DI registration | Add `services.AddScoped<IX, X>()` |
| `The LINQ expression could not be translated` | Complex LINQ query | Split into client evaluation |
| `Validation failed: Field 'X' is required` | FluentValidation rule | Fix request data or validation rule |
| `DbUpdateConcurrencyException` | Row version conflict | Handle optimistic concurrency |

**Example Fix**:
```csharp
// Error: No service for type 'IGameSessionRepository'
// Fix: Add to DI registration
services.AddScoped<IGameSessionRepository, GameSessionRepository>();
```

---

### Frontend Errors

| Error Message | Cause | Solution |
|---------------|-------|----------|
| `Hydration failed because initial UI does not match` | SSR/client mismatch | Remove `Math.random()`, use deterministic data |
| `Cannot read property 'X' of undefined` | Missing null check | Add optional chaining `?.` |
| `Network request failed` | API unreachable | Check API running, CORS config |
| `Invalid hook call` | Hooks outside component | Move hooks inside component body |
| `Maximum update depth exceeded` | Infinite re-render | Fix dependency array in `useEffect` |
| `Type 'X' is not assignable to type 'Y'` | TypeScript type mismatch | Fix type definitions |

**Example Fix**:
```tsx
// Error: Hydration mismatch from Math.random()
// ❌ WRONG
const mockData = Array.from({ length: 10 }, () => ({
  id: Math.random().toString(),
  value: Math.random() * 100,
}));

// ✅ CORRECT
const mockData = Array.from({ length: 10 }, (_, i) => ({
  id: `item-${i + 1}`,
  value: (i + 1) * 10,
}));
```

## Decision Tree: Performance Issues

```
Performance problem?
├─ Slow API response?
│   ├─ Database query slow?
│   │   ├─ Check: Query execution plan
│   │   ├─ Add: Indexes on filtered columns
│   │   ├─ Optimize: Use Select() to project
│   │   └─ Fix: N+1 queries with Include()
│   │
│   ├─ No caching?
│   │   ├─ Add: HybridCache for queries
│   │   ├─ Set: Appropriate TTL
│   │   └─ Implement: Cache invalidation
│   │
│   ├─ Too much data?
│   │   ├─ Add: Pagination (Skip/Take)
│   │   ├─ Implement: Filtering
│   │   └─ Use: Projection (Select)
│   │
│   └─ Blocking operations?
│       ├─ Check: Async/await usage
│       ├─ Move: Long operations to background jobs
│       └─ Use: Quartz.NET for scheduling
│
└─ Slow UI rendering?
    ├─ Too many re-renders?
    │   ├─ Check: useEffect dependencies
    │   ├─ Use: React.memo for expensive components
    │   ├─ Optimize: State updates (batch)
    │   └─ Debug: React DevTools Profiler
    │
    ├─ Large bundle size?
    │   ├─ Check: Bundle analyzer
    │   ├─ Use: Dynamic imports (lazy loading)
    │   ├─ Remove: Unused dependencies
    │   └─ Split: Code by routes
    │
    ├─ Slow data fetching?
    │   ├─ Use: React Query for caching
    │   ├─ Implement: Prefetching
    │   ├─ Add: Loading states
    │   └─ Optimize: API pagination
    │
    └─ Heavy computations?
        ├─ Memoize: Expensive calculations (useMemo)
        ├─ Debounce: User input (useDebounce)
        ├─ Throttle: Scroll/resize handlers
        └─ Move: To Web Workers (if needed)
```

## Database Issues

### Connection Problems

| Symptom | Check | Fix |
|---------|-------|-----|
| **Can't connect** | Docker container running? | `docker compose up -d postgres` |
| **Wrong credentials** | Secrets file correct? | Check `infra/secrets/database.secret` |
| **Port conflict** | Port 5432 in use? | `netstat -ano \| findstr :5432` → kill process |
| **Container crashed** | Container logs? | `docker logs meepleai-postgres` |

**Commands**:
```bash
# Check container status
docker compose ps

# View logs
docker logs meepleai-postgres --tail=50

# Restart container
docker compose restart postgres

# Reset database (⚠️ DATA LOSS)
docker compose down -v && docker compose up -d postgres
```

---

### Migration Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| **Migration fails** | Database state mismatch | Apply missing migrations |
| **Constraint violation** | Data incompatible with schema | Clean data or modify migration |
| **Pending migrations** | Not applied | `dotnet ef database update` |

**Commands**:
```bash
# Check migration status
dotnet ef migrations list

# Apply migrations
dotnet ef database update

# Rollback last migration
dotnet ef database update <PreviousMigrationName>

# Create new migration
dotnet ef migrations add <MigrationName>

# Remove last migration (⚠️ if not applied)
dotnet ef migrations remove
```

## Docker Issues

### Container Problems

| Symptom | Check | Fix |
|---------|-------|-----|
| **Container won't start** | Port already in use | Change port or kill conflicting process |
| **Container crashes** | Logs show error | Fix configuration or dependencies |
| **Network issues** | Containers can't communicate | Check Docker network config |
| **Volume issues** | Data not persisting | Check volume mounts |

**Commands**:
```bash
# View all containers
docker compose ps

# View logs
docker compose logs -f <service>

# Restart service
docker compose restart <service>

# Rebuild service
docker compose up -d --build <service>

# Reset everything (⚠️ DATA LOSS)
docker compose down -v
docker compose up -d
```

---

### PowerShell on Windows

| Issue | Cause | Fix |
|-------|-------|-----|
| **Pipe failures** | Bash pipes don't work | Use `pwsh -c "docker logs ... \| grep pattern"` |
| **Script errors** | Escaping issues | Use `-File script.ps1` instead of `-c` for complex logic |
| **Permission denied** | Execution policy | `Set-ExecutionPolicy RemoteSigned -Scope CurrentUser` |

**Example**:
```powershell
# ✅ CORRECT - PowerShell wrapper
pwsh -c "docker logs meepleai-api --tail=50 | Select-String 'ERROR'"

# ❌ WRONG - Direct bash pipe
docker logs meepleai-api --tail=50 | grep ERROR
```

## Test Issues

### Test Failures

| Symptom | Cause | Fix |
|---------|-------|-----|
| **Testhost blocking** | Previous test still running | `taskkill /IM testhost.exe /F` |
| **Flaky tests** | Non-deterministic data | Use fixed dates, not `DateTime.Now` or `Math.random()` |
| **Integration test fails** | Docker not running | `docker compose up -d postgres redis` |
| **Coverage too low** | Missing scenarios | Add edge case tests |

**Commands**:
```bash
# Kill blocking testhost
taskkill /IM testhost.exe /F

# Run specific test
dotnet test --filter "FullyQualifiedName~GameSessionTests"

# Run with diagnostics
dotnet test --logger "console;verbosity=detailed"

# Check coverage
dotnet test /p:CollectCoverage=true
```

---

### E2E Test Failures

| Symptom | Cause | Fix |
|---------|-------|-----|
| **Timeout** | Element not found | Increase timeout or fix selector |
| **Selector not found** | DOM structure changed | Update selector to use ARIA roles |
| **Network error** | API mocking issue | Check MSW handlers |

**Playwright Commands**:
```bash
# Run tests
pnpm test:e2e

# Debug mode (headed browser)
pnpm test:e2e --debug

# Update snapshots
pnpm test:e2e --update-snapshots

# Specific test file
pnpm test:e2e tests/e2e/sessions.spec.ts
```

## Authentication Issues

### Token Problems

| Symptom | Cause | Fix |
|---------|-------|-----|
| **401 Unauthorized** | Missing/invalid token | Get fresh token via `/auth/login` |
| **403 Forbidden** | Insufficient permissions | Check user roles in token claims |
| **Token expired** | TTL exceeded | Refresh token or re-login |

**Debug Steps**:
```bash
# 1. Get token
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@meepleai.com","password":"<from admin.secret>"}'

# 2. Decode token (jwt.io or)
echo "<token>" | base64 -d

# 3. Verify token claims
# Check: sub (user ID), role, exp (expiry)

# 4. Test endpoint with token
curl http://localhost:8080/api/v1/sessions \
  -H "Authorization: Bearer <token>"
```

## Cache Issues

### Stale Data

| Symptom | Cause | Fix |
|---------|-------|-----|
| **Old data shown** | Cache not invalidated | Implement cache invalidation on updates |
| **No cache hits** | Cache key mismatch | Verify cache key consistency |
| **Memory leak** | No expiration set | Add TTL to cache entries |

**Cache Invalidation Pattern**:
```csharp
// After update/delete
await _cache.RemoveAsync($"sessions-{userId}", cancellationToken);

// Or invalidate related caches
await _cache.RemoveByTagAsync("sessions", cancellationToken);
```

## Deployment Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| **Environment mismatch** | Wrong .env file | Check environment-specific config |
| **Secret missing** | Not copied to prod | Verify all `.secret` files exist |
| **Migration not applied** | Manual step missed | Run `dotnet ef database update` |
| **CORS errors** | Origin not allowed | Update CORS policy |

## Diagnostic Commands Reference

```bash
# Backend Health
dotnet --version                        # Check .NET version
dotnet build                           # Test compilation
dotnet test --no-build                 # Run tests
docker compose ps                      # Check services

# Frontend Health
pnpm --version                         # Check pnpm version
pnpm typecheck                         # Check TypeScript
pnpm lint                              # Check linting
pnpm build                             # Test production build

# Database Health
docker exec -it meepleai-postgres psql -U meepleai -d meepleai_dev
# Then: \dt (list tables), \d <table> (describe table)

# Redis Health
docker exec -it meepleai-redis redis-cli
# Then: PING, KEYS *, GET <key>

# Logs
docker logs meepleai-api --tail=100 -f
docker logs meepleai-postgres --tail=50
docker logs meepleai-redis --tail=50
```

## Related Documentation

- **Architecture**: [Link to architecture template]
- **API Reference**: [Link to API template]
- **Development Guide**: [Link to development template]
- **Testing Guide**: [Link to testing template]
