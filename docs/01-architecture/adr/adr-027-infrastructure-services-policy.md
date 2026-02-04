# ADR-027: Infrastructure Services Policy

**Status**: Accepted
**Date**: 2025-12-07
**Deciders**: Engineering Team
**Related**: Issue #1680, DDD Migration

---

## Context

After the DDD migration (100% complete, 2,070 lines of legacy services removed), **39 infrastructure services** remain in `apps/api/src/Api/Services/`. We need clear guidelines for:

1. **When** to create infrastructure services vs domain/application services
2. **How** to classify existing services (keep, consolidate, migrate)
3. **Where** services should live (infrastructure layer vs bounded contexts)
4. **Why** some services are legitimate outside bounded contexts

Without clear policy, services proliferate unchecked, violating DDD principles and creating maintenance burden.

---

## Decision

We adopt a **4-tier classification system** for infrastructure services with explicit criteria for each tier. Only services meeting tier criteria remain in infrastructure layer.

### Service Tiers

#### Tier 1: Infrastructure Adapters ✅ ALWAYS LEGITIMATE

**Purpose**: Integrate external systems with no business logic.

**Criteria**:
- Wraps external API, database, or service (Qdrant, Email, BGG API)
- Contains **zero business logic** (pure technical implementation)
- Implements adapter/gateway pattern
- Reusable across multiple contexts

**Examples**: QdrantService, EmailService, BggApiService
**Location**: `apps/api/src/Api/Services/`

---

#### Tier 2: Cross-Cutting Concerns ✅ ALWAYS LEGITIMATE

**Purpose**: Transversal functionality used by all bounded contexts.

**Criteria**:
- Used by **3+ bounded contexts** (not context-specific)
- Implements aspect-oriented concern (logging, caching, security, monitoring)
- No dependency on specific context's domain logic
- Infrastructure-level responsibility

**Examples**: AlertingService, RateLimitService, EncryptionService
**Location**: `apps/api/src/Api/Services/`

---

#### Tier 3: Orchestration Services ✅ LEGITIMATE WITH MONITORING

**Purpose**: Coordinate operations across multiple bounded contexts.

**Criteria**:
- Orchestrates **2+ bounded contexts** (RAG pipeline: KnowledgeBase + DocumentProcessing)
- Implements saga/orchestrator pattern
- Contains coordination logic, not core business logic
- Provides cross-context workflow

**Examples**: RagService, HybridSearchService
**Location**: `apps/api/src/Api/Services/`
**Monitoring**: Size limit 1000 lines (consider split if exceeded)

---

#### Tier 4: Shared Application Services ⚠️ EVALUATE PERIODICALLY

**Purpose**: Business logic shared by 2+ contexts (temporary/strategic).

**Criteria**:
- Used by **2+ contexts** (but not all)
- Contains some business logic (not pure infrastructure)
- Strategic decision to keep shared (avoid duplication)
- Reviewed quarterly for migration opportunities

**Examples**: PromptTemplateService, ChatExportService
**Location**: `apps/api/src/Api/Services/`
**Review**: Quarterly evaluation for context migration

---

### Disallowed: Context-Specific Services ❌

Services that:
- Belong to **single bounded context** (auth, game, document, knowledge)
- Contain context-specific business logic
- Are tightly coupled to one domain

**Action**: Migrate to `BoundedContexts/{Context}/Application/Services/` or `BoundedContexts/{Context}/Infrastructure/Services/`

