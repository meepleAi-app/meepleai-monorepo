# Infrastructure Services Catalog

**Issue**: #1680
**Last Updated**: 2025-12-07
**Status**: Active Inventory (Post-DDD Migration)

---

## Overview

After the DDD migration (100% complete), **39 infrastructure services** remain in `apps/api/src/Api/Services/`. This document provides a comprehensive audit, categorization, and guidelines for these services.

### Categorization Summary

| Category | Count | Status | Description |
|----------|-------|--------|-------------|
| Infrastructure Adapters | 12 | ✅ Legitimate | External system integrations (Qdrant, Email, BGG) |
| Cross-Cutting Concerns | 10 | ✅ Legitimate | Transversal services (Logging, Caching, Security) |
| Orchestration Services | 8 | ✅ Legitimate | Coordinate cross-context operations (RAG, Search) |
| Shared Application Services | 7 | ⚠️ Borderline | Shared business logic (Prompt, Export, Session) |
| Migration Candidates | 2 | 🔄 Consider | May belong in bounded contexts (OAuth, ApiKeyAuth) |
| **TOTAL** | **39** | | |

---

## Service Categories

### 1. Infrastructure Adapters (12 services) ✅

External system integrations with **no business logic**.

| Service | Purpose | External System | References | Lines |
|---------|---------|-----------------|------------|-------|
| **QdrantService** | Vector database operations | Qdrant | 19 | 524 |
| **EmbeddingService** | Text embedding generation | OpenRouter/Ollama | 17 | 208 |
| **EmailService** | Email delivery | SMTP | 6 | 241 |
| **BggApiService** | Board game data fetch | BoardGameGeek API | 6 | 367 |
| **N8nConfigService** | n8n workflow config | n8n | 6 | 329 |
| **N8nTemplateService** | n8n template management | n8n | 5 | 583 |
| QdrantClientAdapter | Qdrant client wrapper | Qdrant SDK | 6 | - |
| RedisFrequencyTracker | Rate limit tracking | Redis | 5 | - |
| TotpService | TOTP token generation | OTP libraries | 5 | 718 |
| LanguageDetectionService | Language detection | Detection library | 5 | 213 |
| EmailAlertChannel | Email alert delivery | SMTP (via EmailService) | - | - |
| SlackAlertChannel | Slack alert delivery | Slack API | - | - |

**Pattern**: Adapter/Gateway
**Recommendation**: Keep as infrastructure layer. Consider consolidating similar adapters.

---

### 2. Cross-Cutting Concerns (10 services) ✅

Transversal services used across **all bounded contexts**.

| Service | Purpose | Cross-Cutting Aspect | References | Lines |
|---------|---------|---------------------|------------|-------|
| **AlertingService** | Multi-channel alerting | Observability | 18 | 287 |
| **RateLimitService** | API throttling | Security/Performance | 15 | 384 |
| **EncryptionService** | Data encryption | Security | 11 | 83 |
| **AuditService** | Audit logging | Compliance | 10 | 91 |
| ConfigurationService | Dynamic configuration | System Configuration | 9 | 150 |
| HybridCacheService | L1+L2 caching | Performance | 5 | 489 |
| CacheWarmingService | Cache optimization | Performance | 6 | 269 |
| PasswordHashingService | Password hashing | Security | 9 | 144 |
| BackgroundTaskService | Task orchestration | Infrastructure | 6 | 99 |
| FeatureFlagService | Feature toggles | Configuration | 6 | 203 |

**Pattern**: Aspect-Oriented Programming
**Recommendation**: Keep as shared services. Essential for cross-context operations.

---

### 3. Orchestration Services (8 services) ✅

Coordinate operations across **multiple bounded contexts**.

| Service | Purpose | Orchestrates | References | Lines |
|---------|---------|--------------|------------|-------|
| **RagService** | RAG pipeline orchestration | Embedding, Search, LLM | 21 | 852 |
| **HybridSearchService** | Hybrid search (vector+keyword) | Qdrant, Keyword Search | 10 | 403 |
| PromptEvaluationService | Prompt quality evaluation | LLM, Templates | 7 | 969 |
| RagEvaluationService | RAG pipeline evaluation | RAG, Quality Metrics | 5 | 616 |
| WeeklyEvaluationService | Weekly RAG metrics | RAG, Evaluation | 5 | 375 |
| ResponseQualityService | Response validation | LLM, Quality Checks | 2 | 203 |
| QualityReportService | Quality reporting | Metrics, Reports | 7 | 193 |
| WorkflowErrorLoggingService | Workflow error handling | n8n, Logging | 5 | 189 |

**Pattern**: Orchestrator/Saga
**Recommendation**: Keep as orchestration layer. Monitor size (RagService: 852 lines, consider split at 1000+).

---

### 4. Shared Application Services (7 services) ⚠️

Shared business logic used by **multiple contexts**.

| Service | Purpose | Used By | References | Lines | Migration? |
|---------|---------|---------|------------|-------|------------|
| **PromptTemplateService** | Prompt template management | RagService, Evaluation | 11 | 509 | Maybe (KnowledgeBase) |
| ChatExportService | Chat export (MD/PDF/TXT) | KnowledgeBase | 6 | 111 | No (shared utility) |
| AiResponseCacheService | AI response caching | RagService | 6 | 239 | No (performance) |
| SessionCacheService | Session caching | Authentication | 5 | 183 | Maybe (Authentication) |
| TempSessionService | Temporary session mgmt | Authentication/2FA | 5 | 140 | Maybe (Authentication) |
| PasswordResetService | Password reset flow | Authentication | 5 | 297 | Maybe (Authentication) |
| SessionAutoRevocationService | Auto session cleanup | Authentication | 5 | 111 | Maybe (Authentication) |

