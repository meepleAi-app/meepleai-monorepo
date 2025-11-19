# ADR-013: NSwag TypeScript Client and Zod Schema Generation

**Status**: Proposed
**Date**: 2025-01-19
**Deciders**: Engineering Lead, Frontend Team, Backend Team
**Context**: Code Review - Backend-Frontend Interactions Type Safety

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
│  http://localhost:5080/swagger/v1/swagger.json              │
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
      "url": "http://localhost:5080/swagger/v1/swagger.json",
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
  "input": "http://localhost:5080/swagger/v1/swagger.json",
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

**Decision Maker**: Engineering Lead
**Approval**: Pending Frontend & Backend Team Review
**Implementation**: Issue #TBD (Sprint 3)
