#!/bin/bash
# Script to create GitHub issues for API improvements
# Generated: 2025-11-15
# Source: API Analysis Report

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}================================================${NC}"
echo -e "${BLUE}  MeepleAI - API Improvements Issue Creator${NC}"
echo -e "${BLUE}================================================${NC}"
echo ""

# Check if gh is installed
if ! command -v gh &> /dev/null; then
    echo -e "${RED}Error: GitHub CLI (gh) is not installed${NC}"
    echo "Install it from: https://cli.github.com/"
    exit 1
fi

# Check if authenticated
if ! gh auth status &> /dev/null; then
    echo -e "${RED}Error: Not authenticated with GitHub CLI${NC}"
    echo "Run: gh auth login"
    exit 1
fi

echo -e "${GREEN}✓ GitHub CLI is installed and authenticated${NC}"
echo ""

# Function to create label if it doesn't exist
create_label() {
    local name=$1
    local description=$2
    local color=$3

    if gh label list --json name --jq '.[].name' | grep -q "^${name}$"; then
        echo -e "${YELLOW}  Label '${name}' already exists${NC}"
    else
        gh label create "$name" --description "$description" --color "$color"
        echo -e "${GREEN}  ✓ Created label: ${name}${NC}"
    fi
}

# Create labels
echo -e "${BLUE}Creating labels...${NC}"
create_label "ddd" "Domain-Driven Design architecture" "0366d6"
create_label "cqrs" "Command Query Responsibility Segregation" "0366d6"
create_label "legacy-code" "Legacy code to be refactored or removed" "d93f0b"
create_label "streaming" "Streaming/async operations" "fbca04"
create_label "domain-events" "Domain event implementation" "0e8a16"
create_label "rag" "RAG (Retrieval Augmented Generation)" "5319e7"
echo ""

# Function to create issue
create_issue() {
    local title=$1
    local body=$2
    local labels=$3
    local milestone=$4

    echo -e "${BLUE}Creating issue: ${title}${NC}"

    # Create issue and capture URL
    issue_url=$(echo "$body" | gh issue create \
        --title "$title" \
        --body-file - \
        --label "$labels" \
        ${milestone:+--milestone "$milestone"})

    if [ $? -eq 0 ]; then
        echo -e "${GREEN}  ✓ Created: ${issue_url}${NC}"
    else
        echo -e "${RED}  ✗ Failed to create issue${NC}"
    fi
    echo ""
}

# ========================================
# PHASE 1 - CRITICAL
# ========================================

echo -e "${RED}================================================${NC}"
echo -e "${RED}  PHASE 1 - CRITICAL (P0)${NC}"
echo -e "${RED}================================================${NC}"
echo ""

create_issue "[P0] Fix Deadlock Risk in RateLimitService" "## Problem

