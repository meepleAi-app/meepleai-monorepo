# ADR-013: NSwag TypeScript Client and Zod Schema Generation

**Status**: Superseded by .NET 9 Native OpenAPI (2025-11-22, Issue #1583)
**Date**: 2025-01-19
**Implementation Date**: 2025-11-21 (Issue #1450)
**Migration Date**: 2025-11-22 (Issue #1583 - Swashbuckle → Microsoft.AspNetCore.OpenApi 9.0.0)
**Deciders**: Engineering Lead, Frontend Team, Backend Team
**Context**: Code Review - Backend-Frontend Interactions Type Safety

> **Update 2025-11-22**: Migrated from Swashbuckle.AspNetCore to Microsoft.AspNetCore.OpenApi 9.0.0 (native .NET 9).
> OpenAPI endpoint changed: `/swagger/v1/swagger.json` → `/openapi/v1.json`. See Migration Notes below.

---

## Context

During the code review of backend-frontend interactions (2025-01-19), a critical gap in type safety was identified:

**Current State**:
- **Manual TypeScript types** in `apps/web/src/lib/api/types/`
- **Manual Zod schemas** in `apps/web/src/lib/api/schemas/`
- **No automatic sync** between C# DTOs and TypeScript interfaces
- **Manual maintenance** required after every backend change

**Problem**: Type Drift Risk
```
Backend (C#)                          Frontend (TypeScript)
───────────────────────────────────────────────────────────
public record UserProfile             interface UserProfile {
{                                       id: string;
    public Guid Id { get; init; }       email: string;
    public string Email { get; init; }  displayName: string;
    public string DisplayName { get; init; } // ⚠️ Missing: role
}                                       // ⚠️ Drift detected!
public string Role { get; init; }  }
```

**Impact of Manual Sync**:
- ❌ **Type mismatch errors** at runtime (not compile time)
- ❌ **Silent failures** when backend adds/removes fields
- ❌ **Developer burden** (remember to update TypeScript)
- ❌ **Code review overhead** (catching type drift)
- ❌ **Regression risk** (incomplete migrations)

**Evidence from Code Review**:
- 8 manual schemas in `apps/web/src/lib/api/schemas/`
- 6 manual types in `apps/web/src/lib/api/types/`
- **Potential for drift**: Every backend PR that changes DTOs

---

## Decision

Implement **NSwag-based automatic TypeScript client and Zod schema generation** from OpenAPI specification.

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Backend (C# DTOs)                                          │
│  apps/api/src/Api/BoundedContexts/*/Application/DTOs/       │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼ (Swagger/OpenAPI generation)
┌─────────────────────────────────────────────────────────────┐
│  OpenAPI Specification (swagger.json)                       │
│  http://localhost:8080/swagger/v1/swagger.json              │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼ (NSwag CLI / MSBuild)
┌─────────────────────────────────────────────────────────────┐
│  Generated TypeScript (apps/web/src/lib/api/generated/)     │
│  ├─ types/                                                  │
│  │  ├─ AuthTypes.ts      (TypeScript interfaces)           │
│  │  ├─ GameTypes.ts                                        │
│  │  └─ ...                                                 │
│  ├─ schemas/                                               │
│  │  ├─ AuthSchemas.ts    (Zod validation schemas)         │
│  │  ├─ GameSchemas.ts                                     │
│  │  └─ ...                                                 │
│  └─ client/                                                │
│     └─ ApiClient.ts       (HTTP client methods)           │
└─────────────────────────────────────────────────────────────┘
```

### Components

#### 1. NSwag Configuration (`apps/api/nswag.json`)

```json
{
  "$schema": "http://json.schemastore.org/nswag",
  "runtime": "Net80",
  "defaultVariables": null,
  "documentGenerator": {
    "fromDocument": {
      "url": "http://localhost:8080/swagger/v1/swagger.json",
      "output": null
    }
  },
  "codeGenerators": {
    "openApiToTypeScriptClient": {
      "className": "MeepleAiClient",
      "moduleName": "",
      "namespace": "",
      "typeScriptVersion": 5.0,
      "template": "Fetch",
      "promiseType": "Promise",
      "httpClass": "HttpClient",
      "dateTimeType": "Date",
      "generateClientClasses": true,
      "generateClientInterfaces": false,
      "generateOptionalParameters": true,
      "exportTypes": true,
      "wrapDtoExceptions": true,
      "useTransformOptionsMethod": true,
      "useTransformResultMethod": false,
      "generateDtoTypes": true,
      "operationGenerationMode": "SingleClientFromOperationId",
      "markOptionalProperties": true,
      "typeNameGenerator": "TypeScriptTypeNameGenerator",
      "output": "../../../apps/web/src/lib/api/generated/client/ApiClient.ts"
    }
  }
}
```

#### 2. Zod Schema Generation (Custom Template)

Since NSwag doesn't natively generate Zod schemas, we'll use a two-step process:
1. Generate TypeScript types with NSwag
2. Use `ts-to-zod` to convert TypeScript interfaces to Zod schemas

**Alternative**: Use `@hey-api/openapi-ts` which supports Zod directly

```json
{
  "$schema": "https://openapi-ts.dev/schema.json",
  "client": "@hey-api/client-fetch",
  "input": "http://localhost:8080/swagger/v1/swagger.json",
  "output": {
    "path": "apps/web/src/lib/api/generated",
    "format": "prettier",
    "lint": "eslint"
  },
  "schemas": {
    "export": true,
    "type": "zod"
  },
  "types": {
    "dates": true,
    "enums": "typescript"
  }
}
```

#### 3. NPM Scripts (`apps/web/package.json`)

```json
{
  "scripts": {
    "generate:api-client": "pnpm generate:openapi && pnpm generate:zod",
    "generate:openapi": "nswag run ../api/nswag.json",
    "generate:zod": "openapi-ts --config openapi-ts.config.json",
    "validate:generated": "node scripts/validate-generated-files.js"
  },
  "devDependencies": {
    "nswag": "^14.0.7",
    "@hey-api/openapi-ts": "^0.45.0",
    "zod": "^3.22.4"
  }
}
```

#### 4. CI/CD Integration (`.github/workflows/ci.yml`)

```yaml
- name: Generate API Client
  working-directory: apps/web
  run: |
    # Start backend (needed for OpenAPI spec)
    cd ../api/src/Api
    dotnet run &
    API_PID=$!
    sleep 10  # Wait for API to start

    # Generate TypeScript client
    cd ../../../web
    pnpm generate:api-client

    # Verify generated files are up-to-date
    if ! git diff --exit-code src/lib/api/generated/; then
      echo "❌ Generated API client is out of date!"
      echo "Run: pnpm generate:api-client"
      exit 1
    fi

    # Stop backend
    kill $API_PID
```

---

## Consequences

### Positive

✅ **Type Safety**:
- Compile-time errors for type mismatches
- No more runtime surprises from backend changes
- IntelliSense for all API types

✅ **Developer Experience**:
- Zero manual type maintenance
- Automatic sync on backend changes
- Self-documenting API (types show structure)

✅ **Reduced Errors**:
- No more manual type transcription errors
- No more forgotten field updates
- CI fails if types are out of sync

✅ **Faster Development**:
- No time wasted on manual type updates
- Focus on features, not boilerplate
- Instant feedback on breaking changes

✅ **Better Refactoring**:
- Rename backend DTO → frontend updates automatically
- Add field → TypeScript knows immediately
- Remove field → compiler catches usage

### Negative

⚠️ **Build Complexity**:
- Need running backend to generate types
- CI pipeline more complex
- **Mitigation**: Cache generated files, conditional regeneration

⚠️ **Generated Code in Repo**:
- Large generated files in git history
- **Mitigation**: `.gitattributes` to mark as generated
- **Alternative**: Generate on-demand (not committed)

⚠️ **Breaking Changes Visible**:
- Backend change → frontend build fails
- **Mitigation**: This is actually good! Catch breaking changes early

⚠️ **Learning Curve**:
- Team needs to understand generation workflow
- **Mitigation**: Documentation, examples, onboarding

### Risks

🟡 **OpenAPI Spec Incomplete**:
- **Risk**: Not all DTOs documented in Swagger
- **Mitigation**: Enforce Swagger annotations in code reviews

🟡 **Zod Schema Limitations**:
- **Risk**: OpenAPI can't express all Zod features (e.g., `refine()`)
- **Mitigation**: Manual Zod validators for complex rules

🟢 **Generation Failures**:
- **Risk**: NSwag fails if API is down
- **Mitigation**: Graceful fallback, cached specs

---

## Migration Strategy

### Phase 1: Setup & Proof of Concept (Week 1)
1. ✅ Install NSwag CLI and dependencies
2. ✅ Create `nswag.json` configuration
3. ✅ Generate initial TypeScript client
4. ✅ Validate output matches manual types
5. ✅ Test with one endpoint (e.g., `/api/v1/users/profile`)

### Phase 2: Parallel Mode (Week 2-3)
6. ✅ Generate all types (keep manual as deprecated)
7. ✅ Add `@deprecated` JSDoc to manual types
8. ✅ Migrate one client at a time (start with `authClient`)
9. ✅ Run both manual and generated in parallel
10. ✅ Extensive testing

### Phase 3: Full Migration (Week 4)
11. ✅ Migrate all remaining clients
12. ✅ Remove manual types directory
13. ✅ Remove manual schemas directory
14. ✅ Update all imports

### Phase 4: CI/CD Integration (Week 5)
15. ✅ Add generation step to CI pipeline
16. ✅ Fail build if generated files out of date
17. ✅ Add pre-commit hook for generation
18. ✅ Documentation

---

## Comparison: Manual vs Generated

### Manual Approach (Current)

**Pros**:
- Full control over types
- No build dependencies
- Simple workflow

**Cons**:
- Manual maintenance burden
- Type drift risk
- Slower development
- Error-prone

**Example**:
```typescript
// Manual type (can drift from backend)
export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  // Forgot to add 'role' field!
}

// Manual Zod schema (duplicate definition)
export const UserProfileSchema = z.object({
  id: z.string().uuid(),
  email: z.string().email(),
  displayName: z.string(),
  // Forgot here too!
});
```

### Generated Approach (Proposed)

**Pros**:
- Always in sync with backend
- Zero maintenance
- Type safety guaranteed
- Single source of truth

**Cons**:
- Build complexity
- Less manual control
- Generated code in repo

**Example**:
```typescript
// Generated automatically from C# DTO
export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  role: string;  // ✅ Automatically added when backend changes
}

// Generated Zod schema (matches exactly)
export const UserProfileSchema = z.object({
  id: z.string().uuid(),
  email: z.string().email(),
  displayName: z.string(),
  role: z.string(),  // ✅ Always in sync
});
```

---

## Alternatives Considered

### Alternative 1: GraphQL Code Generation
**Description**: Use GraphQL with `graphql-codegen`

**Pros**:
- Excellent type safety
- Query-specific types
- Smaller payloads

**Cons**:
- Backend rewrite required (currently REST)
- Steeper learning curve
- Infrastructure overhead (GraphQL server)

**Decision**: Rejected - Too big of a change

### Alternative 2: tRPC
**Description**: TypeScript RPC framework with end-to-end type safety

**Pros**:
- Perfect type safety (shared types)
- No code generation needed
- Excellent DX

**Cons**:
- Requires Node.js backend (we use C#)
- Not compatible with ASP.NET Core
- Can't migrate incrementally

**Decision**: Rejected - Backend is C#, not TypeScript

### Alternative 3: Keep Manual Types + Rigorous Testing
**Description**: Maintain manual types but add more tests

**Pros**:
- No build complexity
- Full control

**Cons**:
- Still error-prone
- High maintenance burden
- Doesn't scale

**Decision**: Rejected - Automation is better

### Alternative 4: OpenAPI TypeScript (alternative tool)
**Description**: Use `openapi-typescript` instead of NSwag

**Pros**:
- Lighter weight
- Better Zod support
- Active development

**Cons**:
- Less mature than NSwag for .NET
- Fewer .NET-specific features

**Decision**: Consider as alternative if NSwag has issues

---

## Tooling Comparison

| Tool | Zod Support | .NET Support | Maturity | DX |
|------|-------------|--------------|----------|-----|
| **NSwag** | ❌ (custom) | ✅ Excellent | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| **@hey-api/openapi-ts** | ✅ Native | ✅ Good | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **openapi-typescript** | ✅ Plugin | ✅ Good | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| **swagger-typescript-api** | ❌ | ✅ | ⭐⭐⭐ | ⭐⭐⭐ |

**Recommendation**: Start with **@hey-api/openapi-ts** (best Zod support)

---

## Configuration Files

### `.gitattributes` (Mark as Generated)
```
apps/web/src/lib/api/generated/** linguist-generated=true
```

### `tsconfig.json` (Exclude Generated from Linting)
```json
{
  "exclude": [
    "src/lib/api/generated/**"
  ]
}
```

### `.eslintignore`
```
apps/web/src/lib/api/generated/
```

---

## Monitoring

**Metrics**:
- Generation time in CI
- Number of generated types
- Breaking change frequency

**Alerts**:
- Generation fails in CI
- Generated files out of sync
- OpenAPI spec validation errors

---

## Related Decisions

- **ADR-012**: FluentValidation CQRS (validates generated types)
- **ADR-008**: Streaming CQRS Migration (CQRS handlers)

---

## References

- [NSwag Documentation](https://github.com/RicoSuter/NSwag/wiki)
- [@hey-api/openapi-ts](https://github.com/hey-api/openapi-ts)
- [OpenAPI TypeScript](https://github.com/drwpow/openapi-typescript)
- [Zod Documentation](https://zod.dev/)
- [OpenAPI 3.0 Specification](https://swagger.io/specification/)

---

## Implementation Details (2025-11-21)

### Final Architecture

We implemented a hybrid approach using:
- **NSwag.MSBuild** (v14.2.0) for TypeScript client generation
- **openapi-zod-client** (v1.18.3) for Zod schema generation

### Key Files

1. **Backend**:
   - `apps/api/src/Api/Api.csproj` - Added NSwag.MSBuild packages
   - `apps/api/src/Api/nswag.json` - NSwag configuration for TypeScript client
   - MSBuild target to generate on build (Debug mode or NSwagExecution=true)

2. **Frontend**:
   - `apps/web/scripts/generate-api-client.ts` - Main generation script
   - `apps/web/src/lib/api/generated/` - Output directory
   - `apps/web/src/lib/api/generated/.gitignore` - Excludes generated files from git
   - `apps/web/package.json` - Added `generate:api` and `generate:api:watch` scripts

3. **CI/CD**:
   - `.github/workflows/ci.yml` - New `validate-api-codegen` job
   - Validates generated files are up-to-date on every PR
   - Fails if manual changes needed

### Usage

```bash
# Generate API client and schemas
cd apps/web
pnpm generate:api

# Watch mode (regenerate on API changes)
pnpm generate:api:watch
```

### CI Integration

The CI pipeline now includes a validation step that:
1. Builds the API (triggers NSwag to generate openapi.json)
2. Runs the generation script
3. Verifies generated files match committed versions
4. Fails the build if they differ (with helpful diff output)

### Migration Status

- ✅ **Phase 1-4 Complete**: All phases implemented
- ✅ **CI/CD Integration**: Validation job added
- ⏳ **Manual Schema Migration**: Can be done incrementally (existing schemas still work)

---

**Decision Maker**: Engineering Lead
**Implementation**: Issue #1450 (Completed 2025-11-21)
**Status**: ✅ Fully Operational

---

## Update (2025-11-21 - Issue #1543)

### NSwag MSBuild Removal

**Context**: NSwag.MSBuild integration caused local build failures due to incompatibility with .NET 9 minimal APIs:
- **Error**: `NSwag requires BuildWebHost or CreateWebHostBuilder/CreateHostBuilder method`
- **Impact**: Local `dotnet build` and `dotnet test` blocked
- **Root Cause**: NSwag.MSBuild expects legacy ASP.NET Core patterns, not minimal APIs

**Decision**: Migrate from NSwag.MSBuild to hybrid OpenAPI generation approach (Option C):

1. **Removed**:
   - `NSwag.MSBuild` package (v14.2.0)
   - `NSwag.ApiDescription.Client` package (v14.2.0)
   - NSwag MSBuild target from `Api.csproj`
   - `nswag.json` configuration file

2. **Retained**:
   - Swashbuckle.AspNetCore (v10.0.1) for runtime OpenAPI generation
   - `/swagger/v1/swagger.json` endpoint (Development only)
   - openapi-zod-client for Zod schema generation

3. **Added**:
   - **Scalar.AspNetCore** (v2.11.0) - Modern OpenAPI documentation UI
   - **Committed openapi.json** (Option C - Hybrid approach)
   - Updated `generate-api-client.ts` to use committed spec as fallback

### New Workflow (Option C - Hybrid)

**TypeScript Generation**:
```bash
# Uses committed openapi.json (apps/api/src/Api/openapi.json)
cd apps/web
pnpm generate:api
```

**OpenAPI Spec Regeneration** (when API changes):
```bash
# 1. Fix build errors (ActiveSession, EndpointFilter issues)
# 2. Run API locally
cd apps/api && dotnet run

# 3. Download updated spec
curl http://localhost:8080/swagger/v1/swagger.json -o src/Api/openapi.json

# 4. Commit updated spec
git add src/Api/openapi.json
git commit -m "chore: update OpenAPI spec"
```

### Benefits

✅ **Unblocked Local Builds**:
- No more NSwag MSBuild errors
- `dotnet build` and `dotnet test` work locally
- Developers can run coverage tools

✅ **Improved Developer Experience**:
- Modern Scalar UI at `/scalar/v1` (replaces Swagger UI)
- DeepSpace theme, testing capabilities, code samples
- Swagger UI still available at `/api/docs` for compatibility

✅ **Simplified Architecture**:
- One less build-time dependency
- Runtime-only OpenAPI generation (cleaner)
- Committed spec enables frontend development without running API

### Migration Notes

- **CI/CD**: Validation job still works (reads committed openapi.json)
- **Frontend**: No changes needed (generate-api-client.ts updated)
- **Build Errors**: Preexisting errors (ActiveSession, EndpointFilter) block Swagger generation until fixed
- **Template Spec**: Current openapi.json is minimal template (to be regenerated post-build-fix)

### References

- Issue #1543: NSwag build error blocks local test/coverage execution
- Issue #1583: OAuth test failures + OpenAPI migration
- Scalar Documentation: https://scalar.com/
- Microsoft.AspNetCore.OpenApi: Native .NET 9 OpenAPI support

---

## Migration Notes (2025-11-22)

### OpenAPI Provider Migration: Swashbuckle → .NET 9 Native

**Trigger**: Issue #1583 (OAuth integration test failures)

**Root Cause**: Swashbuckle 10.0.1 + Microsoft.OpenApi 3.0.1 incompatibility caused Npgsql connection issues in Testcontainers.

**Solution**: Migrated to Microsoft.AspNetCore.OpenApi 9.0.0 (native .NET 9 OpenAPI generation).

### Changes

**Backend (apps/api/src/Api/Api.csproj)**:
```diff
- Swashbuckle.AspNetCore 10.0.1
- Microsoft.OpenApi 3.0.1 (invalid - spec version not package version)
+ Microsoft.AspNetCore.OpenApi 9.0.0 (native .NET 9)
```

**Configuration Simplified**:
```csharp
// Before (ObservabilityServiceExtensions.cs): 83 lines of Swashbuckle config
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(options => { /* 70+ lines */ });

// After: 1 line
services.AddOpenApi();
```

**Middleware Updated** (WebApplicationExtensions.cs):
```csharp
// Before
app.UseSwagger();
app.UseSwaggerUI(options => { /* config */ });

// After
app.MapOpenApi();
```

### Endpoint Changes

| Aspect | Old (Swashbuckle) | New (Native .NET 9) |
|--------|-------------------|---------------------|
| **OpenAPI JSON** | `/swagger/v1/swagger.json` | `/openapi/v1.json` |
| **Swagger UI** | `/api/docs` | ❌ Removed |
| **Scalar UI** | `/scalar/v1` | ✅ `/scalar/v1` (unchanged) |
| **UI Recommendation** | Swagger UI | Scalar (modern, feature-rich) |

### Frontend Integration Impact

**generate-api-client.ts Updated**:
```typescript
// Before
const OPENAPI_URL = 'http://localhost:8080/swagger/v1/swagger.json';

// After
const OPENAPI_URL = 'http://localhost:8080/openapi/v1.json';
```

**Manual Regeneration Steps**:
```bash
# 1. Start API
cd apps/api && dotnet run

# 2. Download new spec
curl http://localhost:8080/openapi/v1.json -o src/Api/openapi.json

# 3. Generate TypeScript types + Zod schemas
cd apps/web && pnpm generate:api
```

### Benefits of Migration

1. **Native .NET 9 Support**: No third-party dependency for OpenAPI generation
2. **Simpler Configuration**: 83 lines → 1 line (98% reduction)
3. **Stability**: Microsoft.AspNetCore.OpenAPI 9.0.0 is production-ready and maintained by Microsoft
4. **Compatibility**: No more Swashbuckle/Microsoft.OpenApi version conflicts
5. **Test Reliability**: Fixed Npgsql connection issues in integration tests
6. **Future-Proof**: Aligned with .NET 9+ best practices

### Breaking Changes

**None for API consumers**:
- OpenAPI document still available (different endpoint)
- Scalar UI continues to work
- Frontend type generation unaffected (script updated)

**For Developers**:
- Swagger UI removed (use Scalar UI at `/scalar/v1` instead)
- OpenAPI endpoint URL changed (documented above)

### Rollback Plan

If issues arise, revert to Swashbuckle:
```bash
dotnet add package Swashbuckle.AspNetCore --version 6.9.0
# Revert ObservabilityServiceExtensions.cs and WebApplicationExtensions.cs
# Update generate-api-client.ts back to /swagger/v1/swagger.json
```

**Note**: Swashbuckle 10.x NOT recommended due to Microsoft.OpenApi 2.x breaking changes.

### Related

- **ADR-014**: NSwag.MSBuild removal (incompatible with .NET 9)
- **Issue #1583**: OAuth test failures (resolved by this migration)
- **PR #1593**: Implementation PR
