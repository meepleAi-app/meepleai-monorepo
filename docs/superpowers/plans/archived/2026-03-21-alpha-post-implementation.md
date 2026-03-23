# Alpha Post-Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the alpha system with admin UI for agent lifecycle, user catalog filtering, domain tests, styled email, and Slack routing.

**Architecture:** Task 1 is the largest (frontend admin UI). Tasks 2-5 are small, focused backend changes. All follow existing patterns in the codebase. Frontend uses React 19 + TanStack Query + shadcn/ui. Backend is .NET 9 DDD + CQRS.

**Tech Stack:** Next.js 16 (React 19, TanStack Query, Zod) | .NET 9 (MediatR, EF Core) | xUnit + FluentAssertions

---

## File Structure

| File | Action | Responsibility |
|------|--------|---------------|
| `apps/web/src/lib/api/schemas/agent-definitions.schemas.ts` | Modify | Add `status` field to Zod schema |
| `apps/web/src/lib/api/agent-definitions.api.ts` | Modify | Add `startTesting`, `publish`, `unpublish` API methods |
| `apps/web/src/components/admin/agent-definitions/BuilderTable.tsx` | Modify | Show lifecycle status badge + add lifecycle actions to dropdown |
| `apps/web/src/components/admin/agent-definitions/BuilderFilters.tsx` | Modify | Add status filter (Draft/Testing/Published) |
| `apps/web/src/app/admin/(dashboard)/agents/definitions/[id]/page.tsx` | Modify | Show status badge + lifecycle action buttons |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AgentDefinition/GetAllAgentDefinitionsQuery.cs` | Modify | Add `PublishedOnly` param |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AgentDefinition/GetAllAgentDefinitionsQueryHandler.cs` | Modify | Filter by Published when `PublishedOnly=true` |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Repositories/IAgentDefinitionRepository.cs` | Modify | Add `GetAllPublishedAsync` |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/AgentDefinitionTests.cs` | Modify | Add lifecycle state transition tests |
| `apps/api/src/Api/Services/Email/EmailService.Auth.cs` | Verify | Already styled — verify only |
| `apps/api/src/Api/appsettings.json` | Modify | Add `access_request` Slack channel route |
| `apps/api/src/Api/BoundedContexts/Authentication/Application/EventHandlers/AccessRequestCreatedEventHandler.cs` | Modify | Add `_slack_category` metadata |

---

### Task 1: Frontend Admin UI for AgentDefinition Lifecycle

**Goal:** Admin can see Draft/Testing/Published status and transition agents through the lifecycle from the definitions list and detail page.

**Files:**
- Modify: `apps/web/src/lib/api/schemas/agent-definitions.schemas.ts`
- Modify: `apps/web/src/lib/api/agent-definitions.api.ts`
- Modify: `apps/web/src/components/admin/agent-definitions/BuilderTable.tsx`
- Modify: `apps/web/src/components/admin/agent-definitions/BuilderFilters.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/agents/definitions/[id]/page.tsx`

- [ ] **Step 1: Add `status` to frontend Zod schema**

Read `apps/web/src/lib/api/schemas/agent-definitions.schemas.ts`. Add `status` field to the agent definition schema:

```typescript
status: z.enum(['Draft', 'Testing', 'Published']).optional().default('Draft'),
```

Also check the TypeScript `AgentDefinitionDto` type — if defined separately, add `status` there too.

Note: The backend returns `Status` as an integer (0=Draft, 1=Testing, 2=Published). Check how `isActive` boolean is serialized. The Zod schema may need to handle integer → string mapping, or the API client may need to map it. Check existing patterns.

- [ ] **Step 2: Add lifecycle API methods**

Read `apps/web/src/lib/api/agent-definitions.api.ts`. Add three methods:

```typescript
async startTesting(id: string): Promise<void> {
  await apiClient.post(`${BASE_URL}/${id}/start-testing`);
},
async publish(id: string): Promise<void> {
  await apiClient.post(`${BASE_URL}/${id}/publish`);
},
async unpublish(id: string): Promise<void> {
  await apiClient.post(`${BASE_URL}/${id}/unpublish`);
},
```

Check how `apiClient` is used in other methods and follow the same pattern.

- [ ] **Step 3: Update BuilderTable to show lifecycle status**

Read `apps/web/src/components/admin/agent-definitions/BuilderTable.tsx`.

Replace the current `isActive` status badge with a lifecycle status badge:

```tsx
// Status column — replace isActive badge with lifecycle status
const statusColors: Record<string, string> = {
  Draft: 'bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300',
  Testing: 'bg-amber-100 text-amber-700 dark:bg-amber-900 dark:text-amber-300',
  Published: 'bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300',
};
const statusLabel = ['Draft', 'Testing', 'Published'][agent.status ?? 0] || 'Draft';

<Badge className={statusColors[statusLabel]}>{statusLabel}</Badge>
```

