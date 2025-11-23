# Issue #022: Audit Infrastructure Services

**Priority:** 🟢 LOW
**Category:** Architecture / Documentation
**Estimated Effort:** 2-3 days
**Sprint:** MEDIUM-TERM (3-6 months)

## Summary

39 infrastructure service files remain after DDD migration. While most are legitimate infrastructure/adapter layer services, they should be audited, documented, and potentially consolidated.

## Background

According to CLAUDE.md, 2,070 lines of legacy service code was eliminated during DDD migration:
- ✅ GameService (181 lines) - Migrated to GameManagement context
- ✅ AuthService (346 lines) - Migrated to Authentication context
- ✅ PDF services (1,300 lines) - Migrated to DocumentProcessing context
- ✅ UserManagementService (243 lines) - Migrated to Administration context

**Retained as legitimate infrastructure services:**
- ConfigurationService - Dynamic config management
- AdminStatsService - Statistics/analytics
- AlertingService - Email/Slack/PagerDuty alerts
- RagService - RAG orchestration (infrastructure, not domain)

## Remaining Infrastructure Services (39 files)

```
apps/api/src/Api/Services/

├── Core Services (Retained)
│   ├── ConfigurationService.cs
│   ├── AdminStatsService.cs
│   ├── AlertingService.cs
│   └── RagService.cs

├── AI/LLM Services (11)
│   ├── AiResponseService.cs
│   ├── EmbeddingService.cs
│   ├── PromptService.cs
│   ├── RagEvaluationService.cs
│   ├── ResponseQualityService.cs
│   ├── WeeklyEvaluationService.cs
│   └── LlmClients/
│       ├── OllamaLlmClient.cs
│       ├── OpenRouterLlmClient.cs
│       ├── AzureOpenAiLlmClient.cs
│       ├── AnthropicLlmClient.cs
│       └── GeminiLlmClient.cs

├── Search/RAG Services (4)
│   ├── HybridSearchService.cs
│   ├── KeywordSearchService.cs
│   ├── QdrantService.cs
│   └── TextChunkingService.cs

├── PDF Processing Services (5)
│   └── Pdf/
│       ├── PdfTextExtractorFactory.cs
│       ├── DocnetPdfTextExtractor.cs
│       ├── UnstructuredPdfTextExtractor.cs
│       ├── SmolDoclingPdfTextExtractor.cs
│       └── TesseractOcrAdapter.cs

├── Authentication/Security Services (6)
│   ├── ApiKeyService.cs
│   ├── EncryptionService.cs
│   ├── OAuthService.cs
│   ├── PasswordService.cs
│   ├── SessionService.cs
│   └── TotpService.cs

├── Infrastructure Services (8)
│   ├── BackgroundJobService.cs
│   ├── BggApiService.cs (BoardGameGeek API)
│   ├── CacheWarmingService.cs
│   ├── EmailService.cs
│   ├── HybridCacheService.cs
│   ├── LanguageDetectionService.cs
│   ├── N8nWebhookService.cs
│   └── WorkflowErrorLoggingService.cs

├── Quality/Monitoring Services (3)
│   ├── AuditLoggingService.cs
│   ├── QualityMetricsService.cs
│   └── RateLimitService.cs

├── Data Export (2)
│   ├── ChatExportService.cs
│   └── FeatureFlagService.cs
```

---

## Audit Questions

For each service, determine:

1. **Is it actively used?**
   - Search for references in codebase
   - Check DI registrations
   - Verify endpoint/handler usage

2. **Is it properly categorized?**
   - Infrastructure adapter (external service integration)
   - Cross-cutting concern (logging, caching, monitoring)
   - Orchestration service (coordinates domain logic)
   - OR: Should be migrated to bounded context?

3. **Is it documented?**
   - Purpose and responsibilities clear?
   - Interface/contract defined?
   - Usage examples available?

