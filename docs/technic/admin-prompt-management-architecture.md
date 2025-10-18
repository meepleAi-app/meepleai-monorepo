# Admin-Configurable Prompt Management System - Architecture Design

**Document Version**: 1.0
**Created**: 2025-10-18
**Status**: Design Document
**Related Issues**: ADMIN-01

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Requirements Analysis](#requirements-analysis)
3. [Architecture Options](#architecture-options)
4. [Recommended Architecture](#recommended-architecture)
5. [Risk Assessment](#risk-assessment)
6. [Implementation Phases](#implementation-phases)
7. [Appendices](#appendices)

---

## Executive Summary

This document presents the architectural design for an admin-configurable prompt management system for MeepleAI. The system will replace hardcoded LLM prompts in four core services (RagService, StreamingQaService, ChessAgentService, SetupGuideService) with a database-driven, versioned, and auditable solution.

**Key Goals**:
- Enable non-technical administrators to modify prompts without code deployments
- Version all prompt changes with full audit history
- Support zero-downtime prompt updates via Redis caching
- Implement comprehensive prompt testing framework to prevent quality regressions
- Maintain backward compatibility during migration

**Current State**:
- Database schema: READY (tables exist but unused)
- DTOs: READY (complete API models)
- Services: HARDCODED (4 services with embedded prompts)
- Admin UI: NOT IMPLEMENTED

**Recommended Approach**: Redis-cached service layer with React admin UI, comprehensive prompt evaluation framework, and phased migration strategy.

---

## Requirements Analysis

### Confidence Assessment: 9/10

We have excellent clarity on requirements due to existing database schema, DTOs, and codebase patterns. Minor uncertainty around A/B testing requirements and specific quality metrics.

### 1. Functional Requirements

#### Core Capabilities
- **FR-1**: Admin users can create, edit, and activate prompt templates via web UI
- **FR-2**: System maintains full version history for all prompts
- **FR-3**: Only one version of a template can be active at a time
- **FR-4**: All agents (Q&A, Chess, Setup Guide) retrieve prompts from database
- **FR-5**: Prompt changes are audited (who, what, when, why)
- **FR-6**: Support for prompt metadata (tags, descriptions, test results)

#### Prompt Testing Framework
- **FR-7**: Validate prompt changes against test dataset before activation
- **FR-8**: Compare two prompt versions side-by-side (A/B testing)
- **FR-9**: Automated regression detection for quality metrics
- **FR-10**: Generate reports with metrics: accuracy, hallucination rate, confidence, latency

### 2. Non-Functional Requirements

#### Scale
- **NFR-1**: Support 50+ prompt templates (current: 4, growth: 10x)
- **NFR-2**: Handle 100K+ prompt retrievals per day (current: 10K req/day)
- **NFR-3**: Maintain < 10ms latency for cached prompt retrieval
- **NFR-4**: Support 10+ concurrent admin users

#### Performance
- **NFR-5**: Prompt retrieval: < 10ms (p95) with Redis cache
- **NFR-6**: Prompt activation: < 500ms (cache invalidation + update)
- **NFR-7**: Admin UI response time: < 300ms for list/view operations

#### Security
- **NFR-8**: Prompt management restricted to admin role only
- **NFR-9**: All changes logged in audit trail (prompt_audit_logs)
- **NFR-10**: Support rollback to any previous version

#### Availability
- **NFR-11**: Zero downtime during prompt updates
- **NFR-12**: Cache miss fallback to database (no service disruption)
- **NFR-13**: Admin UI available 99.5% of the time

### 3. Constraints

#### Technical Constraints
- **C-1**: Must use existing PostgreSQL database (prompt_templates, prompt_versions, prompt_audit_logs)
- **C-2**: Must integrate with existing Redis infrastructure
- **C-3**: Must follow ASP.NET Core 9.0 + Next.js 14 stack
- **C-4**: Must maintain existing API contracts during migration

#### Operational Constraints
- **C-5**: Cannot require database migration (schema already exists)
- **C-6**: Must support gradual rollout (service-by-service migration)
- **C-7**: Budget: Development time only (no new infrastructure)

#### Team Constraints
- **C-8**: Team familiar with C# / TypeScript / React
- **C-9**: Standard MeepleAI patterns (DI, EF Core, xUnit, Jest)

### 4. Assumptions

- **A-1**: Prompt updates are infrequent (< 10 per day)
- **A-2**: Redis is reliable and available (existing infrastructure)
- **A-3**: Admin users have technical understanding of prompt engineering
- **A-4**: Test datasets will be manually curated by domain experts
- **A-5**: Quality metrics can be calculated within 1-2 minutes per test run

### 5. Open Questions

- **Q-1**: Do we need real-time A/B testing in production (split traffic)? → **Decision**: Phase 2 feature
- **Q-2**: Should prompts support templating (e.g., `{{gameTitle}}`)? → **Decision**: Yes, simple string interpolation
- **Q-3**: Maximum prompt size limit? → **Decision**: 16KB (sufficient for system prompts)
- **Q-4**: Prompt testing in CI/CD or on-demand only? → **Decision**: Both (CI for migrations, on-demand for testing)

---

## Architecture Options

### Option 1: Simple Database Query (No Cache)

#### Description
Services query `prompt_templates` and `prompt_versions` tables directly via Entity Framework on every LLM request.

#### Architecture Diagram (Text)
```
Request → Service → EF Core → PostgreSQL → PromptVersion (active=true)
                  ↓
                 LLM API (with fetched prompt)
```

#### Technology Choices
- **Data Access**: Entity Framework Core 9.0
- **Service Layer**: New `PromptTemplateService` with methods: `GetActivePromptAsync(templateName)`
- **No caching layer**

#### Pros
- **Simplicity**: Minimal code, no cache management
- **Consistency**: Always reads latest active version from database
- **Easy debugging**: Direct SQL queries visible in logs
- **No cache invalidation complexity**

#### Cons
- **Performance**: Database query on every LLM request (10K+ req/day = 10K+ DB queries)
- **Latency**: 20-50ms latency added per request (unacceptable)
- **Scalability**: Database becomes bottleneck under load
- **Load**: Increases DB connection pool pressure

#### Cost Implications
- **Infrastructure**: $0 (no new resources)
- **Performance**: High latency cost (user experience degraded)
- **Scalability**: Requires DB scaling sooner

#### Verdict
❌ **Not Recommended** - Unacceptable latency for high-frequency operations.

---

### Option 2: Redis-Cached Service Layer (Recommended)

#### Description
Services retrieve prompts from Redis cache (first) with fallback to PostgreSQL. Cache invalidation on activation. Admin UI for CRUD operations.

#### Architecture Diagram (Text)
```json
Request → PromptTemplateService.GetActivePromptAsync("qa-system-prompt")
           ↓
           Redis Cache (key: "prompt:qa-system-prompt:active")
           ↓ (cache miss)
           EF Core → PostgreSQL → PromptVersion (active=true)
           ↓
           Write to Redis (TTL: 1 hour, manual invalidation on update)
           ↓
          Return cached prompt
           ↓
         LLM API (with cached prompt)

Admin UI → POST /api/v1/admin/prompts/{id}/versions/{versionId}/activate
           ↓
           PromptTemplateService.ActivateVersionAsync(...)
           ↓
           Transaction: Update active flags + Create audit log
           ↓
           Redis INVALIDATE key "prompt:{name}:active"
           ↓
           Next request rebuilds cache
```

#### Technology Choices
- **Cache**: Redis (existing infrastructure)
- **Service Layer**:
  - `IPromptTemplateService` (interface)
  - `PromptTemplateService` (implementation with Redis + EF Core)
- **Data Access**: Entity Framework Core 9.0
- **Cache Client**: `StackExchange.Redis` (existing)
- **Admin API**: ASP.NET Core Minimal APIs under `/api/v1/admin/prompts/*`
- **Admin UI**: Next.js 14 pages under `/admin/prompts`
- **UI Components**: shadcn/ui (consistent with MeepleAI patterns)
- **Code Editor**: Monaco Editor (VS Code editor component)

#### Pros
- **Performance**: < 10ms prompt retrieval (Redis in-memory)
- **Scalability**: Handles 100K+ req/day without DB load
- **Reliability**: Fallback to DB if cache unavailable
- **Zero Downtime**: Cache invalidation ensures instant updates
- **Cost-Effective**: Uses existing Redis infrastructure
- **Future-Proof**: Foundation for A/B testing (Phase 2)

#### Cons
- **Complexity**: Cache invalidation logic required
- **Consistency Risk**: Short window between DB update and cache invalidation (mitigated by transaction)
- **Memory Usage**: ~50KB per prompt × 50 templates = 2.5MB (negligible)
- **Debugging**: Requires checking both cache and DB state

#### Cost Implications
- **Infrastructure**: $0 (Redis already provisioned)
- **Development**: 2-3 weeks (service + UI + tests)
- **Operational**: Minimal (cache monitoring already in place)

#### Cache Strategy Details
```csharp
// Cache Key Format
"prompt:{templateName}:active"  // Example: "prompt:qa-system-prompt:active"

// Cache Structure (JSON serialized PromptVersionEntity)
{
  "id": "abc123",
  "templateId": "xyz789",
  "versionNumber": 5,
  "content": "You are a board game assistant...",
  "metadata": "{\"testResults\": {...}}",
  "createdAt": "2025-10-15T10:00:00Z"
}

// TTL Strategy
- TTL: 1 hour (3600 seconds) - safety net for cache consistency
- Manual Invalidation: On activation, deactivation, template deletion
- Cache Warming: Pre-populate cache for critical prompts on startup

// Fallback Logic
1. Try Redis GET "prompt:{name}:active"
2. If cache hit → deserialize and return
3. If cache miss → query PostgreSQL (active=true)
4. Write to Redis with TTL
5. Return prompt
```

#### Integration with Existing Services

**Before (Hardcoded)**:
```csharp
// RagService.cs (line 111)
var systemPrompt = @"You are a board game rules assistant...";
```

**After (Database-Driven)**:
```csharp
// RagService.cs
private readonly IPromptTemplateService _promptService;

public async Task<QaResponse> AskAsync(string gameId, string query, ...)
{
    var systemPrompt = await _promptService.GetActivePromptAsync(
        "qa-system-prompt",
        cancellationToken
    );
    // ... rest of logic
}
```

#### Verdict
✅ **Recommended** - Optimal balance of performance, reliability, and maintainability.

---

### Option 3: In-Memory Cache with Background Refresh

#### Description
Services load prompts into in-memory cache on startup, background service polls database every 60 seconds for updates.

#### Architecture Diagram (Text)
```
Startup → BackgroundService (every 60s) → EF Core → PostgreSQL
                                          ↓
                                    IMemoryCache (in-process)
                                          ↓
Request → Service → IMemoryCache.Get("qa-system-prompt")
```json
#### Technology Choices
- **Cache**: `IMemoryCache` (ASP.NET Core built-in)
- **Background Service**: `BackgroundService` polling every 60s
- **Data Access**: Entity Framework Core 9.0

#### Pros
- **No External Dependency**: No Redis required
- **Low Latency**: < 1ms retrieval (in-memory)
- **Simple**: Standard ASP.NET Core patterns

#### Cons
- **Delayed Updates**: Up to 60s delay for prompt changes to take effect
- **Scalability Issue**: Each API instance has separate cache (inconsistent state during deployments)
- **Memory Per Instance**: Each pod/container duplicates prompt cache
- **Polling Overhead**: Database query every 60s per instance (wasteful)

#### Cost Implications
- **Infrastructure**: $0
- **Latency**: Unacceptable for prompt updates (60s delay)

#### Verdict
❌ **Not Recommended** - Delayed updates violate zero-downtime requirement.

---

## Recommended Architecture

**Selected**: **Option 2 - Redis-Cached Service Layer**

### Architecture Rationale

Option 2 provides the best balance across all critical dimensions:

| Criterion | Option 1 | Option 2 | Option 3 |
|-----------|----------|----------|----------|
| **Performance** | ❌ Poor (20-50ms) | ✅ Excellent (< 10ms) | ✅ Excellent (< 1ms) |
| **Scalability** | ❌ Poor (DB bottleneck) | ✅ Excellent (Redis) | ⚠️ Medium (memory per instance) |
| **Update Speed** | ✅ Instant | ✅ Instant | ❌ Delayed (60s) |
| **Consistency** | ✅ Perfect | ✅ Eventual (1-2s) | ❌ Poor (multi-instance) |
| **Reliability** | ✅ Simple | ✅ High (fallback) | ⚠️ Medium |
| **Complexity** | ✅ Low | ⚠️ Medium | ⚠️ Medium |
| **Cost** | ✅ $0 | ✅ $0 | ✅ $0 |

**Why Option 2 Wins**:
1. **Performance**: Meets < 10ms latency requirement (NFR-5)
2. **Zero Downtime**: Instant updates via cache invalidation (NFR-11)
3. **Scalability**: Handles 100K+ req/day (NFR-2)
4. **Existing Infrastructure**: Leverages production-ready Redis
5. **Reliability**: Fallback to DB prevents cache-related outages

### Detailed Component Design

#### 1. Backend Service Layer

**File**: `apps/api/src/Api/Services/IPromptTemplateService.cs`

```csharp
namespace Api.Services;

/// <summary>
/// Service for managing prompt templates with Redis caching.
/// </summary>
public interface IPromptTemplateService
{
    // === RETRIEVAL (High-Frequency) ===

    /// <summary>
    /// Get the active prompt content for a template (cached).
    /// Returns null if no active version exists.
    /// </summary>
    Task<string?> GetActivePromptAsync(string templateName, CancellationToken ct = default);

    /// <summary>
    /// Get the active prompt version entity (cached, for metadata access).
    /// </summary>
    Task<PromptVersionEntity?> GetActiveVersionAsync(string templateName, CancellationToken ct = default);

    // === ADMIN OPERATIONS (Low-Frequency) ===

    /// <summary>
    /// List all prompt templates with pagination and filtering.
    /// </summary>
    Task<PromptTemplateListResponse> ListTemplatesAsync(
        string? category = null,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Get a single template with all versions.
    /// </summary>
    Task<PromptTemplateDto?> GetTemplateAsync(string templateId, CancellationToken ct = default);

    /// <summary>
    /// Create a new prompt template with initial version.
    /// </summary>
    Task<CreatePromptTemplateResponse> CreateTemplateAsync(
        CreatePromptTemplateRequest request,
        string createdByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Create a new version for an existing template.
    /// </summary>
    Task<PromptVersionDto> CreateVersionAsync(
        string templateId,
        CreatePromptVersionRequest request,
        string createdByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Activate a specific version (deactivates others, invalidates cache).
    /// </summary>
    Task<PromptVersionDto> ActivateVersionAsync(
        string templateId,
        string versionId,
        string activatedByUserId,
        string? reason = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get version history for a template.
    /// </summary>
    Task<PromptVersionHistoryResponse> GetVersionHistoryAsync(
        string templateId,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Get audit log for a template.
    /// </summary>
    Task<PromptAuditLogResponse> GetAuditLogAsync(
        string templateId,
        int skip = 0,
        int take = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Update template metadata (name, description, category).
    /// </summary>
    Task<PromptTemplateDto> UpdateTemplateAsync(
        string templateId,
        string? description,
        string? category,
        string updatedByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Delete a template and all its versions (soft delete or hard delete based on policy).
    /// </summary>
    Task DeleteTemplateAsync(string templateId, string deletedByUserId, CancellationToken ct = default);

    // === CACHE MANAGEMENT ===

    /// <summary>
    /// Invalidate cache for a specific template (called after activation/deactivation).
    /// </summary>
    Task InvalidateCacheAsync(string templateName, CancellationToken ct = default);

    /// <summary>
    /// Warm cache for critical prompts (called on startup).
    /// </summary>
    Task WarmCacheAsync(CancellationToken ct = default);
}
```

**Implementation Pseudocode**:

```csharp
public class PromptTemplateService : IPromptTemplateService
{
    private readonly MeepleAiDbContext _context;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<PromptTemplateService> _logger;
    private const string CacheKeyPrefix = "prompt:";
    private const int CacheTtlSeconds = 3600; // 1 hour

    public async Task<string?> GetActivePromptAsync(string templateName, CancellationToken ct)
    {
        // Step 1: Try Redis cache
        var cacheKey = $"{CacheKeyPrefix}{templateName}:active";
        var db = _redis.GetDatabase();
        var cachedJson = await db.StringGetAsync(cacheKey);

        if (cachedJson.HasValue)
        {
            var cached = JsonSerializer.Deserialize<PromptVersionEntity>(cachedJson);
            return cached?.Content;
        }

        // Step 2: Cache miss - query database
        var template = await _context.PromptTemplates
            .Include(t => t.Versions.Where(v => v.IsActive))
            .FirstOrDefaultAsync(t => t.Name == templateName, ct);

        var activeVersion = template?.Versions.FirstOrDefault();
        if (activeVersion == null)
        {
            _logger.LogWarning("No active version found for template: {TemplateName}", templateName);
            return null;
        }

        // Step 3: Populate cache
        var json = JsonSerializer.Serialize(activeVersion);
        await db.StringSetAsync(cacheKey, json, TimeSpan.FromSeconds(CacheTtlSeconds));

        return activeVersion.Content;
    }

    public async Task<PromptVersionDto> ActivateVersionAsync(
        string templateId,
        string versionId,
        string activatedByUserId,
        string? reason,
        CancellationToken ct)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);

        try
        {
            // Step 1: Validate version exists
            var version = await _context.PromptVersions
                .Include(v => v.Template)
                .FirstOrDefaultAsync(v => v.Id == versionId && v.TemplateId == templateId, ct);

            if (version == null)
                throw new InvalidOperationException($"Version {versionId} not found");

            // Step 2: Deactivate all other versions for this template
            var otherVersions = await _context.PromptVersions
                .Where(v => v.TemplateId == templateId && v.IsActive)
                .ToListAsync(ct);

            foreach (var v in otherVersions)
                v.IsActive = false;

            // Step 3: Activate target version
            version.IsActive = true;

            // Step 4: Create audit log
            var auditLog = new PromptAuditLogEntity
            {
                Id = Guid.NewGuid().ToString(),
                TemplateId = templateId,
                VersionId = versionId,
                Action = "activate_version",
                ChangedByUserId = activatedByUserId,
                ChangedAt = DateTime.UtcNow,
                Details = reason ?? $"Activated version {version.VersionNumber}"
            };
            _context.PromptAuditLogs.Add(auditLog);

            // Step 5: Save changes
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // Step 6: Invalidate cache (CRITICAL - must happen after commit)
            await InvalidateCacheAsync(version.Template.Name, ct);

            _logger.LogInformation(
                "Activated version {VersionNumber} for template {TemplateName} by user {UserId}",
                version.VersionNumber, version.Template.Name, activatedByUserId);

            return MapToDto(version);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task InvalidateCacheAsync(string templateName, CancellationToken ct)
    {
        var cacheKey = $"{CacheKeyPrefix}{templateName}:active";
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(cacheKey);
        _logger.LogInformation("Invalidated cache for template: {TemplateName}", templateName);
    }
}
```

#### 2. Admin API Endpoints

**File**: `apps/api/src/Api/Program.cs` (add to v1Api group)

```csharp
// Admin: Prompt Management (Admin Only)
var promptsGroup = v1Api.MapGroup("/admin/prompts")
    .RequireAuthorization(policy => policy.RequireRole("admin"));

// List all templates
promptsGroup.MapGet("/", async (
    IPromptTemplateService promptService,
    string? category,
    int skip = 0,
    int take = 50,
    CancellationToken ct = default) =>
{
    var result = await promptService.ListTemplatesAsync(category, skip, take, ct);
    return Results.Ok(result);
}).WithName("ListPromptTemplates");

// Get single template
promptsGroup.MapGet("/{templateId}", async (
    string templateId,
    IPromptTemplateService promptService,
    CancellationToken ct) =>
{
    var template = await promptService.GetTemplateAsync(templateId, ct);
    return template != null ? Results.Ok(template) : Results.NotFound();
}).WithName("GetPromptTemplate");

// Create template
promptsGroup.MapPost("/", async (
    CreatePromptTemplateRequest request,
    IPromptTemplateService promptService,
    ClaimsPrincipal user,
    CancellationToken ct) =>
{
    var userId = user.FindFirst("UserId")?.Value
        ?? throw new UnauthorizedAccessException();
    var result = await promptService.CreateTemplateAsync(request, userId, ct);
    return Results.Created($"/api/v1/admin/prompts/{result.Template.Id}", result);
}).WithName("CreatePromptTemplate");

// Get version history
promptsGroup.MapGet("/{templateId}/versions", async (
    string templateId,
    IPromptTemplateService promptService,
    int skip = 0,
    int take = 50,
    CancellationToken ct = default) =>
{
    var result = await promptService.GetVersionHistoryAsync(templateId, skip, take, ct);
    return Results.Ok(result);
}).WithName("GetPromptVersionHistory");

// Create new version
promptsGroup.MapPost("/{templateId}/versions", async (
    string templateId,
    CreatePromptVersionRequest request,
    IPromptTemplateService promptService,
    ClaimsPrincipal user,
    CancellationToken ct) =>
{
    var userId = user.FindFirst("UserId")?.Value
        ?? throw new UnauthorizedAccessException();
    var version = await promptService.CreateVersionAsync(templateId, request, userId, ct);
    return Results.Created($"/api/v1/admin/prompts/{templateId}/versions/{version.Id}", version);
}).WithName("CreatePromptVersion");

// Activate version
promptsGroup.MapPost("/{templateId}/versions/{versionId}/activate", async (
    string templateId,
    string versionId,
    ActivatePromptVersionRequest request,
    IPromptTemplateService promptService,
    ClaimsPrincipal user,
    CancellationToken ct) =>
{
    var userId = user.FindFirst("UserId")?.Value
        ?? throw new UnauthorizedAccessException();
    var version = await promptService.ActivateVersionAsync(
        templateId, versionId, userId, request.Reason, ct);
    return Results.Ok(version);
}).WithName("ActivatePromptVersion");

// Get audit log
promptsGroup.MapGet("/{templateId}/audit", async (
    string templateId,
    IPromptTemplateService promptService,
    int skip = 0,
    int take = 100,
    CancellationToken ct = default) =>
{
    var result = await promptService.GetAuditLogAsync(templateId, skip, take, ct);
    return Results.Ok(result);
}).WithName("GetPromptAuditLog");

// Update template metadata
promptsGroup.MapPut("/{templateId}", async (
    string templateId,
    UpdatePromptTemplateRequest request,
    IPromptTemplateService promptService,
    ClaimsPrincipal user,
    CancellationToken ct) =>
{
    var userId = user.FindFirst("UserId")?.Value
        ?? throw new UnauthorizedAccessException();
    var template = await promptService.UpdateTemplateAsync(
        templateId, request.Description, request.Category, userId, ct);
    return Results.Ok(template);
}).WithName("UpdatePromptTemplate");

// Delete template
promptsGroup.MapDelete("/{templateId}", async (
    string templateId,
    IPromptTemplateService promptService,
    ClaimsPrincipal user,
    CancellationToken ct) =>
{
    var userId = user.FindFirst("UserId")?.Value
        ?? throw new UnauthorizedAccessException();
    await promptService.DeleteTemplateAsync(templateId, userId, ct);
    return Results.NoContent();
}).WithName("DeletePromptTemplate");
```

#### 3. Frontend Admin UI

**Page Structure**:
```
apps/web/src/pages/admin/prompts/
├── index.tsx              # List all templates (table view)
├── [id].tsx               # Template detail + version history
├── [id]/edit.tsx          # Edit template metadata
├── [id]/versions/new.tsx  # Create new version (Monaco editor)
├── [id]/versions/[versionId].tsx  # View version (readonly Monaco)
├── [id]/compare.tsx       # Side-by-side version comparison
└── new.tsx                # Create new template
```

**Example: Template List Page** (`pages/admin/prompts/index.tsx`)

```typescript
import { useState, useEffect } from 'react';
import { api } from '@/lib/api';
import type { PromptTemplateListResponse, PromptTemplateDto } from '@/types/prompts';

export default function PromptsListPage() {
  const [templates, setTemplates] = useState<PromptTemplateDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [category, setCategory] = useState<string | null>(null);

  useEffect(() => {
    async function loadTemplates() {
      try {
        setLoading(true);
        const params = new URLSearchParams();
        if (category) params.set('category', category);
        params.set('take', '50');

        const response = await api.get<PromptTemplateListResponse>(
          `/admin/prompts?${params.toString()}`
        );
        setTemplates(response.templates);
      } catch (err) {
        setError('Failed to load prompt templates');
        console.error(err);
      } finally {
        setLoading(false);
      }
    }

    loadTemplates();
  }, [category]);

  return (
    <div className="container mx-auto py-8">
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-3xl font-bold">Prompt Templates</h1>
        <a href="/admin/prompts/new" className="btn-primary">
          Create Template
        </a>
      </div>

      {/* Category Filter */}
      <div className="mb-4">
        <label className="mr-2">Filter by category:</label>
        <select
          value={category || ''}
          onChange={(e) => setCategory(e.target.value || null)}
          className="border rounded px-3 py-2"
        >
          <option value="">All</option>
          <option value="qa">Q&A</option>
          <option value="chess">Chess</option>
          <option value="setup">Setup Guide</option>
        </select>
      </div>

      {loading && <p>Loading templates...</p>}
      {error && <p className="text-red-600">{error}</p>}

      {!loading && templates.length === 0 && (
        <p className="text-gray-600">No templates found. Create one to get started.</p>
      )}

      {!loading && templates.length > 0 && (
        <table className="w-full border-collapse border">
          <thead>
            <tr className="bg-gray-100">
              <th className="border p-2 text-left">Name</th>
              <th className="border p-2 text-left">Category</th>
              <th className="border p-2 text-left">Description</th>
              <th className="border p-2 text-center">Versions</th>
              <th className="border p-2 text-center">Active Version</th>
              <th className="border p-2 text-left">Created</th>
              <th className="border p-2 text-center">Actions</th>
            </tr>
          </thead>
          <tbody>
            {templates.map((template) => (
              <tr key={template.id} className="hover:bg-gray-50">
                <td className="border p-2 font-mono text-sm">{template.name}</td>
                <td className="border p-2">
                  <span className="px-2 py-1 bg-blue-100 text-blue-800 rounded text-xs">
                    {template.category || 'N/A'}
                  </span>
                </td>
                <td className="border p-2 text-sm">{template.description || '-'}</td>
                <td className="border p-2 text-center">{template.versionCount}</td>
                <td className="border p-2 text-center">
                  {template.activeVersionNumber ? (
                    <span className="px-2 py-1 bg-green-100 text-green-800 rounded text-xs">
                      v{template.activeVersionNumber}
                    </span>
                  ) : (
                    <span className="text-gray-400">None</span>
                  )}
                </td>
                <td className="border p-2 text-sm text-gray-600">
                  {new Date(template.createdAt).toLocaleDateString()}
                </td>
                <td className="border p-2 text-center">
                  <a
                    href={`/admin/prompts/${template.id}`}
                    className="text-blue-600 hover:underline mr-2"
                  >
                    View
                  </a>
                  <a
                    href={`/admin/prompts/${template.id}/edit`}
                    className="text-green-600 hover:underline"
                  >
                    Edit
                  </a>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
```

**Example: Monaco Editor for Version Creation**

```typescript
import { useState } from 'react';
import Editor from '@monaco-editor/react';
import { api } from '@/lib/api';

export default function CreateVersionPage({ templateId }: { templateId: string }) {
  const [content, setContent] = useState('');
  const [metadata, setMetadata] = useState('');
  const [activateImmediately, setActivateImmediately] = useState(false);
  const [saving, setSaving] = useState(false);

  async function handleSave() {
    try {
      setSaving(true);
      await api.post(`/admin/prompts/${templateId}/versions`, {
        content,
        metadata: metadata || null,
        activateImmediately
      });
      alert('Version created successfully!');
      window.location.href = `/admin/prompts/${templateId}`;
    } catch (err) {
      alert('Failed to create version');
      console.error(err);
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="container mx-auto py-8">
      <h1 className="text-3xl font-bold mb-6">Create New Version</h1>

      <div className="mb-4">
        <label className="block mb-2 font-semibold">Prompt Content</label>
        <Editor
          height="400px"
          defaultLanguage="markdown"
          value={content}
          onChange={(value) => setContent(value || '')}
          theme="vs-dark"
          options={{
            minimap: { enabled: false },
            fontSize: 14,
            wordWrap: 'on'
          }}
        />
      </div>

      <div className="mb-4">
        <label className="block mb-2 font-semibold">
          Metadata (optional JSON)
        </label>
        <textarea
          className="w-full border rounded p-2 font-mono text-sm"
          rows={4}
          value={metadata}
          onChange={(e) => setMetadata(e.target.value)}
          placeholder='{"testResults": {...}}'
        />
      </div>

      <div className="mb-4">
        <label className="flex items-center">
          <input
            type="checkbox"
            checked={activateImmediately}
            onChange={(e) => setActivateImmediately(e.target.checked)}
            className="mr-2"
          />
          Activate this version immediately
        </label>
      </div>

      <div className="flex gap-4">
        <button
          onClick={handleSave}
          disabled={!content || saving}
          className="px-4 py-2 bg-blue-600 text-white rounded disabled:opacity-50"
        >
          {saving ? 'Saving...' : 'Create Version'}
        </button>
        <a
          href={`/admin/prompts/${templateId}`}
          className="px-4 py-2 bg-gray-300 rounded"
        >
          Cancel
        </a>
      </div>
    </div>
  );
}
```

#### 4. Service Refactoring Pattern

**Migration Strategy**: Refactor each service to use `IPromptTemplateService` while maintaining backward compatibility via feature flag.

**Example: RagService Refactoring**

```csharp
public class RagService : IRagService
{
    private readonly IPromptTemplateService _promptService;
    private readonly ILlmService _llmService;
    private readonly IConfiguration _configuration;

    // Feature flag for gradual rollout
    private bool UsePromptDatabase =>
        _configuration.GetValue<bool>("Features:PromptDatabase", false);

    public async Task<QaResponse> AskAsync(
        string gameId,
        string query,
        CancellationToken ct)
    {
        // ... RAG retrieval logic ...

        // Fetch system prompt from database (with fallback)
        string systemPrompt;
        if (UsePromptDatabase)
        {
            systemPrompt = await _promptService.GetActivePromptAsync(
                "qa-system-prompt", ct)
                ?? GetFallbackPrompt();
        }
        else
        {
            systemPrompt = GetFallbackPrompt();
        }

        // ... rest of logic ...
    }

    private static string GetFallbackPrompt()
    {
        // Hardcoded fallback (same as current implementation)
        return @"You are a board game rules assistant...";
    }
}
```

**appsettings.json Configuration**:
```json
{
  "Features": {
    "PromptDatabase": false  // Set to true after migration
  }
}
```sql
---

## Risk Assessment

### Technical Risks

#### TR-1: Cache Consistency Issues
**Risk**: Short window between DB commit and cache invalidation where users might get stale prompt
**Likelihood**: Medium
**Impact**: Low (1-2 second inconsistency)
**Mitigation**:
- Use database transaction before cache invalidation
- Add cache TTL (1 hour) as safety net
- Monitor cache hit/miss rates
- Log cache invalidation operations

#### TR-2: Redis Unavailability
**Risk**: Redis outage prevents prompt retrieval, causing LLM requests to fail
**Likelihood**: Low (Redis 99.9% uptime)
**Impact**: High (service disruption)
**Mitigation**:
- Implement fallback to PostgreSQL query
- Add circuit breaker pattern
- Monitor Redis health via health checks
- Alert on cache miss rate spike

**Code Implementation**:
```csharp
public async Task<string?> GetActivePromptAsync(string templateName, CancellationToken ct)
{
    try
    {
        // Try Redis
        var cached = await TryGetFromCacheAsync(templateName);
        if (cached != null) return cached;
    }
    catch (RedisException ex)
    {
        _logger.LogWarning(ex, "Redis unavailable, falling back to database");
        MeepleAiMetrics.RecordCacheFallback("prompt", "redis_unavailable");
    }

    // Fallback to database
    return await GetFromDatabaseAsync(templateName, ct);
}
```

#### TR-3: Prompt Quality Regression
**Risk**: Admin activates prompt that degrades response quality (hallucinations, incorrect answers)
**Likelihood**: High (human error inevitable)
**Impact**: Critical (poor user experience)
**Mitigation**:
- **CRITICAL**: Implement prompt testing framework (see separate document)
- Require test pass before activation (optional but recommended)
- Store test results in version metadata
- Enable rollback to previous version (< 30 seconds)
- Monitor AI request logs for confidence score drops

**Prompt Testing Framework** (High-Level):
```typescript
// Test workflow
1. Admin creates new version
2. Admin clicks "Run Tests" button
3. System runs version against test dataset
4. System displays metrics: accuracy, hallucination rate, confidence, latency
5. If pass thresholds → enable "Activate" button
6. If fail → display failing queries, suggest revisions
```sql
#### TR-4: Database Migration Dependency
**Risk**: Prompt tables assumed to exist, but some environments may not have migration applied
**Likelihood**: Low (schema already deployed)
**Impact**: High (service crash)
**Mitigation**:
- Verify migration existence in deployment scripts
- Add existence check in `PromptTemplateService` constructor
- Graceful degradation: fallback to hardcoded if tables missing
- CI/CD validation step

### Operational Risks

#### OR-1: Unauthorized Prompt Modifications
**Risk**: Non-admin user gains access to prompt endpoints, modifies critical prompts
**Likelihood**: Low (role-based authorization)
**Impact**: Critical (malicious prompts)
**Mitigation**:
- Enforce admin role check via `[Authorize(Roles = "admin")]`
- Audit all modifications (prompt_audit_logs)
- Review audit logs weekly
- Add anomaly detection for rapid prompt changes

#### OR-2: Accidental Prompt Deletion
**Risk**: Admin deletes critical prompt template, breaking live service
**Likelihood**: Medium (UI allows deletion)
**Impact**: High (service disruption)
**Mitigation**:
- Implement soft delete (mark as deleted, retain data)
- Add confirmation dialog: "Are you sure? This template is used by [services]"
- Prevent deletion if active version exists
- Daily backup of prompt_templates table

#### OR-3: Performance Degradation from Cache Misses
**Risk**: Cache invalidation frequency too high, causing DB load spike
**Likelihood**: Low (prompts updated < 10x/day)
**Impact**: Medium (increased latency)
**Mitigation**:
- Monitor cache hit rate (target: > 95%)
- Alert on sustained cache miss rate > 10%
- Implement cache warming on deployment
- Add rate limiting for activation endpoint (max 10/minute)

### Success Metrics

#### Metric 1: Prompt Retrieval Latency
- **Target**: < 10ms (p95)
- **Measurement**: OpenTelemetry histogram
- **Alert**: p95 > 20ms for 5 minutes

#### Metric 2: Cache Hit Rate
- **Target**: > 95%
- **Measurement**: Redis hits / (hits + misses)
- **Alert**: < 90% for 10 minutes

#### Metric 3: Prompt Activation Time
- **Target**: < 500ms (DB update + cache invalidation)
- **Measurement**: Endpoint latency
- **Alert**: > 1000ms

#### Metric 4: Test Coverage
- **Target**: 100% of prompts have test datasets
- **Measurement**: Count templates with test metadata
- **Alert**: Manual review

#### Metric 5: Quality Regression Detection
- **Target**: 0 undetected quality drops in production
- **Measurement**: AI request log confidence score trends
- **Alert**: Confidence drop > 10% week-over-week

---

## Implementation Phases

### Phase 1: Core Infrastructure (Week 1-2)

**Goal**: Implement backend service layer with Redis caching

**Tasks**:
1. Create `IPromptTemplateService` interface
2. Implement `PromptTemplateService` with Redis caching
3. Add dependency injection in `Program.cs`
4. Create admin API endpoints (8 endpoints)
5. Unit tests (90% coverage target)
6. Integration tests with Testcontainers

**Deliverables**:
- Working API endpoints
- 25+ unit tests
- 10+ integration tests
- API documentation (Swagger)

**Testing Focus**:
- Cache hit/miss scenarios
- Cache invalidation correctness
- Concurrent activation handling
- Fallback to database
- Authorization enforcement

---

### Phase 2: Admin UI (Week 2-3)

**Goal**: Build React admin pages for prompt management

**Tasks**:
1. Create 7 pages (list, detail, edit, create, version compare)
2. Integrate Monaco editor for version creation
3. Add side-by-side diff viewer for version comparison
4. Implement loading states and error handling
5. Jest unit tests for UI components
6. Playwright E2E tests for critical flows

**Deliverables**:
- 7 working admin pages
- 90% component test coverage
- 5+ E2E test scenarios

**Testing Focus**:
- Form validation
- Monaco editor integration
- Version activation flow
- Error handling (network failures)
- Authentication gate

---

### Phase 3: Service Migration (Week 3-4)

**Goal**: Refactor 4 services to use database prompts

**Tasks**:
1. Refactor `RagService` (Q&A prompt)
2. Refactor `StreamingQaService` (streaming Q&A prompt)
3. Refactor `ChessAgentService` (chess prompt)
4. Refactor `SetupGuideService` (setup prompt)
5. Add feature flags for gradual rollout
6. Migrate hardcoded prompts to database (seed data)
7. Update service tests to mock `IPromptTemplateService`

**Deliverables**:
- 4 refactored services
- Feature flag configuration
- Database seed script
- Updated tests (no behavior change)

**Testing Focus**:
- Feature flag toggle (on/off)
- Fallback logic correctness
- No regression in LLM responses
- Performance (< 10ms overhead)

---

### Phase 4: Prompt Testing Framework (Week 4-5)

**Goal**: Build comprehensive prompt evaluation system

**Tasks**:
1. Design test dataset JSON schema
2. Create `IPromptEvaluationService` interface
3. Implement evaluation metrics:
   - Accuracy (vs. ground truth)
   - Hallucination detection
   - Confidence score distribution
   - Latency benchmarks
4. Build UI for running tests and viewing results
5. Integrate with CI/CD (run on migrations)
6. Create initial test datasets for 4 prompts

**Deliverables**:
- `PromptEvaluationService` implementation
- Test dataset format documentation
- 4 test datasets (50+ queries)
- Evaluation report UI
- CI integration

**Testing Focus**:
- Metric calculation accuracy
- Test dataset loading
- Report generation
- A/B comparison logic

**See**: `docs/technic/admin-prompt-testing-framework.md` for detailed design

---

### Phase 5: Deployment & Monitoring (Week 5-6)

**Goal**: Deploy to production with monitoring

**Tasks**:
1. Create OpenTelemetry metrics for prompt operations
2. Add Grafana dashboards (cache hit rate, latency, activation frequency)
3. Configure alerts (cache miss rate, latency spikes)
4. Create runbook for prompt-related incidents
5. Conduct admin training session
6. Gradual rollout: enable feature flags per service
7. Monitor for 1 week, rollback if issues

**Deliverables**:
- Production deployment
- 3 Grafana dashboards
- 5 alert rules
- Runbook documentation
- Training materials

**Monitoring Focus**:
- Cache performance
- Database query latency
- Prompt activation frequency
- Error rates
- AI request log confidence trends

---

## Appendices

### Appendix A: Database Schema Reference

**Existing Tables** (already migrated):

```sql
-- Prompt Templates
CREATE TABLE prompt_templates (
    id VARCHAR(64) PRIMARY KEY,
    name VARCHAR(128) UNIQUE NOT NULL,
    description VARCHAR(512),
    category VARCHAR(64),
    created_by_user_id VARCHAR(64) NOT NULL,
    created_at TIMESTAMP NOT NULL,
    FOREIGN KEY (created_by_user_id) REFERENCES users(id)
);
CREATE INDEX idx_prompt_templates_category ON prompt_templates(category);
CREATE INDEX idx_prompt_templates_created_at ON prompt_templates(created_at);

-- Prompt Versions
CREATE TABLE prompt_versions (
    id VARCHAR(64) PRIMARY KEY,
    template_id VARCHAR(64) NOT NULL,
    version_number INT NOT NULL,
    content TEXT NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT FALSE,
    created_by_user_id VARCHAR(64) NOT NULL,
    created_at TIMESTAMP NOT NULL,
    metadata VARCHAR(4096),
    FOREIGN KEY (template_id) REFERENCES prompt_templates(id) ON DELETE CASCADE,
    FOREIGN KEY (created_by_user_id) REFERENCES users(id),
    UNIQUE (template_id, version_number)
);
CREATE INDEX idx_prompt_versions_template_active ON prompt_versions(template_id, is_active);
CREATE INDEX idx_prompt_versions_created_at ON prompt_versions(created_at);

-- Prompt Audit Logs
CREATE TABLE prompt_audit_logs (
    id VARCHAR(64) PRIMARY KEY,
    template_id VARCHAR(64) NOT NULL,
    version_id VARCHAR(64),
    action VARCHAR(64) NOT NULL,
    changed_by_user_id VARCHAR(64) NOT NULL,
    changed_at TIMESTAMP NOT NULL,
    details VARCHAR(2048),
    FOREIGN KEY (template_id) REFERENCES prompt_templates(id) ON DELETE CASCADE,
    FOREIGN KEY (version_id) REFERENCES prompt_versions(id) ON DELETE SET NULL,
    FOREIGN KEY (changed_by_user_id) REFERENCES users(id)
);
CREATE INDEX idx_prompt_audit_logs_template_id ON prompt_audit_logs(template_id);
CREATE INDEX idx_prompt_audit_logs_version_id ON prompt_audit_logs(version_id);
CREATE INDEX idx_prompt_audit_logs_changed_at ON prompt_audit_logs(changed_at);
CREATE INDEX idx_prompt_audit_logs_action ON prompt_audit_logs(action);
```

### Appendix B: Redis Cache Key Conventions

```
# Active Prompt Cache
Key: "prompt:{templateName}:active"
Value: JSON-serialized PromptVersionEntity
TTL: 3600 seconds (1 hour)
Example: "prompt:qa-system-prompt:active"

# Cache Statistics
Key: "prompt:stats:hits"
Value: Counter (incremented on cache hit)

Key: "prompt:stats:misses"
Value: Counter (incremented on cache miss)

# Invalidation Events
Pub/Sub Channel: "prompt:invalidation"
Message: {"templateName": "qa-system-prompt", "timestamp": "2025-10-18T10:00:00Z"}
```

### Appendix C: Configuration Reference

**appsettings.json**:
```json
{
  "Features": {
    "PromptDatabase": false  // Feature flag for prompt DB usage
  },
  "PromptManagement": {
    "CacheTtlSeconds": 3600,
    "MaxPromptSizeBytes": 16384,  // 16 KB
    "EnableAutomaticCacheWarming": true,
    "CriticalPrompts": [
      "qa-system-prompt",
      "streaming-qa-system-prompt",
      "chess-system-prompt",
      "setup-guide-system-prompt"
    ]
  },
  "ConnectionStrings": {
    "Redis": "redis:6379",  // Existing
    "Postgres": "Host=postgres;Database=meepleai;..."  // Existing
  }
}
```

### Appendix D: Migration Seed Data

**SQL Script**: `apps/api/src/Api/Migrations/Seeds/SeedPromptTemplates.sql`

```sql
-- Seed Q&A System Prompt
INSERT INTO prompt_templates (id, name, description, category, created_by_user_id, created_at)
VALUES (
    'prompt-qa-001',
    'qa-system-prompt',
    'System prompt for board game Q&A agent',
    'qa',
    'admin@meepleai.dev',
    NOW()
);

INSERT INTO prompt_versions (id, template_id, version_number, content, is_active, created_by_user_id, created_at)
VALUES (
    'prompt-qa-001-v1',
    'prompt-qa-001',
    1,
    'You are a board game rules assistant. Your job is to answer questions about board game rules based ONLY on the provided context from the rulebook.

CRITICAL INSTRUCTIONS:
- If the answer to the question is clearly found in the provided context, answer it concisely and accurately.
- If the answer is NOT in the provided context or you''re uncertain, respond with EXACTLY: "Not specified"
- Do NOT make assumptions or use external knowledge about the game.
- Do NOT hallucinate or invent information.
- Keep your answers brief and to the point (2-3 sentences maximum).
- Reference page numbers when relevant.',
    TRUE,
    'admin@meepleai.dev',
    NOW()
);

-- Repeat for other 3 prompts (chess, setup-guide, streaming-qa)
-- ...
```

### Appendix E: Testing Strategy Summary

| Test Type | Count | Coverage | Tools |
|-----------|-------|----------|-------|
| **Backend Unit** | 25+ | 90% | xUnit, Moq, SQLite |
| **Backend Integration** | 10+ | Key flows | xUnit, Testcontainers |
| **Frontend Unit** | 20+ | 90% | Jest, React Testing Library |
| **Frontend E2E** | 5+ | Critical paths | Playwright |
| **Prompt Evaluation** | 10+ | All prompts | Custom framework |
| **Load Tests** | 3 | Cache/DB | K6 or similar |

**Total Estimated Tests**: 70+

---

## Document Approval

- [ ] Architecture Review: _____________________
- [ ] Security Review: _____________________
- [ ] DevOps Review: _____________________
- [ ] Product Owner Approval: _____________________

**Next Steps**:
1. Review and approve this architecture document
2. Create GitHub issue (ADMIN-01) with implementation tasks
3. Review prompt testing framework design (separate document)
4. Begin Phase 1 implementation

---

**Document History**:
- v1.0 (2025-10-18): Initial architecture design