Add lifecycle actions to the dropdown menu (alongside Edit and Delete):

```tsx
// After Edit menu item, before Delete:
{statusLabel === 'Draft' && (
  <DropdownMenuItem onClick={() => onStartTesting(agent.id)}>
    <FlaskConical className="h-4 w-4 mr-2" />Start Testing
  </DropdownMenuItem>
)}
{statusLabel === 'Testing' && (
  <DropdownMenuItem onClick={() => onPublish(agent.id)}>
    <Rocket className="h-4 w-4 mr-2" />Publish
  </DropdownMenuItem>
)}
{statusLabel === 'Published' && (
  <DropdownMenuItem onClick={() => onUnpublish(agent.id)}>
    <ArchiveRestore className="h-4 w-4 mr-2" />Unpublish
  </DropdownMenuItem>
)}
```

Add `onStartTesting`, `onPublish`, `onUnpublish` props to the component (same pattern as `onDelete`).

- [ ] **Step 4: Add lifecycle mutations to the definitions list page**

Read `apps/web/src/app/admin/(dashboard)/agents/definitions/page.tsx`. Add mutations:

```typescript
const startTestingMutation = useMutation({
  mutationFn: (id: string) => agentDefinitionsApi.startTesting(id),
  onSuccess: () => {
    toast.success('Agent moved to Testing');
    queryClient.invalidateQueries({ queryKey: ['admin', 'agent-definitions'] });
  },
  onError: () => toast.error('Failed to start testing'),
});

const publishMutation = useMutation({
  mutationFn: (id: string) => agentDefinitionsApi.publish(id),
  onSuccess: () => {
    toast.success('Agent published');
    queryClient.invalidateQueries({ queryKey: ['admin', 'agent-definitions'] });
  },
  onError: () => toast.error('Failed to publish'),
});

const unpublishMutation = useMutation({
  mutationFn: (id: string) => agentDefinitionsApi.unpublish(id),
  onSuccess: () => {
    toast.success('Agent unpublished');
    queryClient.invalidateQueries({ queryKey: ['admin', 'agent-definitions'] });
  },
  onError: () => toast.error('Failed to unpublish'),
});
```

Pass to `BuilderTable`:
```tsx
<BuilderTable
  agents={data}
  onDelete={(id) => deleteMutation.mutate(id)}
  onStartTesting={(id) => startTestingMutation.mutate(id)}
  onPublish={(id) => publishMutation.mutate(id)}
  onUnpublish={(id) => unpublishMutation.mutate(id)}
/>
```

- [ ] **Step 5: Update detail page with lifecycle actions**

Read `apps/web/src/app/admin/(dashboard)/agents/definitions/[id]/page.tsx`. Add lifecycle action buttons next to the existing Edit button in the header:

```tsx
// Next to the Edit button, show lifecycle action based on current status
{agent.status === 0 && (
  <Button size="sm" onClick={() => startTestingMutation.mutate(id)}>
    <FlaskConical className="h-4 w-4 mr-1" />Start Testing
  </Button>
)}
{agent.status === 1 && (
  <Button size="sm" onClick={() => publishMutation.mutate(id)}>
    <Rocket className="h-4 w-4 mr-1" />Publish
  </Button>
)}
{agent.status === 2 && (
  <Button size="sm" variant="outline" onClick={() => unpublishMutation.mutate(id)}>
    <ArchiveRestore className="h-4 w-4 mr-1" />Unpublish
  </Button>
)}
```

Also show the status badge next to the `isActive` badge.

- [ ] **Step 6: Add status filter to BuilderFilters**

Read `apps/web/src/components/admin/agent-definitions/BuilderFilters.tsx`. Add a lifecycle status select alongside the existing Active filter:

```tsx
<Select value={statusFilter} onValueChange={setStatusFilter}>
  <SelectTrigger className="w-[160px]">
    <SelectValue placeholder="All Statuses" />
  </SelectTrigger>
  <SelectContent>
    <SelectItem value="all">All Statuses</SelectItem>
    <SelectItem value="Draft">Draft</SelectItem>
    <SelectItem value="Testing">Testing</SelectItem>
    <SelectItem value="Published">Published</SelectItem>
  </SelectContent>
</Select>
```

Check how filters are passed to the API — they may need a `status` query param added to `getAll()`.

- [ ] **Step 7: Build frontend**

