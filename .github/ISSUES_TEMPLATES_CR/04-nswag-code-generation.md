# 🔧 [DX] NSwag TypeScript Code Generation from OpenAPI

**Priority**: 🟡 HIGH
**Complexity**: Medium
**Estimated Time**: 8-10 hours
**Dependencies**: Swagger/OpenAPI (✅ already configured)

## 🎯 Objective

Automate TypeScript client and Zod schema generation from OpenAPI specification to eliminate manual type sync errors between backend and frontend.

## 📋 Context

**Source**: Code Review Backend-Frontend Interactions
**Issue**: Manual Zod schemas in `apps/web/src/lib/api/schemas/` can diverge from C# DTOs
**Impact**: High - Eliminates type mismatch errors, improves developer experience
**Current State**: Manual TypeScript types + manual Zod schemas

## 🔧 Proposed Solution

Use NSwag to generate:
1. **TypeScript interfaces** from C# DTOs
2. **Zod schemas** for runtime validation
3. **API client methods** with proper typing
4. **Automatic regeneration** in CI/CD pipeline

## ✅ Task Checklist

### Setup
- [ ] Install `NSwag.MSBuild` in `apps/api/src/Api/Api.csproj`
- [ ] Install `@hey-api/openapi-ts` or similar npm package for Zod generation
- [ ] Create `apps/api/nswag.json` configuration file
- [ ] Configure output directory: `apps/web/src/lib/api/generated/`

### Configuration
- [ ] Configure NSwag for TypeScript + Zod output
- [ ] Setup custom templates (if needed)
- [ ] Configure naming conventions (camelCase for TS, PascalCase for C#)
- [ ] Setup source maps for debugging
- [ ] Configure API client with HttpClient integration

### NPM Scripts
- [ ] Add `pnpm generate:api-client` script in `apps/web/package.json`
- [ ] Add pre-build hook to ensure API is running
- [ ] Add validation script to check generated files are up-to-date

### CI/CD Integration
- [ ] Add generation step to `.github/workflows/ci.yml`
- [ ] Fail build if generated files are out of date
- [ ] Auto-commit generated files (or fail with diff)
- [ ] Setup cache for OpenAPI spec

### Migration
- [ ] Create migration plan for existing manual schemas
- [ ] Gradual migration: keep both manual and generated (deprecated manual)
- [ ] Update imports to use generated schemas
- [ ] Remove deprecated manual schemas (after validation)

### Testing
- [ ] Test generated client matches manual client behavior
- [ ] Integration tests for all endpoints
- [ ] Verify Zod validation works correctly
- [ ] Test error handling with generated types
- [ ] Verify SSE streaming endpoints work

### Documentation
- [ ] Create `docs/02-development/api-client-generation.md`
- [ ] Document regeneration workflow
- [ ] Add troubleshooting guide
- [ ] Update contributor guide with generation steps
- [ ] Document customization options

## 📁 Files to Create/Modify

```
apps/api/nswag.json (NEW)
apps/api/src/Api/Api.csproj (MODIFY - add NSwag.MSBuild)
apps/web/package.json (MODIFY - add scripts)
apps/web/src/lib/api/generated/ (NEW - auto-generated directory)
├── schemas/ (Zod schemas)
├── types/ (TypeScript interfaces)
└── client/ (API client methods)
.github/workflows/ci.yml (MODIFY - add generation step)
docs/02-development/api-client-generation.md (NEW)
.gitignore (MODIFY - ignore temp generation files)
```

## 🔗 References

- [NSwag Documentation](https://github.com/RicoSuter/NSwag)
- [NSwag MSBuild](https://github.com/RicoSuter/NSwag/wiki/NSwag.MSBuild)
- [@hey-api/openapi-ts](https://github.com/hey-api/openapi-ts)
- [Zod](https://zod.dev/)
- [OpenAPI 3.0 Specification](https://swagger.io/specification/)

## 📊 Acceptance Criteria

- ✅ TypeScript types auto-generated from C# DTOs
- ✅ Zod schemas auto-generated from OpenAPI spec
- ✅ API client methods generated with proper typing
- ✅ CI/CD pipeline regenerates on backend changes
- ✅ Build fails if generated files out of date
- ✅ All existing functionality migrated to generated client
- ✅ Documentation complete
- ✅ Zero manual type sync needed

## ⚠️ Migration Strategy

### Phase 1: Setup & Generation (Week 1)
- Configure NSwag
- Generate initial files
- Validate output

### Phase 2: Parallel Mode (Week 2-3)
- Keep manual schemas (deprecated)
- Migrate endpoints one by one
- Run both in parallel with tests

### Phase 3: Full Migration (Week 4)
- Remove manual schemas
- Update all imports
- Remove deprecated code

### Phase 4: Optimization (Week 5)
- Fine-tune generation
- Optimize bundle size
- Performance testing

## 🏷️ Labels

`priority: high`, `type: enhancement`, `area: frontend`, `area: backend`, `effort: large`, `sprint: 3`