4. **Can it be consolidated?**
   - Overlaps with other services?
   - Too granular (merge with related service)?
   - Too large (split into smaller services)?

5. **Does it violate DDD?**
   - Contains business logic (should be in domain)?
   - Tightly coupled to specific context?
   - Can be refactored to adapter pattern?

---

## Tasks

### Phase 1: Inventory & Usage Analysis (1 day)

#### 1.1 Create Service Registry
- [ ] List all 39 services with metadata:
  - File path
  - Lines of code
  - Dependencies
  - Registered in DI? (check Program.cs)
  - Last modified date

#### 1.2 Usage Analysis
For each service:
```bash
# Example for ApiResponseService
grep -r "ApiResponseService" apps/api/src --include="*.cs"
grep -r "IApiResponseService" apps/api/src --include="*.cs"

# Check DI registration
grep "AddScoped.*ApiResponseService" apps/api/src/Api/Program.cs
```

- [ ] Document usage count
- [ ] Identify primary consumers
- [ ] Flag unused services for removal

#### 1.3 Categorization
- [ ] Categorize each service:
  - **Keep (Infrastructure):** External integrations, cross-cutting
  - **Keep (Orchestration):** Complex workflows, multi-context coordination
  - **Consolidate:** Merge with similar services
  - **Migrate:** Move to specific bounded context
  - **Remove:** Unused or obsolete

---

### Phase 2: Documentation (1 day)

#### 2.1 Create Service Catalog
Create `docs/02-development/infrastructure-services.md`:

```markdown
# Infrastructure Services Catalog

## Purpose
Infrastructure services handle cross-cutting concerns and external integrations.
They should NOT contain business logic (that belongs in domain layer).

## Services by Category

### AI/LLM Services
- **EmbeddingService:** Vector embeddings via OpenRouter
- **LlmClients/*:** Provider-specific LLM integrations
- ...

### PDF Processing
- **PdfTextExtractorFactory:** 3-stage extraction pipeline
- ...

[Continue for all categories]
```

#### 2.2 Document Each Service
Add XML documentation to each service:

```csharp
/// <summary>
/// Provides hybrid search combining vector (Qdrant) and keyword (PostgreSQL) search.
/// Uses Reciprocal Rank Fusion (RRF) with 70/30 weighting.
/// </summary>
/// <remarks>
/// This is an infrastructure orchestration service. Domain logic for search relevance
/// and quality validation belongs in KnowledgeBase bounded context.
/// </remarks>
public class HybridSearchService : IHybridSearchService
{
    // ...
}
```

#### 2.3 Create Architecture Decision Record
Create `docs/01-architecture/adr/adr-00X-infrastructure-services.md`:
- Why we keep these services
- Distinction from domain services
- When to create new infrastructure service vs. domain service
- Guidelines for preventing business logic leakage

---

### Phase 3: Consolidation Opportunities (1 day)

#### 3.1 Identify Overlaps

**Example consolidation candidates:**

1. **Email/Alerting:**
   - `EmailService.cs` - Send emails
   - `AlertingService.cs` - Send alerts via email/Slack/PagerDuty
   - **Action:** Merge? Or keep separate (email is low-level, alerting is orchestration)?

2. **Caching:**
   - `HybridCacheService.cs` - L1/L2 caching
   - `CacheWarmingService.cs` - Preload cache
   - **Action:** Keep separate (different concerns) or merge into single caching namespace?

3. **Quality Services:**
   - `ResponseQualityService.cs`
   - `QualityMetricsService.cs`
   - `RagEvaluationService.cs`
   - **Action:** Consolidate into `QualityMonitoringService`?

#### 3.2 Evaluate LLM Clients
- [ ] Are all 5 LLM clients actively used?
- [ ] Can we reduce to 2-3 primary providers?
- [ ] Should they be in a separate library/package?

