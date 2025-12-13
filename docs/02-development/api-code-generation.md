# API Code Generation Guide

**Status**: ✅ Implemented (Issue #1450)
**Related**: [ADR-013: NSwag TypeScript Generation](../01-architecture/adr/adr-013-nswag-typescript-generation.md)

---

## Overview

MeepleAI uses **automated code generation** to maintain type safety between the C# backend and TypeScript frontend. This eliminates manual type synchronization and prevents type drift.

**Flow**:
```
C# DTOs (Backend) → OpenAPI Spec → TypeScript Types + Zod Schemas (Frontend)
```

---

## Quick Start

### Generate API Client

```bash
# From the web directory
cd apps/web
pnpm generate:api
```

This will:
1. Fetch the OpenAPI specification from the running API (or use cached `openapi.json`)
2. Generate TypeScript types and API client (`api-client.ts`)
3. Generate Zod validation schemas (`zod-schemas.ts`)
4. Save all files to `apps/web/src/lib/api/generated/`

### Watch Mode (Auto-regenerate)

```bash
pnpm generate:api:watch
```

---

## Architecture

### Components

1. **Backend (NSwag.MSBuild)**:
   - Package: `NSwag.MSBuild` v14.2.0
   - Config: `apps/api/src/Api/nswag.json`
   - Trigger: Runs on `dotnet build` (Debug mode or `NSwagExecution=true`)
   - Output: `openapi.json`, `api-client.ts`

2. **Frontend (openapi-zod-client)**:
   - Package: `openapi-zod-client` v1.18.3
   - Script: `apps/web/scripts/generate-api-client.ts`
   - Input: OpenAPI spec from API or local file
   - Output: `zod-schemas.ts`

3. **CI/CD Validation**:
   - Workflow: `.github/workflows/ci.yml` → `validate-api-codegen` job
   - Ensures generated files are committed and up-to-date
   - Fails build if manual generation needed

---

## Generated Files

All files are in `apps/web/src/lib/api/generated/`:

```
apps/web/src/lib/api/generated/
├── .gitignore          # Excludes generated files from git
├── README.md           # Instructions (auto-generated)
├── openapi.json        # OpenAPI specification (cached)
├── api-client.ts       # TypeScript API client (NSwag)
└── zod-schemas.ts      # Zod validation schemas (openapi-zod-client)
```

**⚠️ DO NOT EDIT**: These files are auto-generated and will be overwritten.

---

## Usage Examples

### 1. Import Generated Types

```typescript
// Before (manual types - prone to drift)
import { UserProfile } from '@/lib/api/types/user';

// After (generated types - always in sync)
import { UserProfile } from '@/lib/api/generated/api-client';
```

### 2. Validate API Responses with Zod

```typescript
import { GameSchema } from '@/lib/api/generated/zod-schemas';

// Validate API response
const response = await fetch('/api/v1/games');
const data = await response.json();
const games = GameSchema.parse(data); // Runtime validation
```

### 3. Use Generated API Client

```typescript
import { MeepleAiClient } from '@/lib/api/generated/api-client';

const client = new MeepleAiClient('http://localhost:8080');
const profile = await client.getUserProfile(); // Fully typed!
```

---

## Development Workflow

### When to Regenerate

Run `pnpm generate:api` when:
- ✅ Backend DTOs change (fields added/removed/renamed)
- ✅ New API endpoints added
- ✅ API contracts modified
- ✅ After pulling changes that modify the API

**CI will fail** if you forget to regenerate and commit the updated files.

### Before Committing

```bash
# 1. Make backend changes (e.g., add field to DTO)
cd apps/api/src/Api
# Edit UserProfile.cs

# 2. Build backend (generates openapi.json)
dotnet build

# 3. Generate frontend types
cd ../../../web
pnpm generate:api

# 4. Commit all changes (including generated files)
git add .
git commit -m "feat: add role field to UserProfile"
```

---

## CI/CD Integration

### Validation Job

The `validate-api-codegen` job in CI:

1. **Builds API**: Generates `openapi.json` via NSwag
2. **Generates Types**: Runs `pnpm generate:api`
3. **Checks Diff**: Compares generated files with committed versions
4. **Fails Build**: If generated files differ, build fails with diff output

### Example CI Failure

```
❌ Generated API client files are out of date!

The following files have uncommitted changes:
  apps/web/src/lib/api/generated/api-client.ts
  apps/web/src/lib/api/generated/zod-schemas.ts

Please run 'pnpm generate:api' locally and commit the changes.
```

---

## Configuration

### NSwag Configuration (`apps/api/src/Api/nswag.json`)

Key settings:
- **Runtime**: `Net90` (ASP.NET 9)
- **TypeScript Version**: `5.0`
- **Template**: `Fetch` (native fetch API)
- **Output**: `../../../web/src/lib/api/generated/api-client.ts`

### Generation Script (`apps/web/scripts/generate-api-client.ts`)

Environment variables:
- `OPENAPI_URL`: API endpoint (default: `http://localhost:8080/swagger/v1/swagger.json`)
- `OPENAPI_FILE`: Local fallback (default: `../../api/src/Api/openapi.json`)

---

## Troubleshooting

### Error: "Could not fetch OpenAPI spec"

**Problem**: API is not running or OpenAPI spec not found

**Solution**:
```bash
# Build backend to generate openapi.json
cd apps/api
dotnet build

# Or start the API
dotnet run

# Then regenerate
cd ../web
pnpm generate:api
```

### Error: "Generated files out of date" in CI

**Problem**: Forgot to regenerate and commit updated files

**Solution**:
```bash
# Regenerate locally
pnpm generate:api

# Commit changes
git add apps/web/src/lib/api/generated/
git commit --amend --no-edit
git push --force-with-lease
```

### TypeScript Errors After Regeneration

**Problem**: Breaking changes in API contracts

**Solution**:
1. Review TypeScript errors (fields added/removed/renamed)
2. Update frontend code to match new types
3. This is intentional! Catching breaking changes early

---

## Migration from Manual Types

### Gradual Migration

You can migrate incrementally:

1. **Keep existing manual types** (they still work)
2. **Add `@deprecated` JSDoc** to manual types:
   ```typescript
   /**
    * @deprecated Use generated type from @/lib/api/generated/api-client
    */
   export interface UserProfile { ... }
   ```
3. **Migrate one client at a time**
4. **Remove manual types** when all usages migrated

### Example Migration

```typescript
// Before
import { UserProfile } from '@/lib/api/types/user';
import { UserProfileSchema } from '@/lib/api/schemas/auth.schemas';

// After
import { UserProfile } from '@/lib/api/generated/api-client';
import { UserProfileSchema } from '@/lib/api/generated/zod-schemas';
```

---

## Benefits

✅ **Type Safety**: Compile-time errors for API changes
✅ **Zero Drift**: Backend changes → frontend types automatically
✅ **Faster Development**: No manual type maintenance
✅ **Self-Documenting**: Types show API structure
✅ **CI Validation**: Build fails if types out of sync
✅ **Refactoring Safety**: Rename DTO → frontend catches all usages

---

## Related Documentation

- [ADR-013: NSwag TypeScript Generation](../01-architecture/adr/adr-013-nswag-typescript-generation.md)
- [NSwag Documentation](https://github.com/RicoSuter/NSwag/wiki)
- [openapi-zod-client Documentation](https://github.com/astahmer/openapi-zod-client)
- [Zod Documentation](https://zod.dev/)

---

**Last Updated**: 2025-12-13T10:59:23.970Z
**Implemented In**: Issue #1450
**Status**: ✅ Fully Operational

