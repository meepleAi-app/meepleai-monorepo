# Plan: Epic #3327 Stage 4 - Feature Flags Verification

> Verification and documentation plan for Issue #3674

## Hypothesis

**Goal**: Verify tier-based feature flag implementation and complete integration

**Current State Analysis**:
- ✅ FeatureFlagService exists with tier support (Issue #3073)
- ✅ CanAccessFeatureAsync(user, feature) combines role + tier
- ✅ Admin endpoints for enable/disable per tier
- ❌ No user endpoint to query available features
- ❌ No feature matrix documentation
- ❌ No [RequireFeature] middleware/attribute
- ❌ No integration tests

**Approach**: Complete missing integration points + documentation

## Expected Outcomes

| Component | Target | Priority |
|-----------|--------|----------|
| **User Features Endpoint** | GET `/users/me/features` | 🔴 Critical |
| **Feature Matrix Doc** | `docs/feature-flags-tier-matrix.md` | 🔴 Critical |
| **Integration Tests** | Tier-based access verification | 🟡 Important |
| **[RequireFeature] Middleware** | Attribute-based enforcement | 🟢 Optional |
| **Seed Data** | Default feature configuration | 🟡 Important |

## Implementation Plan

### Phase 1: User Features Endpoint (30 min)
**Files to Create**:
```
BoundedContexts/SystemConfiguration/Application/
  Queries/GetUserAvailableFeaturesQuery.cs
  Handlers/GetUserAvailableFeaturesQueryHandler.cs
  DTOs/UserFeatureDto.cs
Routing/UserProfileEndpoints.cs (modify)
```

**Logic**:
```csharp
1. Get all feature flags
2. For each feature, call CanAccessFeatureAsync(currentUser, feature)
3. Return list of available features with access status
```

### Phase 2: Feature Matrix Documentation (45 min)
**File**: `docs/features/feature-flags-tier-matrix.md`

**Content**:
```markdown
| Feature Key | Description | Free | Normal | Premium | Admin |
|-------------|-------------|------|--------|---------|-------|
| basic_chat | Basic Q&A | ✅ | ✅ | ✅ | ✅ |
| advanced_rag | RAG search | ❌ | ✅ | ✅ | ✅ |
| multi_agent | Multi-agent | ❌ | ❌ | ✅ | ✅ |
| pdf_ocr | PDF OCR | ❌ | ✅ | ✅ | ✅ |
| export_data | Export | ❌ | ✅ | ✅ | ✅ |
```

**Research**: Identify all existing feature flags in system

### Phase 3: Integration Tests (30 min)
**Test File**: `Api.Tests/Integration/FeatureFlagTierAccessTests.cs`

**Test Cases**:
1. Free user cannot access premium features
2. Premium user can access all features
3. Admin bypasses all restrictions
4. Feature flag changes reflected immediately
5. Tier upgrade grants new features

### Phase 4: Seed Data (15 min)
**File**: Create or update existing seed migration

**Default Configuration**:
```csharp
// Enable for all tiers by default (backward compatibility)
basic_chat: all tiers ✅
basic_qa: all tiers ✅

// Tier-restricted features
advanced_rag: normal+, premium+
multi_agent: premium only
pdf_ocr: normal+, premium+
```

## Risks & Mitigation

| Risk | Mitigation |
|------|------------|
| **Unknown features exist** | Grep all feature flag usages in codebase |
| **Missing tier configurations** | Document current defaults, don't change behavior |
| **Test complexity** | Focus on critical paths (free → premium access) |
| **Documentation staleness** | Link to code as source of truth |

## Success Criteria

**Critical (Must Have)**:
- ✅ GET `/users/me/features` endpoint working
- ✅ Feature matrix documented
- ✅ At least 5 integration tests

**Important (Should Have)**:
- ✅ Seed data for defaults
- ✅ All existing features documented

**Optional (Nice to Have)**:
- [RequireFeature] middleware attribute
- Feature flag usage examples in docs

## Time Allocation

- User endpoint: 30 min
- Documentation: 45 min
- Integration tests: 30 min
- Seed data: 15 min
- **Total**: ~2 hours (vs 2-3h estimate)

---

**Plan Created**: 2026-02-06 16:10
**Next**: Execute Phase 1 (User Features Endpoint)