Run: `cd apps/web && pnpm build`

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/lib/api/schemas/agent-definitions.schemas.ts
git add apps/web/src/lib/api/agent-definitions.api.ts
git add apps/web/src/components/admin/agent-definitions/
git add apps/web/src/app/admin/\(dashboard\)/agents/definitions/
git commit -m "feat(web): add admin UI for AgentDefinition lifecycle (Draft/Testing/Published)"
```

---

### Task 2: User-Facing Agent Catalog — Published Filter

**Goal:** Backend query supports `PublishedOnly` parameter. User-facing code only sees Published agents.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AgentDefinition/GetAllAgentDefinitionsQuery.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AgentDefinition/GetAllAgentDefinitionsQueryHandler.cs`
- Modify: Repository interface + implementation (find `IAgentDefinitionRepository`)

- [ ] **Step 1: Add `PublishedOnly` parameter to query**

Read `GetAllAgentDefinitionsQuery.cs`. Add parameter:

```csharp
public sealed record GetAllAgentDefinitionsQuery(
    bool ActiveOnly = false,
    bool PublishedOnly = false)
    : IRequest<List<AgentDefinitionDto>>;
```

- [ ] **Step 2: Add `GetAllPublishedAsync` to repository**

Find `IAgentDefinitionRepository` (search in KnowledgeBase domain/infrastructure). Add:

```csharp
Task<IReadOnlyList<AgentDefinition>> GetAllPublishedAsync(CancellationToken ct = default);
```

Implement in the repository class:

```csharp
public async Task<IReadOnlyList<AgentDefinition>> GetAllPublishedAsync(CancellationToken ct = default)
    => await _dbContext.Set<AgentDefinition>()
        .Where(a => a.Status == AgentDefinitionStatus.Published && a.IsActive)
        .OrderByDescending(a => a.CreatedAt)
        .ToListAsync(ct);
```

Note: Check how the repository accesses `Status` — it may use shadow properties or direct property access.

- [ ] **Step 3: Update handler to use PublishedOnly filter**

In `GetAllAgentDefinitionsQueryHandler.Handle()`, add:

```csharp
if (request.PublishedOnly)
{
    var published = await _repository.GetAllPublishedAsync(cancellationToken);
    return published.Select(MapToDto).ToList();
}
```

- [ ] **Step 4: Build and test**

Run: `cd apps/api/src/Api && dotnet build`

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/
git commit -m "feat(kb): add PublishedOnly filter for user-facing agent definition queries"
```

---

### Task 3: Unit Tests for AgentDefinition State Transitions

**Goal:** Add unit tests for all lifecycle state transitions (valid + invalid) to the existing test file.

**Files:**
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/AgentDefinitionTests.cs`

- [ ] **Step 1: Read existing test file**

Read `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/AgentDefinitionTests.cs` to understand the pattern. Tests use `[Fact]`, `[Trait("Category", "Unit")]`, FluentAssertions.

- [ ] **Step 2: Add lifecycle transition tests**

Add these tests to the file (follow existing naming and arrange/act/assert pattern):

```csharp
// ─── Lifecycle State Transitions ──────────────────────────────────

[Fact]
public void Create_ShouldStartAsDraft()
{
    var agent = CreateValidAgentDefinition();
    agent.Status.Should().Be(AgentDefinitionStatus.Draft);
    agent.IsActive.Should().BeFalse();
}

[Fact]
public void StartTesting_FromDraft_ShouldTransitionToTesting()
{
    var agent = CreateValidAgentDefinition();
    agent.StartTesting();
    agent.Status.Should().Be(AgentDefinitionStatus.Testing);
}

[Fact]
public void Publish_FromTesting_ShouldTransitionToPublished()
{
    var agent = CreateValidAgentDefinition();
    agent.StartTesting();
    agent.Publish();
    agent.Status.Should().Be(AgentDefinitionStatus.Published);
    agent.IsActive.Should().BeTrue();
}

[Fact]
public void Publish_FromDraft_ShouldThrow()
{
    var agent = CreateValidAgentDefinition();
    var act = () => agent.Publish();
    act.Should().Throw<InvalidOperationException>()
        .WithMessage("*Draft*Testing*");
}

[Fact]
public void StartTesting_FromPublished_ShouldThrow()
{
    var agent = CreateValidAgentDefinition();
    agent.StartTesting();
    agent.Publish();
    var act = () => agent.StartTesting();
    act.Should().Throw<InvalidOperationException>()
        .WithMessage("*Published*");
}

[Fact]
public void Unpublish_FromPublished_ShouldReturnToDraft()
{
    var agent = CreateValidAgentDefinition();
    agent.StartTesting();
    agent.Publish();
    agent.Unpublish();
    agent.Status.Should().Be(AgentDefinitionStatus.Draft);
    agent.IsActive.Should().BeFalse();
}

[Fact]
public void Unpublish_FromTesting_ShouldReturnToDraft()
{
    var agent = CreateValidAgentDefinition();
    agent.StartTesting();
    agent.Unpublish();
    agent.Status.Should().Be(AgentDefinitionStatus.Draft);
}

[Fact]
public void FullLifecycle_DraftToTestingToPublishedToUnpublished()
{
    var agent = CreateValidAgentDefinition();
    agent.Status.Should().Be(AgentDefinitionStatus.Draft);

    agent.StartTesting();
    agent.Status.Should().Be(AgentDefinitionStatus.Testing);

    agent.Publish();
    agent.Status.Should().Be(AgentDefinitionStatus.Published);
    agent.IsActive.Should().BeTrue();

    agent.Unpublish();
    agent.Status.Should().Be(AgentDefinitionStatus.Draft);
    agent.IsActive.Should().BeFalse();
}
```