**Pattern**: Shared Kernel
**Recommendation**: Evaluate migration to specific contexts if single-context usage detected.

---

### 5. Migration Candidates (2 services) 🔄

Services that **may belong** in bounded contexts.

| Service | Purpose | Candidate Context | References | Lines | Rationale |
|---------|---------|------------------|------------|-------|-----------|
| OAuthService | OAuth flow management | Authentication | 5 | 541 | Auth-specific business logic |
| ApiKeyAuthenticationService | API key authentication | Authentication | 6 | 290 | Auth-specific logic |

**Recommendation**: Create follow-up issue to evaluate migration to `Authentication` bounded context.

---

## Top 10 Most-Used Services

| Rank | Service | References | Category | Critical? |
|------|---------|------------|----------|-----------|
| 1 | **RagService** | 21 | Orchestration | ✅ YES |
| 2 | **QdrantService** | 19 | Infrastructure | ✅ YES |
| 3 | **AlertingService** | 18 | CrossCutting | ✅ YES |
| 4 | **EmbeddingService** | 17 | Infrastructure | ✅ YES |
| 5 | **RateLimitService** | 15 | CrossCutting | ✅ YES |
| 6 | **EncryptionService** | 11 | CrossCutting | ✅ YES |
| 7 | **PromptTemplateService** | 11 | SharedApp | ⚠️ YES |
| 8 | **HybridSearchService** | 10 | Orchestration | ✅ YES |
| 9 | **AuditService** | 10 | CrossCutting | ✅ YES |
| 10 | **KeywordSearchService** | 9 | Infrastructure | ⚠️ YES |

**Note**: All top 10 services are actively used and critical to system operation.

---

## Export Formatters (3 internal services)

Internal implementations used by `ChatExportService`:

- `MdExportFormatter` → Markdown export
- `PdfExportFormatter` → PDF export
- `TxtExportFormatter` → Plain text export

**Recommendation**: These are implementation details of `ChatExportService`. Consider making them internal/private classes.

---

## Consolidation Opportunities

### 1. Alert Channels
**Current**: EmailAlertChannel, SlackAlertChannel, PagerDutyAlertChannel (separate files)
**Recommendation**: Consolidate into `AlertingService` as internal strategies.

### 2. Export Formatters
**Current**: 3 separate formatter files
**Recommendation**: Move as nested classes inside `ChatExportService`.

### 3. Session Services
**Current**: SessionCacheService, TempSessionService, SessionAutoRevocationService
**Recommendation**: Evaluate consolidation into `Authentication` bounded context.

---

## Usage Guidelines

### When to Create Infrastructure Services

✅ **YES** - Create infrastructure service when:
- Integrating external system (API, database, service)
- Implementing cross-cutting concern (logging, caching, security)
- Orchestrating cross-context operations (RAG, search pipelines)
- Providing shared utility (export, language detection)

❌ **NO** - Don't create infrastructure service when:
- Logic is specific to one bounded context → Use Application Service in context
- Logic is pure domain behavior → Use Domain Service in context
- Logic is trivial adapter → Consider inline or internal class

### Classification Decision Tree

```
Is it integrating an external system?
├─ YES → Infrastructure Adapter (Keep)
└─ NO
   ├─ Is it used by all/most bounded contexts?
   │  ├─ YES → Cross-Cutting Concern (Keep)
   │  └─ NO
   │     ├─ Does it coordinate multiple contexts?
   │     │  ├─ YES → Orchestration Service (Keep)
   │     │  └─ NO
   │     │     ├─ Is it shared by 2+ contexts?
   │     │     │  ├─ YES → Shared Application Service (Evaluate)
   │     │     │  └─ NO → Migration Candidate (Move to context)
```

---

## Maintenance Checklist

### Quarterly Review
- [ ] Verify all services are actively used (references > 0)
- [ ] Check for services exceeding 1000 lines (split candidates)
- [ ] Identify unused dependencies in services
- [ ] Review migration candidates for context-specific logic

### Before Adding New Service
- [ ] Check if existing service can be extended
- [ ] Verify service belongs in infrastructure (not domain)
- [ ] Document purpose and category in this catalog
- [ ] Add XML documentation to service class

### Before Removing Service
- [ ] Verify zero references across codebase
- [ ] Check DI registrations (none remaining)
- [ ] Remove from Program.cs / ServiceCollectionExtensions
- [ ] Update this catalog

---

## Related Documentation

- [ADR-017: Infrastructure Services Policy](../01-architecture/adr/adr-017-infrastructure-services-policy.md)
- [DDD Migration Status](../01-architecture/overview/ddd-migration-status.md)
- [Service Architecture](../01-architecture/overview/service-architecture.md)

---

## Follow-Up Issues

**Recommended Actions**:

1. **Issue**: Evaluate OAuthService migration to Authentication context
   - **Priority**: LOW
   - **Effort**: 2-3 days
   - **Impact**: Better DDD alignment

2. **Issue**: Consolidate Export Formatters into ChatExportService
   - **Priority**: LOW
   - **Effort**: 1 day
   - **Impact**: Reduce file count, simplify structure

3. **Issue**: Monitor RagService size (852 lines)
   - **Priority**: MEDIUM
   - **Effort**: 3-5 days (if split needed)
   - **Impact**: Maintainability, testability

4. **Issue**: Add XML documentation to top 10 critical services
   - **Priority**: MEDIUM
   - **Effort**: 2 days
   - **Impact**: Better developer experience

---

**Last Audit**: 2025-12-07
**Next Review**: 2026-03-07 (Quarterly)
**Owner**: Engineering Team