#### 3.3 Create Consolidation Plan
- [ ] Document proposed merges
- [ ] Estimate effort
- [ ] Identify breaking changes
- [ ] Create migration plan

---

### Phase 4: Migration Candidates

#### 4.1 Review for Domain Logic Leakage

Check each service for business logic that should be in domain:

**Example - PromptService:**
```csharp
// If contains rules like "use this prompt template for Italian games"
// → Should be PromptSelectionDomainService in KnowledgeBase context

// If just loads/saves prompts from storage
// → OK as infrastructure
```

#### 4.2 Identify Context-Specific Services

Services tightly coupled to one bounded context:

- `ChatExportService` → Might belong in KnowledgeBase
- `BggApiService` → Might belong in GameManagement
- `FeatureFlagService` → Might belong in SystemConfiguration

#### 4.3 Create Migration Issues
For services that should be migrated, create separate GitHub issues.

---

## Specific Service Analysis

### High Priority for Review

#### 1. RagService (Current: Orchestration)
**Usage:** Core search/chat functionality
**Question:** Does it contain business logic or just orchestration?
**Action:**
- [ ] Review implementation
- [ ] Document why it's infrastructure vs. domain
- [ ] Consider splitting if too large

#### 2. LLM Client Services (5 clients)
**Usage:** Multi-provider LLM support
**Question:** Are all 5 needed? Performance overhead?
**Action:**
- [ ] Check usage metrics
- [ ] Keep 2-3 most used
- [ ] Consider plugin architecture for others

#### 3. PDF Services (5 services)
**Status:** Recently refactored (3-stage pipeline, ADR-003b)
**Question:** All still needed after orchestrator pattern?
**Action:**
- [ ] Verify orchestrator uses all 3 extractors
- [ ] Check if factory pattern is optimal
- [ ] Document pipeline architecture

#### 4. Authentication Services (6 services)
**Question:** Should these be in Authentication bounded context?
**Current:** Infrastructure layer (used by auth handlers)
**Action:**
- [ ] Evaluate if they contain domain logic
- [ ] Consider moving to Authentication/Infrastructure/

---

## Success Criteria

- [ ] All 39 services cataloged with usage data
- [ ] Each service categorized (Keep/Consolidate/Migrate/Remove)
- [ ] Documentation created (service catalog + ADR)
- [ ] Consolidation plan created for overlapping services
- [ ] Migration issues created for context-specific services
- [ ] Zero unused services remain
- [ ] Clear guidelines for future service creation

---

## Deliverables

1. **Service Inventory Spreadsheet**
   - Name, Category, LOC, Usage Count, DI Status, Recommendation

2. **Documentation**
   - `docs/02-development/infrastructure-services.md`
   - `docs/01-architecture/adr/adr-00X-infrastructure-services.md`

3. **Consolidation Plan**
   - Proposed merges with rationale
   - Breaking changes analysis
   - Migration timeline

4. **Follow-up Issues**
   - Individual issues for each migration/consolidation
   - Prioritized by impact and effort

---

## Related Issues

- DDD Migration (99% complete)
- Issue #010: Resolve Backend TODOs
- Issue #013: Remove Obsolete Models
- Clean Architecture principles

## References

- Current services: `apps/api/src/Api/Services/`
- DI registrations: `apps/api/src/Api/Program.cs`
- CLAUDE.md: Lists retained services
- Legacy code analysis: Section 5 (Legacy Services)
- Clean Architecture: Infrastructure layer guidelines

## Notes

**Why this is LOW priority:**
- Services are functional and don't block development
- More documentation/organization than critical refactoring
- Can be done incrementally during maintenance sprints

**Long-term benefit:**
- Clearer architecture
- Easier onboarding for new developers
- Prevents service proliferation
- Maintains DDD purity

**Estimated Outcome:**
- 5-10 services consolidated
- 2-5 services migrated to contexts
- 1-3 services removed as unused
- ~25-30 services remain (well-documented)
