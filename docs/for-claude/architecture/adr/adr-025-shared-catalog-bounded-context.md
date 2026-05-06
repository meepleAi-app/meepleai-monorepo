# ADR-025: SharedGameCatalog Bounded Context Separation

**Status**: Accepted
**Date**: 2026-01-14
**Deciders**: Development Team
**Issue**: #2425 (Parent: #2374 Phase 5)

---

## Context

MeepleAI manages both a **community-shared game catalog** (SharedGameCatalog) and **personal game collections** (GameManagement). These two concerns have different:
- **Ownership models**: Community vs individual
- **Authorization requirements**: Admin/Editor vs User
- **Data lifecycles**: Shared catalog is permanent, personal collections are ephemeral
- **Scalability needs**: Catalog serves all users, collections are user-specific

We needed to decide whether to combine these into a single GameManagement context or separate them.

---

## Decision

**We separate SharedGameCatalog as an independent bounded context** with:
- Dedicated database tables (`shared_games`, `game_categories`, `game_mechanics`, etc.)
- Separate CQRS operations (20 commands/queries)
- Independent authorization policies (AdminOnlyPolicy, AdminOrEditorPolicy)
- Distinct API endpoints (`/api/v1/shared-games`, `/api/v1/admin/shared-games`)

---

## Rationale

### 1. Single Responsibility Principle
- **SharedGameCatalog**: Community governance, catalog maintenance, BGG integration
- **GameManagement**: Personal collections, play sessions, user-specific rules

Mixing these concerns violates SRP and creates cognitive overload.

### 2. Authorization Boundaries
- **Catalog**: Role-based (Admin can publish, Editor can create/edit, User can read)
- **Personal**: User-owned (each user manages only their games)

Separate contexts allow distinct authorization models without complex if/else logic.

### 3. Independent Scaling
- **Catalog**: Read-heavy (thousands of searches/day), benefits from aggressive caching
- **Personal**: Write-heavy (users adding games), less cacheable

Separation enables targeted optimization strategies.

### 4. Clear Domain Boundaries
- **Catalog**: Source of truth for verified game data (BGG imports, community edits)
- **Personal**: Links to catalog via `SharedGameId` FK for enriched data

This creates a clean dependency: Personal → Catalog (unidirectional).

### 5. Future Multi-Tenancy
Separate contexts prepare for potential future where:
- Different organizations maintain their own catalogs
- Personal collections can link to multiple catalog sources
- Catalog can be deployed independently (microservices)

---

## Alternatives Considered

### Alternative 1: Single GameManagement Context
**Rejected**: Mixing community catalog with personal collections violates domain boundaries.

**Problems**:
- Authorization complexity (role-based AND ownership-based in same context)
- Unclear ownership (who can edit? admin? user? depends on entity type)
- Coupled scaling (can't optimize catalog independently)
- Namespace pollution (SharedGame vs Game confusion)

### Alternative 2: Shared Kernel Pattern
**Rejected**: Game entities are not value objects suitable for shared kernels.

**Problems**:
- Shared kernel should contain only value objects and domain primitives
- Game is an aggregate root with complex lifecycle
- Updates to shared kernel affect both contexts (high coupling risk)

---

## Consequences

### Positive
- ✅ Clear ownership and authorization boundaries
- ✅ Independent optimization strategies (cache TTLs, indexes)
- ✅ Reduced coupling between community and personal features
- ✅ Easier to reason about and maintain
- ✅ Supports future multi-tenancy or microservices architecture

### Negative
- ❌ Data duplication (SharedGame + Game entities store similar fields)
- ❌ Additional complexity (2 repositories, 2 sets of endpoints)
- ❌ Sync overhead (if catalog game updated, personal games may be stale)

### Mitigation Strategies
- **Data Duplication**: Accept as trade-off for bounded context independence
- **Sync Overhead**: Personal games link via `SharedGameId` FK, can refresh on-demand
- **Complexity**: DDD layer structure (Domain/Application/Infrastructure) organizes code clearly

---

## Compliance

This decision aligns with:
- **DDD Principles**: Each bounded context has clear ubiquitous language
- **SOLID**: Single Responsibility (catalog ≠ personal collection)
- **Microservices Ready**: Can extract SharedGameCatalog to separate service if needed

---

## References

- Issue #2370: SharedGameCatalog Phase 1 (Backend Foundation)
- Issue #2372: SharedGameCatalog Phase 3 (Frontend Admin UI)
- Issue #2373: SharedGameCatalog Phase 4 (User-Facing Features)
- Domain-Driven Design (Eric Evans, 2003)
- Implementing Domain-Driven Design (Vaughn Vernon, 2013)