**Migration Candidates (Issue #1680)**:
- OAuthService → Authentication context
- ApiKeyAuthenticationService → Authentication context

---

## Classification Decision Tree

```
┌─ Service Classification Decision ─┐
│                                    │
│  Does it integrate external system?│
│  ├─ YES → Tier 1 (Infrastructure) │
│  └─ NO ─┐                          │
│         │                          │
│  Used by ALL contexts?             │
│  ├─ YES → Tier 2 (CrossCutting)   │
│  └─ NO ─┐                          │
│         │                          │
│  Orchestrates 2+ contexts?         │
│  ├─ YES → Tier 3 (Orchestration)  │
│  └─ NO ─┐                          │
│         │                          │
│  Used by 2+ contexts?              │
│  ├─ YES → Tier 4 (SharedApp)      │
│  └─ NO → MIGRATE to context       │
│                                    │
└────────────────────────────────────┘
```

---

## Consequences

### Positive

1. **Clear Guidelines**: Developers know when to create infrastructure services vs context services
2. **DDD Compliance**: Services are properly layered and scoped
3. **Maintainability**: 39 services cataloged, categorized, and monitored
4. **Reduced Proliferation**: Policy prevents unchecked service growth
5. **Migration Path**: Clear criteria for moving services to contexts

### Negative

1. **Judgment Required**: Tier 3/4 classification requires architectural judgment
2. **Quarterly Overhead**: Tier 4 services require periodic review
3. **Migration Effort**: 2 services identified for potential migration
4. **Monitoring Burden**: Orchestration services need size monitoring

---

## Implementation

### New Service Creation

**Before creating a new service in** `apps/api/src/Api/Services/`:

1. **Check Classification**: Does it meet Tier 1-4 criteria?
2. **Verify Alternatives**: Can existing service be extended?
3. **Document Purpose**: Add XML docs with tier classification
4. **Update Catalog**: Add to `infrastructure-services.md`

**Template**:
```csharp
/// <summary>
/// [Brief description]
///
/// **Classification**: [Tier 1-4]
/// **Pattern**: [Adapter/CrossCutting/Orchestrator/SharedApp]
/// **Used By**: [List of contexts or "All contexts"]
/// </summary>
public class MyService : IMyService
{
    // Implementation
}
```

---

### Existing Service Evaluation

**Quarterly Review Process** (Q1, Q2, Q3, Q4):

1. **Usage Analysis**: Run `audit-infrastructure-services.ps1`
2. **Reference Count**: Identify services with 0 references → Remove
3. **Size Monitoring**: Flag services >1000 lines → Consider split
4. **Tier 4 Review**: Evaluate shared services for migration
5. **Update Catalog**: Refresh `infrastructure-services.md`

---

### Migration Process

**When migrating service to bounded context**:

1. **Create Issue**: Document migration rationale
2. **Identify Consumers**: Verify all usages
3. **Move Service**:
   - Context-specific logic → `BoundedContexts/{Context}/Application/Services/`
   - Context infrastructure → `BoundedContexts/{Context}/Infrastructure/Services/`
4. **Update Registrations**: Move DI from `Program.cs` to context
5. **Update Catalog**: Remove from infrastructure catalog
6. **Run Tests**: Verify no breaking changes

---

## Consolidation Guidelines

### When to Consolidate

✅ Consolidate when:
- Services share >70% implementation
- Services have overlapping purpose (e.g., alert channels)
- Services are internal implementation details (e.g., export formatters)

**Examples from Issue #1680**:
- Alert channels → Internal strategies in AlertingService
- Export formatters → Nested classes in ChatExportService

### How to Consolidate

1. **Identify Primary Service**: Which service is the coordinator?
2. **Convert to Strategies**: Make variants internal strategies/policies
3. **Preserve Interfaces**: Maintain public contracts if needed
4. **Update Tests**: Consolidate test files
5. **Remove Files**: Delete redundant service files

---

## Monitoring & Enforcement

### Automated Checks

**CI/CD Integration**:
```bash
# Run quarterly as part of architecture review
pwsh tools/audit-infrastructure-services.ps1

# Alert if:
# - New service added without catalog update
# - Service exceeds 1000 lines
# - Service has 0 references
```

### Code Review Checklist

**For new services in** `apps/api/src/Api/Services/`:

- [ ] Meets Tier 1-4 criteria (which tier?)
- [ ] Documented in XML comments with tier
- [ ] Added to `infrastructure-services.md`
- [ ] DI registration in appropriate location
- [ ] Tests cover service functionality

**For service modifications**:

- [ ] Size check (warn if >1000 lines)
- [ ] Reference check (warn if 0 references)
- [ ] Consider consolidation if similar service exists

---

## Examples

### ✅ CORRECT: Infrastructure Adapter

```csharp
/// <summary>
/// Qdrant vector database adapter.
///
/// **Classification**: Tier 1 (Infrastructure Adapter)
/// **Pattern**: Adapter
/// **Used By**: KnowledgeBase, DocumentProcessing
/// </summary>
public class QdrantService : IQdrantService
{
    // Pure technical integration, no business logic
}
```

---

### ✅ CORRECT: Cross-Cutting Concern

```csharp
/// <summary>
/// Multi-channel alerting service (Email, Slack, PagerDuty).
///
/// **Classification**: Tier 2 (Cross-Cutting Concern)
/// **Pattern**: Strategy
/// **Used By**: All bounded contexts
/// </summary>
public class AlertingService : IAlertingService
{
    // Used across all contexts for alerting
}
```

---

### ✅ CORRECT: Orchestration Service

```csharp
/// <summary>
/// RAG pipeline orchestrator (retrieval + generation + validation).
///
/// **Classification**: Tier 3 (Orchestration Service)
/// **Pattern**: Orchestrator
/// **Used By**: KnowledgeBase (primary), Administration (metrics)
/// **Size**: 852 lines (monitor for 1000+ threshold)
/// </summary>
public class RagService : IRagService
{
    // Coordinates: Embedding, Search, LLM, Validation
}
```

---

### ⚠️ BORDERLINE: Shared Application Service

```csharp
/// <summary>
/// Prompt template management with caching and versioning.
///
/// **Classification**: Tier 4 (Shared Application Service)
/// **Pattern**: Repository
/// **Used By**: KnowledgeBase, Administration
/// **Review**: Q1 2026 - Consider migration to KnowledgeBase context
/// </summary>
public class PromptTemplateService : IPromptTemplateService
{
    // Contains some business logic but shared across contexts
}
```

---

### ❌ INCORRECT: Should Be in Bounded Context

```csharp
/// <summary>
/// OAuth authentication flow management.
///
/// **Classification**: ❌ SHOULD BE IN Authentication CONTEXT
/// **Pattern**: Application Service
/// **Used By**: Authentication ONLY
/// **Action**: Migrate to BoundedContexts/Authentication/Application/Services/
/// </summary>
public class OAuthService : IOAuthService
{
    // Context-specific business logic → belongs in Authentication context
}
```

---

## References

- [Infrastructure Services Catalog](../../02-development/infrastructure-services.md)
- [DDD Migration Status](../overview/ddd-migration-status.md)
- Issue #1680: Audit Infrastructure Services
- ADR-001: Bounded Context Architecture
- ADR-003: Service Layer Responsibilities

---

## Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2025-12-07 | Adopt 4-tier classification | Provides clear criteria for service placement |
| 2025-12-07 | Tier 3 size limit: 1000 lines | Prevents orchestrators from becoming monoliths |
| 2025-12-07 | Tier 4 quarterly review | Ensures shared services don't accumulate unchecked |
| 2025-12-07 | Migration candidates: OAuth, ApiKeyAuth | Context-specific logic identified |

---

**Next Review**: 2026-03-07 (Quarterly)
**Owner**: Engineering Team