Critical deadlock risk in \`RateLimitService.GetConfigForRole()\` method due to blocking async calls using \`.Result\`.

## Location

- **File**: \`apps/api/src/Api/Services/RateLimitService.cs\`
- **Lines**: 160-161

## Current Code (BROKEN)

\`\`\`csharp
// BLOCKING ASYNC CALLS - CAUSES DEADLOCK!
var maxTokens = GetRateLimitValueAsync<int>(\"MaxTokens\", normalizedRole).Result;
var refillRate = GetRateLimitValueAsync<double>(\"RefillRate\", normalizedRole).Result;
\`\`\`

## Impact

- ⚠️ Thread pool starvation in ASP.NET Core
- ⚠️ Potential deadlocks under load
- ⚠️ Affects ALL rate-limited endpoints (auth, 2FA, OAuth)

## Solution

Make \`GetConfigForRole()\` async and use proper async/await:

\`\`\`csharp
public async Task<RateLimitConfig> GetConfigForRoleAsync(string? role, CancellationToken ct = default)
{
    if (_configService is null)
    {
        return GetConfigFromHardcodedDefaults(role);
    }

    var normalizedRole = role?.ToLowerInvariant() ?? \"anonymous\";

    var maxTokens = await GetRateLimitValueAsync<int>(\"MaxTokens\", normalizedRole, ct);
    var refillRate = await GetRateLimitValueAsync<double>(\"RefillRate\", normalizedRole, ct);

    return new RateLimitConfig(maxTokens, refillRate);
}
\`\`\`

## Files to Update

- \`RateLimitService.cs\` - Make method async
- \`AuthEndpoints.cs:347, 527-528, 579-580\` - Update all callers

## Testing Checklist

- [ ] Unit tests for async behavior
- [ ] Load testing under 100+ concurrent requests
- [ ] Verify no deadlocks with ThreadPool starvation simulation
- [ ] Performance regression testing

## Acceptance Criteria

- [ ] No blocking async calls (\`.Result\`, \`.Wait()\`)
- [ ] All rate limiting endpoints use async pattern
- [ ] Tests pass with 100+ concurrent requests
- [ ] No performance regression

## Estimated Time

⏱️ **2-3 hours**

## Priority

🔴 **P0 - CRITICAL** - Blocks production deployment" "bug,critical,performance,security" "Hotfix v1.0.1"

# ========================================
# PHASE 2 - HIGH PRIORITY
# ========================================

echo -e "${YELLOW}================================================${NC}"
echo -e "${YELLOW}  PHASE 2 - HIGH PRIORITY (P1)${NC}"
echo -e "${YELLOW}================================================${NC}"
echo ""

create_issue "[DDD] Migrate ChatService to CQRS Pattern" "## Overview

Migrate \`ChatService\` (431 lines) to DDD/CQRS pattern to complete KnowledgeBase bounded context migration (95% → 100%).

## Current State

- ❌ Service directly injected in 20+ endpoints
- ❌ Business logic in service layer instead of domain
- ❌ No separation between commands and queries

## Files Affected

- \`apps/api/src/Api/Services/ChatService.cs\` (431 lines - **TO BE REMOVED**)
- \`apps/api/src/Api/Routing/ChatEndpoints.cs\` (uses service at lines 18, 40, 45, 73, 90, 119, 126)
- \`apps/api/src/Api/Routing/AiEndpoints.cs\` (uses service at 14+ locations)

## Commands to Create

- [ ] \`CreateChatCommand\` + \`CreateChatCommandHandler\`
- [ ] \`AddChatMessageCommand\` + \`AddChatMessageCommandHandler\`
- [ ] \`UpdateChatMessageCommand\` + \`UpdateChatMessageCommandHandler\`
- [ ] \`DeleteChatCommand\` + \`DeleteChatCommandHandler\`
- [ ] \`DeleteChatMessageCommand\` + \`DeleteChatMessageCommandHandler\`

## Queries to Create

- [ ] \`GetChatByIdQuery\` + \`GetChatByIdQueryHandler\`
- [ ] \`GetUserChatsQuery\` + \`GetUserChatsQueryHandler\`
- [ ] \`GetUserChatsByGameQuery\` + \`GetUserChatsByGameQueryHandler\`
- [ ] \`GetChatHistoryQuery\` + \`GetChatHistoryQueryHandler\`

## Domain Services (keep in Domain layer)

- [ ] \`ChatMessageInvalidationDomainService\` (for \`InvalidateSubsequentMessagesAsync\`)

## Migration Steps

1. Create Commands/Queries in \`BoundedContexts/KnowledgeBase/Application/\`
2. Create Handlers with business logic
3. Update \`ChatEndpoints.cs\` to use \`IMediator.Send()\`
4. Update \`AiEndpoints.cs\` to use \`IMediator.Send()\`
5. Run all tests (ensure 90%+ coverage)
6. Remove \`ChatService.cs\`
7. Remove service registration from \`ApplicationServiceExtensions.cs:181\`

## Notes

- Some handlers may already exist (e.g., \`CreateChatThreadCommand\`) - verify and reuse
- Ensure audit logging is preserved in handlers
- Maintain cascade invalidation behavior for message edits

## Acceptance Criteria

- [ ] All 6 chat endpoints use MediatR pattern
- [ ] All 14 AI endpoints updated to use chat handlers
- [ ] ChatService.cs removed (431 lines eliminated)
- [ ] Tests pass (90%+ coverage maintained)
- [ ] No performance regression
- [ ] KnowledgeBase context at **100% completion**

## Estimated Time

⏱️ **12-16 hours**

## Priority

🟠 **P1 - High**" "architecture,ddd,cqrs,refactoring,legacy-code" "Sprint 24 - DDD 100%"

create_issue "[DDD] Migrate RuleSpecService to CQRS Pattern" "## Overview

Migrate \`RuleSpecService\` (575+ lines) to DDD/CQRS pattern for GameManagement bounded context.

## Current State

- ❌ Service directly injected in \`RuleSpecEndpoints.cs\` and \`PdfEndpoints.cs\`
- ❌ Complex business logic in service layer
- ❌ Version management, diff computation, export logic not in domain

## Files Affected

- \`apps/api/src/Api/Services/RuleSpecService.cs\` (575+ lines - **TO BE REMOVED**)
- \`apps/api/src/Api/Routing/RuleSpecEndpoints.cs\` (lines 18, 24, 35, 48, 79, 91)
- \`apps/api/src/Api/Routing/PdfEndpoints.cs\` (lines 422, 436)

## Commands to Create

- [ ] \`UpdateRuleSpecCommand\` + Handler
- [ ] \`GenerateRuleSpecFromPdfCommand\` + Handler
- [ ] \`CreateRuleSpecVersionCommand\` + Handler
- [ ] \`ExportRuleSpecsCommand\` + Handler (bulk ZIP export)

## Queries to Create

- [ ] \`GetRuleSpecQuery\` + Handler
- [ ] \`GetRuleSpecVersionQuery\` + Handler
- [ ] \`GetVersionHistoryQuery\` + Handler
- [ ] \`GetVersionTimelineQuery\` + Handler

## Domain Services (move to Domain layer)

- [ ] \`RuleSpecVersioningDomainService\`
- [ ] \`RuleAtomParsingDomainService\` (for \`ParseAtomicRules\`, \`ParseExtractedText\`)

## Migration Steps

1. Create bounded context structure: \`BoundedContexts/GameManagement/RuleSpec/\`
2. Move domain logic to aggregate roots and value objects
3. Create Commands/Queries with handlers
4. Update \`RuleSpecEndpoints.cs\` to use \`IMediator\`
5. Update \`PdfEndpoints.cs\` to use handlers for RuleSpec generation
6. Run tests
7. Remove \`RuleSpecService.cs\`

## Acceptance Criteria

- [ ] All RuleSpec endpoints use MediatR
- [ ] Version management in domain layer
- [ ] RuleSpecService.cs removed (575+ lines eliminated)
- [ ] Tests pass with coverage ≥90%
- [ ] ZIP export functionality preserved

## Estimated Time

⏱️ **10-14 hours**

## Priority

🟠 **P1 - High**" "architecture,ddd,cqrs,refactoring,legacy-code" "Sprint 24 - DDD 100%"

create_issue "[DDD] Implement Streaming Query Handlers for RAG/QA" "## Overview

Migrate streaming RAG/QA services to CQRS pattern using \`IAsyncEnumerable<T>\` handlers.

## Current State

- ❌ \`IStreamingRagService\`, \`IStreamingQaService\`, \`SetupGuideService\` injected directly
- ❌ Streaming operations bypass CQRS architecture
- ❌ Business logic scattered across service layer

## Files Affected

- \`apps/api/src/Api/Services/StreamingRagService.cs\` (**TO BE REMOVED**)
- \`apps/api/src/Api/Services/StreamingQaService.cs\` (**TO BE REMOVED**)
- \`apps/api/src/Api/Services/SetupGuideService.cs\` (456 lines - **TO BE REMOVED**)
- \`apps/api/src/Api/Routing/AiEndpoints.cs\` (lines 404, 475, 781)

## Queries to Create

- [ ] \`StreamExplainQuery\` + \`StreamExplainQueryHandler : IStreamRequestHandler<StreamExplainQuery, string>\`
- [ ] \`StreamQaQuery\` + \`StreamQaQueryHandler : IStreamRequestHandler<StreamQaQuery, string>\`
- [ ] \`GenerateSetupGuideQuery\` + Handler (may use streaming or single response)

## Handler Pattern

\`\`\`csharp
public class StreamExplainQueryHandler : IStreamRequestHandler<StreamExplainQuery, string>
{
    public async IAsyncEnumerable<string> Handle(
        StreamExplainQuery request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Use RAG pipeline
        await foreach (var chunk in _ragService.StreamResponseAsync(request.Question, ct))
        {
            yield return chunk;
        }
    }
}
\`\`\`

## Endpoint Update Example

\`\`\`csharp
// Before:
var stream = await streamingRagService.ExplainAsync(...);

// After:
var query = new StreamExplainQuery(...);
var stream = mediator.CreateStream(query, ct);
\`\`\`

## Migration Steps

1. Create streaming query handlers in \`KnowledgeBase/Application/Queries/\`
2. Update \`AiEndpoints.cs\` to use \`IMediator.CreateStream()\`
3. Ensure SSE (Server-Sent Events) still works
4. Test streaming under load
5. Remove streaming services
6. Update service registrations

## Acceptance Criteria

- [ ] All streaming endpoints use MediatR pattern
- [ ] SSE streaming works correctly
- [ ] No memory leaks with long-running streams
- [ ] Cancellation works properly
- [ ] Services removed

## Related

- Completes KnowledgeBase context: **95% → 100%**

## Estimated Time

⏱️ **8-12 hours**

## Priority

🟠 **P1 - High**" "architecture,ddd,cqrs,streaming,rag" "Sprint 25"

create_issue "[Config] Replace Hardcoded Configuration Values" "## Overview

Replace hardcoded configuration values with dynamic configuration using \`IConfigurationService\`.

## Locations to Fix

### 1. Session Expiration (\`AuthEndpoints.cs:149\`)

\`\`\`csharp
// Current (hardcoded):
var expiresAt = DateTime.UtcNow.AddDays(30);

// Should be:
var sessionDays = await _configService.GetValueAsync<int>(\"Authentication:SessionDays\", 30);
var expiresAt = DateTime.UtcNow.AddDays(sessionDays);
\`\`\`

### 2. OAuth Rate Limits (\`AuthEndpoints.cs:527-528, 579-580\`)

\`\`\`csharp
// Current (hardcoded):
new RateLimitConfig { MaxTokens = 10, RefillRate = 0.16667 }

// Should be:
var config = await _rateLimitService.GetConfigForRoleAsync(\"oauth_callback\");
\`\`\`

### 3. AI Agent Timeouts (\`AiEndpoints.cs\`)

- Identify hardcoded timeout values
- Move to \`appsettings.json\` under \`AIAgents:DefaultTimeout\`

### 4. PDF Max File Size (\`PdfEndpoints.cs\`)

\`\`\`csharp
// Current (likely hardcoded):
const int MaxFileSize = 10485760; // 10MB

// Should be:
var maxFileSize = await _configService.GetValueAsync<int>(\"PdfProcessing:MaxFileSizeMB\", 10) * 1024 * 1024;
\`\`\`

## Configuration Schema (add to DB)

\`\`\`sql
INSERT INTO system_configurations (key, value, category, description) VALUES
('Authentication.SessionDays', '30', 'Authentication', 'Session expiration in days'),
('Authentication.OAuth.RateLimit.MaxTokens', '10', 'RateLimit', 'OAuth callback rate limit burst'),
('Authentication.OAuth.RateLimit.RefillRate', '0.16667', 'RateLimit', 'OAuth callback refill rate'),
('AIAgents.DefaultTimeoutSeconds', '30', 'AI', 'Default timeout for AI agent calls'),
('PdfProcessing.MaxFileSizeMB', '10', 'PDF', 'Maximum PDF upload size in MB');
\`\`\`

## Acceptance Criteria

- [ ] No hardcoded magic numbers for timeouts/limits
- [ ] All values configurable via database
- [ ] Fallback to appsettings.json if DB unavailable
- [ ] Admin UI can modify values at runtime
- [ ] Tests updated with new configuration pattern

## Estimated Time

⏱️ **4-6 hours**

## Priority

🟠 **P1 - High**" "configuration,refactoring,tech-debt" "Sprint 24"

# ========================================
# PHASE 3 - MEDIUM PRIORITY
# ========================================

echo -e "${GREEN}================================================${NC}"
echo -e "${GREEN}  PHASE 3 - MEDIUM PRIORITY (P2)${NC}"
echo -e "${GREEN}================================================${NC}"
echo ""

create_issue "[DDD] Migrate Agent Services to CQRS Pattern" "## Overview

Migrate agent-related services to CQRS pattern in KnowledgeBase bounded context.

## Services to Migrate

- \`ChessAgentService.cs\`
- \`ChessKnowledgeService.cs\`
- \`FollowUpQuestionService.cs\`
- \`AgentFeedbackService.cs\`

## Files Affected

- \`apps/api/src/Api/Routing/AiEndpoints.cs\` (lines 982, 1005, 1038, 1097, 934)

## Existing Handlers (verify and use)

- ✓ \`CreateAgentCommand\` + Handler
- ✓ \`ConfigureAgentCommand\` + Handler
- ✓ \`InvokeAgentCommand\` + Handler

## New Commands to Create

- [ ] \`ProvideAgentFeedbackCommand\` + Handler
- [ ] \`InvokeChessAgentCommand\` + Handler (or extend InvokeAgentCommand)

## New Queries to Create

- [ ] \`GetChessKnowledgeQuery\` + Handler
- [ ] \`GenerateFollowUpQuestionsQuery\` + Handler

## Migration Steps

1. Verify existing agent handlers in \`KnowledgeBase/Application/\`
2. Create missing commands/queries
3. Update \`AiEndpoints.cs\` chess endpoints to use handlers
4. Update feedback endpoint to use command
5. Test agent functionality
6. Remove services

## Acceptance Criteria

- [ ] All agent endpoints use MediatR
- [ ] Chess agent logic preserved
- [ ] Feedback mechanism works
- [ ] Services removed

## Estimated Time

⏱️ **10-14 hours**

## Priority

🟡 **P2 - Medium**" "architecture,ddd,cqrs,agents,legacy-code" "Sprint 25"

create_issue "[DDD] Migrate RuleSpec Comment/Diff Services to CQRS" "## Overview

Migrate comment and diff services for RuleSpec to CQRS pattern.

## Services to Migrate

- \`RuleSpecCommentService.cs\`
- \`RuleSpecDiffService.cs\`
- \`RuleCommentService.cs\`

## Files Affected

- \`apps/api/src/Api/Routing/RuleSpecEndpoints.cs\` (10+ locations)

## Commands to Create

- [ ] \`CreateRuleCommentCommand\` + Handler
- [ ] \`ReplyToRuleCommentCommand\` + Handler
- [ ] \`UpdateRuleCommentCommand\` + Handler
- [ ] \`DeleteRuleCommentCommand\` + Handler
- [ ] \`ResolveRuleCommentCommand\` + Handler
- [ ] \`UnresolveRuleCommentCommand\` + Handler

## Queries to Create

- [ ] \`GetRuleCommentsQuery\` + Handler
- [ ] \`GetCommentsForLineQuery\` + Handler
- [ ] \`ComputeRuleSpecDiffQuery\` + Handler

## Domain Services (keep)

- [ ] \`RuleSpecDiffDomainService\` (diff computation algorithm)

## Acceptance Criteria

- [ ] All comment endpoints use MediatR
- [ ] Threaded comments work correctly
- [ ] Diff computation preserved
- [ ] Services removed

## Estimated Time

⏱️ **6-8 hours**

## Priority

🟡 **P2 - Medium**" "architecture,ddd,cqrs,refactoring" "Sprint 25"

create_issue "[DDD] Implement Domain Events for All Aggregates" "## Overview

Implement 42 TODO domain events across all bounded contexts.

## Current State

- ❌ 42 TODO comments for domain events
- ❌ Events defined but not published
- ❌ No event handlers implemented

## Distribution

- Authentication context: 8 domain events
- GameManagement context: 7 domain events
- KnowledgeBase context: 6 domain events
- WorkflowIntegration context: 2 domain events
- Others: 19 domain events

## Implementation Pattern

\`\`\`csharp
// ChatThread.cs example:
public static ChatThread Create(...)
{
    var thread = new ChatThread(...);
    thread.AddDomainEvent(new ChatThreadCreatedEvent(thread.Id, thread.GameId, thread.UserId));
    return thread;
}
\`\`\`

## Events to Implement

### Authentication (8 events)
- [ ] \`UserRegisteredEvent\`
- [ ] \`UserLoggedInEvent\`
- [ ] \`UserLoggedOutEvent\`
- [ ] \`PasswordChangedEvent\`
- [ ] \`TwoFactorEnabledEvent\`
- [ ] \`TwoFactorDisabledEvent\`
- [ ] \`ApiKeyCreatedEvent\`
- [ ] \`SessionExpiredEvent\`

### GameManagement (7 events)
- [ ] \`GameCreatedEvent\`
- [ ] \`GameUpdatedEvent\`
- [ ] \`GameSessionStartedEvent\`
- [ ] \`GameSessionPausedEvent\`
- [ ] \`GameSessionResumedEvent\`
- [ ] \`GameSessionCompletedEvent\`
- [ ] \`PlayerAddedToSessionEvent\`

### KnowledgeBase (6 events)
- [ ] \`ChatThreadCreatedEvent\`
- [ ] \`ChatThreadClosedEvent\`
- [ ] \`MessageAddedEvent\`
- [ ] \`AgentCreatedEvent\`
- [ ] \`AgentConfiguredEvent\`
- [ ] \`DocumentIndexedEvent\`

## Implementation Steps

1. Define events in \`BoundedContexts/{Context}/Domain/Events/\`
2. Publish events in aggregate methods using \`AddDomainEvent()\`
3. Create event handlers in \`Application/EventHandlers/\`
4. Wire up MediatR notifications
5. Add integration event publishers (if needed for cross-context communication)

## Acceptance Criteria

- [ ] All 42 TODO comments resolved
- [ ] Events published in aggregate methods
- [ ] Event handlers implemented
- [ ] Audit logging via events
- [ ] Tests for event publishing

## Benefits

- ✅ Audit trail completeness
- ✅ Cross-context communication
- ✅ Easier integration with external systems
- ✅ Event sourcing foundation

## Estimated Time

⏱️ **8-12 hours**

## Priority

🟡 **P2 - Medium**" "architecture,ddd,domain-events,event-sourcing" "Sprint 26"

create_issue "[Auth] Complete OAuth Callback Migration to CQRS" "## Overview

Complete OAuth migration by implementing \`HandleOAuthCallbackCommand\` handler.

## Current State

- ❌ \`AuthEndpoints.cs:564\` has TODO comment
- ❌ OAuth callback still uses \`OAuthService.HandleCallbackAsync()\`
- ❌ Token encryption logic in service instead of handler

## Files Affected

- \`apps/api/src/Api/Routing/AuthEndpoints.cs:564\`
- \`apps/api/src/Api/Services/OAuthService.cs\`

## Implementation

### Command

\`\`\`csharp
public record HandleOAuthCallbackCommand(
    string Provider,
    string Code,
    string State,
    string? IpAddress,
    string? UserAgent) : IRequest<OAuthCallbackResult>;
\`\`\`

### Handler

\`\`\`csharp
public class HandleOAuthCallbackCommandHandler : IRequestHandler<HandleOAuthCallbackCommand, OAuthCallbackResult>
{
    // Business logic:
    // 1. Validate state token
    // 2. Exchange code for access token
    // 3. Fetch user info from provider
    // 4. Create or link OAuth account
    // 5. Create session
    // 6. Encrypt and store tokens
}
\`\`\`

## Migration Steps

1. Create command in \`Authentication/Application/Commands/\`
2. Move business logic from \`OAuthService\` to handler
3. Update \`AuthEndpoints.cs:564\` to use \`IMediator.Send()\`
4. Keep \`OAuthService\` as infrastructure adapter (HTTP client for providers)
5. Test OAuth flow (Google, Discord, GitHub)
6. Remove TODO comment

## Acceptance Criteria

- [ ] \`HandleOAuthCallbackCommand\` implemented
- [ ] OAuth callback endpoint uses MediatR
- [ ] Token encryption in handler
- [ ] All OAuth providers work (Google, Discord, GitHub)
- [ ] Tests pass

## Estimated Time

⏱️ **6-8 hours**

## Priority

🟡 **P2 - Medium**" "architecture,authentication,oauth,ddd,cqrs" "Sprint 25"

# ========================================
# PHASE 4 - LOW PRIORITY
# ========================================

echo -e "${BLUE}================================================${NC}"
echo -e "${BLUE}  PHASE 4 - LOW PRIORITY (P3)${NC}"
echo -e "${BLUE}================================================${NC}"
echo ""

create_issue "[Performance] Add AsNoTracking to Read-Only Queries" "## Overview

Add \`.AsNoTracking()\` to read-only queries for 30% performance improvement (per PERF-05 ADR).

## Locations

### PdfEndpoints.cs

\`\`\`csharp
// Line 217-230: PDF text retrieval
var pdf = await _db.PdfDocuments
    .AsNoTracking()  // ← Add this
    .FirstOrDefaultAsync(p => p.Id == pdfId, ct);

// Line 318-326: PDF progress query
var progress = await _db.PdfDocuments
    .AsNoTracking()  // ← Add this
    .Select(p => new { p.ProcessingStatus, p.ProcessingProgress })
    .FirstOrDefaultAsync(p => p.Id == pdfId, ct);
\`\`\`

## Files to Audit

- Check all \`_dbContext.*\` queries in endpoints
- Verify handlers use \`.AsNoTracking()\` for queries
- Ensure commands DON'T use \`.AsNoTracking()\` (need tracking for updates)

## Testing

- [ ] Benchmark query performance before/after
- [ ] Verify 20-30% improvement on read-heavy endpoints
- [ ] Ensure no update failures (tracking still enabled for commands)

## Acceptance Criteria

- [ ] All read-only queries use \`.AsNoTracking()\`
- [ ] Performance improvement documented
- [ ] No regression in update operations

## Estimated Time

⏱️ **2-3 hours**

## Priority

🟢 **P3 - Low**" "performance,optimization,database" "Sprint 26"

create_issue "[Security] Improve Session Authorization and Rate Limiting" "## Overview

Add authorization checks and rate limiting to session management endpoints.

## Current Issues

- ❌ \`GET /auth/sessions/status\` - No authorization check (anyone can check any session?)
- ❌ \`POST /auth/sessions/extend\` - No rate limiting (session extension abuse)
- ❌ Direct DB access instead of CQRS

## Files Affected

- \`apps/api/src/Api/Routing/AuthEndpoints.cs:748-856\`

## Implementation

### 1. GetSessionStatusQuery

\`\`\`csharp
public record GetSessionStatusQuery(
    string SessionToken,
    string RequestingUserId) : IRequest<SessionStatusDto>;

// Handler should verify:
// - User owns the session OR
// - User has Admin role
\`\`\`

### 2. ExtendSessionCommand

\`\`\`csharp
public record ExtendSessionCommand(
    string SessionToken,
    string UserId) : IRequest<ExtendSessionResult>;

// Add rate limiting:
// - Max 10 extensions per hour per user
\`\`\`

### 3. RevokeSessionCommand

\`\`\`csharp
public record RevokeSessionCommand(
    string SessionToken,
    string RevokingUserId) : IRequest<bool>;
\`\`\`

## Migration Steps

1. Create commands/queries in \`Authentication/Application/\`
2. Add authorization logic in handlers
3. Add rate limiting for extend endpoint
4. Update endpoints to use MediatR
5. Test authorization scenarios

## Acceptance Criteria

- [ ] Users can only access own sessions (or admin can access all)
- [ ] Session extend has rate limiting
- [ ] All session operations use CQRS
- [ ] Audit logging for session operations

## Estimated Time

⏱️ **4-6 hours**

## Priority

🟢 **P3 - Low**" "security,authentication,authorization,rate-limiting" "Sprint 26"

create_issue "[Refactor] Centralize Error Handling with Middleware" "## Overview

Centralize error handling to reduce code duplication and improve consistency.

## Current Issues

- ❌ Scattered try-catch blocks across endpoints
- ❌ Duplicate error handling logic (DRY violation)
- ❌ Inconsistent error responses
- ❌ Potential information leakage in exception messages

## Files with Duplicate Patterns

- \`AiEndpoints.cs:209-259, 706-774\`
- \`ChatEndpoints.cs:112-116, 176-190, 248-257\`
- \`RuleSpecEndpoints.cs:65-68, 176-180, 210-218\`

## Implementation

### 1. Global Exception Middleware

\`\`\`csharp
public class GlobalExceptionHandlerMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (DomainException ex)
        {
            await HandleDomainException(context, ex);
        }
        catch (NotFoundException ex)
        {
            await HandleNotFoundException(context, ex);
        }
        catch (UnauthorizedException ex)
        {
            await HandleUnauthorizedException(context, ex);
        }
        catch (Exception ex)
        {
            await HandleUnexpectedException(context, ex);
        }
    }
}
\`\`\`

### 2. Result<T> Pattern (optional enhancement)

\`\`\`csharp
public record Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public Error? Error { get; init; }

    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Failure(Error error) => new() { IsSuccess = false, Error = error };
}
\`\`\`

### 3. Exception Filters

\`\`\`csharp
public class ApiExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        // Map exceptions to HTTP status codes
        // Log exceptions
        // Return consistent error format
    }
}
\`\`\`

## Migration Steps

1. Create middleware in \`Api/Middleware/\`
2. Register in \`Program.cs\`
3. Remove try-catch from endpoints (let middleware handle)
4. Standardize error response format
5. Ensure no sensitive data in error messages
6. Update tests

## Acceptance Criteria

- [ ] Single source of error handling
- [ ] Consistent error response format
- [ ] No sensitive data leakage
- [ ] Proper logging of exceptions
- [ ] Reduced code duplication

## Estimated Time

⏱️ **6-8 hours**

## Priority

🟢 **P3 - Low**" "refactoring,error-handling,middleware,tech-debt" "Sprint 27"

# ========================================
# SUMMARY
# ========================================

echo ""
echo -e "${GREEN}================================================${NC}"
echo -e "${GREEN}  ✓ Issue Creation Complete!${NC}"
echo -e "${GREEN}================================================${NC}"
echo ""
echo -e "${BLUE}Summary:${NC}"
echo -e "  Phase 1 (P0 - Critical):      1 issue"
echo -e "  Phase 2 (P1 - High):          4 issues"
echo -e "  Phase 3 (P2 - Medium):        4 issues"
echo -e "  Phase 4 (P3 - Low):           3 issues"
echo -e "${BLUE}  Total:                       12 issues${NC}"
echo ""
echo -e "${YELLOW}Estimated Total Effort: 78-110 hours${NC}"
echo ""
echo -e "${GREEN}Next Steps:${NC}"
echo -e "  1. Review created issues on GitHub"
echo -e "  2. Assign to team members"
echo -e "  3. Create/update milestones if needed"
echo -e "  4. Start with Phase 1 (P0 - Critical)"
echo ""
echo -e "${BLUE}View all issues:${NC}"
echo -e "  gh issue list --label ddd,cqrs,legacy-code"
echo ""