Check if there's already a `CreateValidAgentDefinition()` helper method in the test file. If not, create one that calls `AgentDefinition.Create(...)` with valid test data, matching existing test patterns.

**IMPORTANT**: After adding Status lifecycle in the previous sprint, `Create()` now sets `_isActive = false` and `_status = Draft`. Existing tests that assert `agent.IsActive.Should().BeTrue()` after `Create()` may need updating. Check `Create_WithValidData_ShouldCreateAgentDefinition` — if it asserts `IsActive = true`, update it to `false`.

- [ ] **Step 3: Run tests**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "AgentDefinitionTests" -v normal`
Expected: All tests pass (new + existing)

- [ ] **Step 4: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/AgentDefinitionTests.cs
git commit -m "test(kb): add unit tests for AgentDefinition lifecycle state transitions"
```

---

### Task 4: Verify Email Template (Already Styled)

**Goal:** Verify the rejection email already follows the styled template pattern. If it does, skip. If not, update.

**Files:**
- Verify: `apps/api/src/Api/Services/Email/EmailService.Auth.cs`

- [ ] **Step 1: Read and verify current implementation**

Read `apps/api/src/Api/Services/Email/EmailService.Auth.cs`, find `BuildAccessRequestRejectedEmailBody`.

The exploration found it already uses:
- Gray header with "MeepleAI" h1
- White card with 1px border
- Yellow left-bordered box for reason
- Footer with "automated message" + copyright

If it matches the shared pattern from other emails (e.g., `BuildPasswordResetEmailBody`, `BuildInvitationEmailBody`), this task is **DONE — no changes needed**.

- [ ] **Step 2: If changes needed, update to match**

Only modify if the current implementation uses raw/unstyled HTML. Compare with `BuildInvitationEmailBody` for the canonical pattern.

- [ ] **Step 3: Commit (only if changes made)**

```bash
git add apps/api/src/Api/Services/Email/EmailService.Auth.cs
git commit -m "fix(email): align rejection email template with shared styling pattern"
```

---

### Task 5: Slack Alert Routing for Access Requests

**Goal:** Add `access_request` category to Slack channel routing so access request notifications go to the right channel. Pass `_slack_category` metadata in the event handler.

**Files:**
- Modify: `apps/api/src/Api/appsettings.json`
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/EventHandlers/AccessRequestCreatedEventHandler.cs`

- [ ] **Step 1: Add `access_request` route to appsettings.json**

Read `apps/api/src/Api/appsettings.json`, find the `Alerting.Slack.ChannelRouting` section. Add:

```json
"access_request": { "WebhookUrl": "", "Channel": "#admin-alerts" }
```

Place it alongside the existing `critical`, `infrastructure`, `ai`, `security` routes.

Note: The `WebhookUrl` is empty by default (configured per-environment via secrets). The `Channel` is a default target.

- [ ] **Step 2: Add `_slack_category` metadata to event handler**

Read `apps/api/src/Api/BoundedContexts/Authentication/Application/EventHandlers/AccessRequestCreatedEventHandler.cs`.

In the `SendAlertAsync` call, add `_slack_category` to the metadata dictionary:

```csharp
new Dictionary<string, object>(StringComparer.Ordinal)
{
    ["email"] = domainEvent.Email,
    ["requestId"] = domainEvent.AccessRequestId,
    ["_slack_category"] = "access_request"  // ← ADD THIS
}
```

This tells `SlackAlertChannel.ResolveRoute()` to look up the `access_request` route in `ChannelRouting`.

- [ ] **Step 3: Build**

Run: `cd apps/api/src/Api && dotnet build`

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/appsettings.json
git add apps/api/src/Api/BoundedContexts/Authentication/Application/EventHandlers/AccessRequestCreatedEventHandler.cs
git commit -m "feat(auth): add Slack channel routing for access request notifications"
```

---

## Execution Order

```
[Task 1: Admin UI lifecycle] → [Task 2: Published filter] → [Task 3: Unit tests] → [Task 4: Email verify] → [Task 5: Slack routing]
```

Tasks 3, 4, 5 are independent and could run in parallel after Task 2.

**Recommended serial order:** 1 → 2 → 3 → 4 → 5
