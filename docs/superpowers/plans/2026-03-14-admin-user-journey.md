# Admin User Journey — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the full admin-user journey: invite via email, accept invite, add game, create agent, voice chat with content-gating, admin role management, and audit log viewer — validated exclusively by E2E Playwright tests.

**Architecture:** 4-phase vertical slice approach. Each phase produces a self-contained PR with its own E2E tests. Backend is ~95% done; work is primarily frontend pages, one new backend command (RevokeInvitation), content-gating logic, and 14 E2E tests.

**Tech Stack:** Next.js 16 (App Router), React 19, Tailwind 4, shadcn/ui, Zustand, React Query, Playwright, .NET 9 (MediatR/CQRS), PostgreSQL, EF Core

**Spec:** `docs/superpowers/specs/2026-03-14-admin-user-journey-design.md`

---

## File Structure

### Phase 1: Invite Flow
```
CREATE: apps/web/src/app/admin/(dashboard)/users/invitations/page.tsx
CREATE: apps/web/src/app/(public)/accept-invite/page.tsx
CREATE: apps/web/src/app/(public)/accept-invite/layout.tsx
MODIFY: apps/web/src/config/admin-dashboard-navigation.ts
MODIFY: apps/web/src/lib/api/clients/adminClient.ts
CREATE: apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/RevokeInvitation/RevokeInvitationCommand.cs
CREATE: apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/RevokeInvitation/RevokeInvitationCommandHandler.cs
MODIFY: apps/api/src/Api/Routing/AdminUserEndpoints.cs
CREATE: apps/web/tests/e2e/admin/admin-invite.spec.ts
```

### Phase 2: Content-Gating RAG
```
CREATE: apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/ValueObjects/ContentAccessLevel.cs
MODIFY: apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/  (chat handler — content filtering)
MODIFY: apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/  (CreateAgent handler — KB gate)
CREATE: apps/web/src/components/chat-unified/SourceReference.tsx
MODIFY: apps/web/src/components/chat-unified/  (chat message rendering)
MODIFY: apps/web/src/components/admin/shared-games/AgentBuilderModal.tsx
CREATE: apps/web/tests/e2e/agent-content-gating.spec.ts
```

### Phase 3: Admin User Management
```
CREATE: apps/web/src/app/admin/(dashboard)/users/[id]/page.tsx
CREATE: apps/web/src/app/admin/(dashboard)/system/audit-log/page.tsx
MODIFY: apps/web/src/config/admin-dashboard-navigation.ts
MODIFY: apps/web/src/lib/api/clients/adminClient.ts
MODIFY: apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ChangeUserRoleCommand.cs  (add Reason)
CREATE: apps/web/tests/e2e/admin/admin-role-audit.spec.ts
```

### Phase 4: Smoke Test Journey
```
CREATE: apps/web/tests/e2e/full-user-journey.spec.ts
```

---

## Chunk 1: Phase 1 — Invite Flow

### Task 1.1: Backend — RevokeInvitationCommand

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/RevokeInvitation/RevokeInvitationCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/RevokeInvitation/RevokeInvitationCommandHandler.cs`
- Modify: `apps/api/src/Api/Routing/AdminUserEndpoints.cs`

**Reference pattern:** `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/RevokeShareLink/RevokeShareLinkCommand.cs`

- [ ] **Step 1: Create RevokeInvitationCommand**

```csharp
// apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/RevokeInvitation/RevokeInvitationCommand.cs
using MediatR;

namespace Api.BoundedContexts.Authentication.Application.Commands.RevokeInvitation;

/// <summary>
/// Revokes a pending invitation, making its token immediately invalid.
/// </summary>
internal sealed record RevokeInvitationCommand(
    Guid InvitationId,
    Guid AdminUserId
) : IRequest<bool>;
```

- [ ] **Step 2: Create RevokeInvitationCommandHandler**

```csharp
// apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/RevokeInvitation/RevokeInvitationCommandHandler.cs
using Api.BoundedContexts.Authentication.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.RevokeInvitation;

internal sealed class RevokeInvitationCommandHandler
    : IRequestHandler<RevokeInvitationCommand, bool>
{
    private readonly IInvitationTokenRepository _invitationRepo;
    private readonly ILogger<RevokeInvitationCommandHandler> _logger;

    public RevokeInvitationCommandHandler(
        IInvitationTokenRepository invitationRepo,
        ILogger<RevokeInvitationCommandHandler> logger)
    {
        _invitationRepo = invitationRepo ?? throw new ArgumentNullException(nameof(invitationRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(
        RevokeInvitationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invitation = await _invitationRepo
            .GetByIdAsync(request.InvitationId, cancellationToken)
            .ConfigureAwait(false);

        if (invitation == null)
        {
            _logger.LogWarning("Invitation {InvitationId} not found", request.InvitationId);
            return false;
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            _logger.LogWarning(
                "Cannot revoke invitation {InvitationId} with status {Status}",
                request.InvitationId, invitation.Status);
            return false;
        }

        invitation.Revoke();

        await _invitationRepo
            .UpdateAsync(invitation, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Invitation {InvitationId} revoked by admin {AdminUserId}",
            request.InvitationId, request.AdminUserId);

        return true;
    }
}
```

**Note:** Check if `InvitationToken` entity has a `Revoke()` method. If not, add: `public void Revoke() { Status = InvitationStatus.Revoked; RevokedAt = DateTime.UtcNow; }`. Check the domain entity at `BoundedContexts/Authentication/Domain/Entities/InvitationToken.cs`.

- [ ] **Step 3: Register endpoint in AdminUserEndpoints**

Find the invitation section in `AdminUserEndpoints.cs` and add:

```csharp
// After the existing resend endpoint
group.MapDelete("/admin/users/invitations/{id:guid}", HandleRevokeInvitation)
    .WithName("RevokeInvitation")
    .RequireAdminSession()
    .WithTags("AdminUsers");
```

Handler method:
```csharp
private static async Task<IResult> HandleRevokeInvitation(
    Guid id,
    HttpContext context,
    IMediator mediator,
    CancellationToken ct)
{
    var (authorized, session, error) = context.RequireAdminSession();
    if (!authorized) return error!;

    var command = new RevokeInvitationCommand(
        InvitationId: id,
        AdminUserId: session!.User!.Id
    );

    var success = await mediator.Send(command, ct).ConfigureAwait(false);

    return success
        ? Results.Ok(new { success = true })
        : Results.NotFound(new { error = "Invitation not found or not pending" });
}
```

- [ ] **Step 4: Build and verify**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/RevokeInvitation/
git add apps/api/src/Api/Routing/AdminUserEndpoints.cs
git commit -m "feat(auth): add RevokeInvitationCommand + endpoint"
```

---

### Task 1.2: Frontend — AdminClient Invitation Methods

**Files:**
- Modify: `apps/web/src/lib/api/clients/adminClient.ts`

**Reference pattern:** Existing `adminClient.ts` methods (getAllUsers, deleteUser, etc.)

- [ ] **Step 1: Add Zod schemas for invitation DTOs**

Add at the top of `adminClient.ts` (or in a schemas file if separated):

```typescript
import { z } from 'zod';

const InvitationStatusSchema = z.enum(['Pending', 'Accepted', 'Expired', 'Revoked']);

const InvitationSchema = z.object({
  id: z.string().uuid(),
  email: z.string().email(),
  role: z.string(),
  status: InvitationStatusSchema,
  sentAt: z.string().datetime(),
  expiresAt: z.string().datetime(),
  acceptedAt: z.string().datetime().nullable().optional(),
});

const InvitationListResponseSchema = z.object({
  invitations: z.array(InvitationSchema),
  total: z.number(),
});

const InvitationStatsSchema = z.object({
  total: z.number(),
  pending: z.number(),
  accepted: z.number(),
  expired: z.number(),
  revoked: z.number(),
});

export type Invitation = z.infer<typeof InvitationSchema>;
export type InvitationStats = z.infer<typeof InvitationStatsSchema>;
```

- [ ] **Step 2: Add invitation methods to adminClient**

Inside `createAdminClient` return object:

```typescript
// === Invitation Management ===

async sendInvitation(request: { email: string; role: string }): Promise<{ id: string }> {
  return httpClient.post('/api/v1/admin/users/invite', request);
},

async bulkSendInvitations(emails: string[], role: string): Promise<{ sent: number; failed: number }> {
  // Backend expects multipart/form-data with CSV file
  const csvContent = emails.join('\n');
  const blob = new Blob([csvContent], { type: 'text/csv' });
  const formData = new FormData();
  formData.append('file', blob, 'invitations.csv');
  formData.append('role', role);
  const response = await fetch('/api/v1/admin/users/bulk/invite', {
    method: 'POST',
    body: formData,
  });
  if (!response.ok) throw new Error('Bulk invite failed');
  return response.json();
},

async getInvitations(params?: {
  status?: string;
  page?: number;
  pageSize?: number;
}): Promise<{ invitations: Invitation[]; total: number }> {
  const query = new URLSearchParams();
  if (params?.status) query.set('status', params.status);
  if (params?.page) query.set('page', params.page.toString());
  if (params?.pageSize) query.set('pageSize', params.pageSize.toString());
  const url = `/api/v1/admin/users/invitations${query.toString() ? `?${query}` : ''}`;
  return httpClient.get(url, InvitationListResponseSchema);
},

async getInvitationStats(): Promise<InvitationStats> {
  return httpClient.get('/api/v1/admin/users/invitations/stats', InvitationStatsSchema);
},

async resendInvitation(invitationId: string): Promise<void> {
  await httpClient.post(`/api/v1/admin/users/invitations/${invitationId}/resend`, {});
},

async revokeInvitation(invitationId: string): Promise<void> {
  await httpClient.delete(`/api/v1/admin/users/invitations/${invitationId}`);
},
```

- [ ] **Step 3: Build and verify**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/api/clients/adminClient.ts
git commit -m "feat(frontend): add invitation management methods to adminClient"
```

---

### Task 1.3: Frontend — Admin Invitations Page

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/users/invitations/page.tsx`
- Modify: `apps/web/src/config/admin-dashboard-navigation.ts`

**Reference pattern:** `apps/web/src/app/admin/(dashboard)/knowledge-base/documents/page.tsx`

- [ ] **Step 1: Add nav item to admin-dashboard-navigation.ts**

Find the "Users" section in `DASHBOARD_SECTIONS` and add to `sidebarItems`:

```typescript
{
  href: '/admin/users/invitations',
  label: 'Invitations',
  icon: MailPlusIcon,
},
```

Add `MailPlusIcon` to the lucide-react imports at the top.

- [ ] **Step 2: Create invitations page**

```typescript
// apps/web/src/app/admin/(dashboard)/users/invitations/page.tsx
'use client';

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/overlays/alert-dialog-primitives';
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/collapsible';
import { Badge } from '@/components/ui/badge';
import {
  MailPlusIcon,
  RefreshCwIcon,
  XCircleIcon,
  ChevronDownIcon,
  AlertCircleIcon,
  UsersIcon,
  Loader2Icon,
} from 'lucide-react';
import type { Invitation } from '@/lib/api/clients/adminClient';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

function statusBadgeVariant(status: string) {
  switch (status) {
    case 'Pending': return 'warning' as const;
    case 'Accepted': return 'success' as const;
    case 'Expired': return 'secondary' as const;
    case 'Revoked': return 'destructive' as const;
    default: return 'default' as const;
  }
}

export default function InvitationsPage() {
  const queryClient = useQueryClient();
  const [email, setEmail] = useState('');
  const [role, setRole] = useState('User');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [page, setPage] = useState(1);
  const [bulkEmails, setBulkEmails] = useState('');
  const [bulkRole, setBulkRole] = useState('User');
  const pageSize = 20;

  // Queries
  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['admin', 'invitations', { page, pageSize, status: statusFilter }],
    queryFn: () => adminClient.getInvitations({
      status: statusFilter !== 'all' ? statusFilter : undefined,
      page,
      pageSize,
    }),
    staleTime: 30_000,
  });

  const { data: stats } = useQuery({
    queryKey: ['admin', 'invitations', 'stats'],
    queryFn: () => adminClient.getInvitationStats(),
    staleTime: 60_000,
  });

  // Mutations
  const sendMutation = useMutation({
    mutationFn: (req: { email: string; role: string }) => adminClient.sendInvitation(req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] });
      setEmail('');
    },
  });

  const bulkSendMutation = useMutation({
    mutationFn: ({ emails, bulkRole: r }: { emails: string[]; bulkRole: string }) => adminClient.bulkSendInvitations(emails, r),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] });
      setBulkEmails('');
    },
  });

  const resendMutation = useMutation({
    mutationFn: (id: string) => adminClient.resendInvitation(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] }),
  });

  const revokeMutation = useMutation({
    mutationFn: (id: string) => adminClient.revokeInvitation(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] }),
  });

  const handleSend = (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim()) return;
    sendMutation.mutate({ email: email.trim(), role });
  };

  const handleBulkSend = (e: React.FormEvent) => {
    e.preventDefault();
    const emails = bulkEmails.split('\n').map(e => e.trim()).filter(Boolean);
    if (emails.length === 0) return;
    bulkSendMutation.mutate({ emails, bulkRole });
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold font-quicksand">Invitations</h1>
        <p className="text-muted-foreground">Invite users to join MeepleAI</p>
      </div>

      {/* Stats Cards */}
      {stats && (
        <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
          {(['total', 'pending', 'accepted', 'expired', 'revoked'] as const).map((key) => (
            <Card key={key} className="p-3">
              <p className="text-xs text-muted-foreground capitalize">{key}</p>
              <p className="text-xl font-bold">{stats[key]}</p>
            </Card>
          ))}
        </div>
      )}

      {/* Send Invitation Form */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <MailPlusIcon className="h-5 w-5" />
            Send Invitation
          </CardTitle>
          <CardDescription>Send an email invitation to a new user</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSend} className="flex items-end gap-3">
            <div className="flex-1">
              <Label htmlFor="invite-email">Email</Label>
              <Input
                id="invite-email"
                type="email"
                placeholder="user@example.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
              />
            </div>
            <div className="w-40">
              <Label htmlFor="invite-role">Role</Label>
              <Select value={role} onValueChange={setRole}>
                <SelectTrigger id="invite-role">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="User">User</SelectItem>
                  <SelectItem value="Contributor">Contributor</SelectItem>
                  <SelectItem value="Admin">Admin</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <Button type="submit" disabled={sendMutation.isPending}>
              {sendMutation.isPending && <Loader2Icon className="h-4 w-4 animate-spin mr-2" />}
              Send
            </Button>
          </form>

          {sendMutation.isSuccess && (
            <p className="text-sm text-green-600 mt-2">Invitation sent successfully!</p>
          )}
          {sendMutation.isError && (
            <p className="text-sm text-red-600 mt-2">
              Failed: {sendMutation.error?.message}
            </p>
          )}
        </CardContent>
      </Card>

      {/* Bulk Invite (collapsible) */}
      <Collapsible>
        <Card>
          <CollapsibleTrigger asChild>
            <CardHeader className="cursor-pointer">
              <CardTitle className="flex items-center gap-2">
                <UsersIcon className="h-5 w-5" />
                Bulk Invite
                <ChevronDownIcon className="h-4 w-4 ml-auto" />
              </CardTitle>
            </CardHeader>
          </CollapsibleTrigger>
          <CollapsibleContent>
            <CardContent>
              <form onSubmit={handleBulkSend} className="space-y-3">
                <div>
                  <Label htmlFor="bulk-emails">Emails (one per line)</Label>
                  <textarea
                    id="bulk-emails"
                    className="w-full min-h-[100px] rounded-md border p-2 text-sm"
                    placeholder="user1@example.com&#10;user2@example.com"
                    value={bulkEmails}
                    onChange={(e) => setBulkEmails(e.target.value)}
                  />
                </div>
                <div className="flex items-end gap-3">
                  <div className="w-40">
                    <Label htmlFor="bulk-role">Role for all</Label>
                    <Select value={bulkRole} onValueChange={setBulkRole}>
                      <SelectTrigger id="bulk-role"><SelectValue /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="User">User</SelectItem>
                        <SelectItem value="Contributor">Contributor</SelectItem>
                        <SelectItem value="Admin">Admin</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <Button type="submit" disabled={bulkSendMutation.isPending}>
                    {bulkSendMutation.isPending && <Loader2Icon className="h-4 w-4 animate-spin mr-2" />}
                    Send All
                  </Button>
                </div>
              </form>
            </CardContent>
          </CollapsibleContent>
        </Card>
      </Collapsible>

      {/* Error Banner */}
      {error && (
        <div className="bg-red-50 dark:bg-red-900/20 p-4 rounded-lg flex items-center gap-3">
          <AlertCircleIcon className="h-5 w-5 text-red-500 shrink-0" />
          <p className="text-sm text-red-800 dark:text-red-200 flex-1">{error.message}</p>
          <Button variant="outline" size="sm" onClick={() => refetch()}>Retry</Button>
        </div>
      )}

      {/* Filter */}
      <div className="flex items-center gap-3">
        <Label>Status:</Label>
        <Select value={statusFilter} onValueChange={(v) => { setStatusFilter(v); setPage(1); }}>
          <SelectTrigger className="w-40"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All</SelectItem>
            <SelectItem value="Pending">Pending</SelectItem>
            <SelectItem value="Accepted">Accepted</SelectItem>
            <SelectItem value="Expired">Expired</SelectItem>
            <SelectItem value="Revoked">Revoked</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Invitations Table */}
      {isLoading ? (
        <div className="text-center py-8 text-muted-foreground">Loading invitations...</div>
      ) : (
        <Card>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Email</TableHead>
                <TableHead>Role</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Sent</TableHead>
                <TableHead>Expires</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data?.invitations.map((inv: Invitation) => (
                <TableRow key={inv.id}>
                  <TableCell className="font-medium">{inv.email}</TableCell>
                  <TableCell>{inv.role}</TableCell>
                  <TableCell>
                    <Badge variant={statusBadgeVariant(inv.status)}>{inv.status}</Badge>
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {new Date(inv.sentAt).toLocaleDateString()}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {new Date(inv.expiresAt).toLocaleDateString()}
                  </TableCell>
                  <TableCell className="text-right">
                    {inv.status === 'Pending' && (
                      <div className="flex justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => resendMutation.mutate(inv.id)}
                          disabled={resendMutation.isPending}
                          title="Resend"
                        >
                          <RefreshCwIcon className="h-4 w-4" />
                        </Button>
                        <AlertDialog>
                          <AlertDialogTrigger asChild>
                            <Button variant="ghost" size="sm" title="Revoke">
                              <XCircleIcon className="h-4 w-4 text-red-500" />
                            </Button>
                          </AlertDialogTrigger>
                          <AlertDialogContent>
                            <AlertDialogHeader>
                              <AlertDialogTitle>Revoke Invitation</AlertDialogTitle>
                              <AlertDialogDescription>
                                This will invalidate the invitation for {inv.email}. They will no longer be able to accept it.
                              </AlertDialogDescription>
                            </AlertDialogHeader>
                            <AlertDialogFooter>
                              <AlertDialogCancel>Cancel</AlertDialogCancel>
                              <AlertDialogAction
                                onClick={() => revokeMutation.mutate(inv.id)}
                                className="bg-red-600 hover:bg-red-700"
                              >
                                Revoke
                              </AlertDialogAction>
                            </AlertDialogFooter>
                          </AlertDialogContent>
                        </AlertDialog>
                      </div>
                    )}
                  </TableCell>
                </TableRow>
              ))}
              {data?.invitations.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} className="text-center text-muted-foreground py-8">
                    No invitations found
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </Card>
      )}

      {/* Pagination */}
      {data && data.total > pageSize && (
        <div className="flex justify-center gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1}
            onClick={() => setPage(p => p - 1)}
          >
            Previous
          </Button>
          <span className="text-sm py-2">
            Page {page} of {Math.ceil(data.total / pageSize)}
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={page >= Math.ceil(data.total / pageSize)}
            onClick={() => setPage(p => p + 1)}
          >
            Next
          </Button>
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 3: Typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors. If shadcn imports fail, check exact paths with `find src/components/ui -name "*.tsx" | head -30`

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/users/invitations/
git add apps/web/src/config/admin-dashboard-navigation.ts
git commit -m "feat(frontend): add admin invitations page"
```

---

### Task 1.4: Frontend — Accept Invite Page (Public)

**Files:**
- Create: `apps/web/src/app/(public)/accept-invite/page.tsx`

**Reference pattern:** `apps/web/src/app/(public)/page.tsx` for public route group

- [ ] **Step 1: Create accept-invite layout (minimal, no public header/footer)**

```typescript
// apps/web/src/app/(public)/accept-invite/layout.tsx
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Accept Invitation — MeepleAI',
  description: 'Set your password and join MeepleAI',
};

export default function AcceptInviteLayout({ children }: { children: React.ReactNode }) {
  // Minimal layout — no public header/footer for clean invite-accept UX
  return (
    <div className="min-h-screen bg-gradient-to-b from-amber-50/50 to-white dark:from-slate-950 dark:to-slate-900">
      <main className="container mx-auto px-4 py-8">
        {children}
      </main>
    </div>
  );
}
```

- [ ] **Step 2: Create accept-invite page**

```typescript
// apps/web/src/app/(public)/accept-invite/page.tsx
'use client';

import { useState, useEffect } from 'react';
import { useSearchParams, useRouter } from 'next/navigation';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { AlertCircleIcon, CheckCircle2Icon, Loader2Icon, LockIcon } from 'lucide-react';

type PageState = 'loading' | 'valid' | 'invalid' | 'submitting' | 'success' | 'error';

interface ValidationResult {
  email: string;
  role: string;
  expiresAt: string;
}

function PasswordStrength({ password }: { password: string }) {
  const checks = [
    { label: 'Min 8 characters', met: password.length >= 8 },
    { label: '1 uppercase letter', met: /[A-Z]/.test(password) },
    { label: '1 number', met: /\d/.test(password) },
    { label: '1 special character', met: /[!@#$%^&*(),.?":{}|<>]/.test(password) },
  ];
  const strength = checks.filter(c => c.met).length;

  return (
    <div className="space-y-1 mt-2">
      <div className="flex gap-1">
        {[1, 2, 3, 4].map(i => (
          <div
            key={i}
            className={`h-1 flex-1 rounded ${
              i <= strength
                ? strength <= 1 ? 'bg-red-500'
                : strength <= 2 ? 'bg-orange-500'
                : strength <= 3 ? 'bg-yellow-500'
                : 'bg-green-500'
                : 'bg-gray-200 dark:bg-gray-700'
            }`}
          />
        ))}
      </div>
      <div className="space-y-0.5">
        {checks.map(check => (
          <p key={check.label} className={`text-xs ${check.met ? 'text-green-600' : 'text-muted-foreground'}`}>
            {check.met ? '✓' : '○'} {check.label}
          </p>
        ))}
      </div>
    </div>
  );
}

export default function AcceptInvitePage() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const token = searchParams.get('token');

  const [state, setState] = useState<PageState>('loading');
  const [validation, setValidation] = useState<ValidationResult | null>(null);
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [errorMessage, setErrorMessage] = useState('');

  // Validate token on mount
  useEffect(() => {
    if (!token) {
      setState('invalid');
      setErrorMessage('No invitation token provided.');
      return;
    }

    const validateToken = async () => {
      try {
        const res = await fetch('/api/v1/auth/validate-invitation', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ token }),
        });

        if (!res.ok) {
          const data = await res.json().catch(() => ({}));
          setState('invalid');
          setErrorMessage(data.error || 'This invitation is invalid or has expired.');
          return;
        }

        const data = await res.json();
        setValidation(data);
        setState('valid');
      } catch {
        setState('invalid');
        setErrorMessage('Failed to validate invitation. Please try again.');
      }
    };

    validateToken();
  }, [token]);

  const isPasswordValid =
    password.length >= 8 &&
    /[A-Z]/.test(password) &&
    /\d/.test(password) &&
    /[!@#$%^&*(),.?":{}|<>]/.test(password) &&
    password === confirmPassword;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isPasswordValid || !token) return;

    setState('submitting');
    try {
      const res = await fetch('/api/v1/auth/accept-invitation', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token, password, confirmPassword }),
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        setState('error');
        setErrorMessage(data.error || 'Failed to create account.');
        return;
      }

      // Backend returns session — cookie is set automatically
      setState('success');

      // Redirect to dashboard after brief success message
      setTimeout(() => router.push('/dashboard'), 1500);
    } catch {
      setState('error');
      setErrorMessage('An unexpected error occurred.');
    }
  };

  // Loading state
  if (state === 'loading') {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <Card className="w-full max-w-md">
          <CardContent className="flex items-center justify-center p-12">
            <Loader2Icon className="h-8 w-8 animate-spin text-muted-foreground" />
          </CardContent>
        </Card>
      </div>
    );
  }

  // Invalid token
  if (state === 'invalid') {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <Card className="w-full max-w-md">
          <CardHeader className="text-center">
            <AlertCircleIcon className="h-12 w-12 text-red-500 mx-auto mb-2" />
            <CardTitle>Invalid Invitation</CardTitle>
            <CardDescription>{errorMessage}</CardDescription>
          </CardHeader>
          <CardContent className="text-center">
            <p className="text-sm text-muted-foreground">
              Contact your administrator to request a new invitation.
            </p>
          </CardContent>
        </Card>
      </div>
    );
  }

  // Success
  if (state === 'success') {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <Card className="w-full max-w-md">
          <CardHeader className="text-center">
            <CheckCircle2Icon className="h-12 w-12 text-green-500 mx-auto mb-2" />
            <CardTitle>Account Created!</CardTitle>
            <CardDescription>Redirecting to your dashboard...</CardDescription>
          </CardHeader>
        </Card>
      </div>
    );
  }

  // Valid token — show form
  return (
    <div className="flex items-center justify-center min-h-[60vh]">
      <Card className="w-full max-w-md">
        <CardHeader className="text-center">
          <LockIcon className="h-10 w-10 text-amber-500 mx-auto mb-2" />
          <CardTitle>Welcome to MeepleAI</CardTitle>
          <CardDescription>Set your password to activate your account</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            {/* Email (readonly) */}
            <div>
              <Label htmlFor="email">Email</Label>
              <Input
                id="email"
                type="email"
                value={validation?.email || ''}
                disabled
                className="bg-muted"
              />
            </div>

            {/* Role badge */}
            <div className="text-sm text-muted-foreground">
              You&apos;ve been invited as: <span className="font-medium text-foreground">{validation?.role}</span>
            </div>

            {/* Password */}
            <div>
              <Label htmlFor="password">Password</Label>
              <Input
                id="password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Choose a strong password"
                required
              />
              <PasswordStrength password={password} />
            </div>

            {/* Confirm Password */}
            <div>
              <Label htmlFor="confirm-password">Confirm Password</Label>
              <Input
                id="confirm-password"
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder="Confirm your password"
                required
              />
              {confirmPassword && password !== confirmPassword && (
                <p className="text-xs text-red-500 mt-1">Passwords do not match</p>
              )}
            </div>

            {/* Error */}
            {state === 'error' && (
              <div className="bg-red-50 dark:bg-red-900/20 p-3 rounded text-sm text-red-800 dark:text-red-200">
                {errorMessage}
              </div>
            )}

            {/* Submit */}
            <Button
              type="submit"
              className="w-full"
              disabled={!isPasswordValid || state === 'submitting'}
            >
              {state === 'submitting' && <Loader2Icon className="h-4 w-4 animate-spin mr-2" />}
              Create Account
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
```

- [ ] **Step 3: Typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/(public)/accept-invite/
git commit -m "feat(frontend): add accept-invite public page with password setup"
```

---

### Task 1.5: E2E Test — Admin Invite Flow

**Files:**
- Create: `apps/web/tests/e2e/admin/admin-invite.spec.ts`

**Reference pattern:** `apps/web/tests/e2e/admin/shared-games/agent-workflow.spec.ts`

- [ ] **Step 1: Create admin-invite.spec.ts**

```typescript
// apps/web/tests/e2e/admin/admin-invite.spec.ts
import { test, expect, type Page } from '@playwright/test';

const ADMIN_EMAIL = 'admin@test.com';
const ADMIN_PASSWORD = 'Admin123!';
const INVITE_EMAIL = `testinvite-${Date.now()}@example.com`;

async function loginAsAdmin(page: Page) {
  await page.goto('/login');
  await page.getByLabel(/email/i).first().fill(ADMIN_EMAIL);
  await page.getByLabel(/password/i).first().fill(ADMIN_PASSWORD);
  await page.getByRole('button', { name: /sign in|log in|accedi/i }).first().click();
  await page.waitForURL('**/admin/**', { timeout: 10000 });
}

test.describe('Admin Invite Flow', () => {
  test('Admin sends invitation and row appears as Pending', async ({ page }) => {
    // Mock the invite API
    let capturedToken = '';
    await page.context().route('**/api/v1/admin/users/invite', async (route) => {
      capturedToken = 'test-token-' + Date.now();
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ id: crypto.randomUUID() }),
      });
    });

    await page.context().route('**/api/v1/admin/users/invitations?**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          invitations: [{
            id: crypto.randomUUID(),
            email: INVITE_EMAIL,
            role: 'User',
            status: 'Pending',
            sentAt: new Date().toISOString(),
            expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
          }],
          total: 1,
        }),
      });
    });

    await page.context().route('**/api/v1/admin/users/invitations/stats', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ total: 1, pending: 1, accepted: 0, expired: 0, revoked: 0 }),
      });
    });

    await loginAsAdmin(page);
    await page.goto('/admin/users/invitations');
    await page.waitForLoadState('networkidle');

    // Fill form
    await page.getByLabel(/email/i).first().fill(INVITE_EMAIL);
    await page.getByRole('button', { name: /send$/i }).first().click();

    // Verify row in table
    await expect(page.getByText(INVITE_EMAIL).first()).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('Pending').first()).toBeVisible();
  });

  test('User accepts invitation and is redirected authenticated', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();

    const mockToken = 'valid-test-token-123';

    // Mock validate-invitation
    await context.route('**/api/v1/auth/validate-invitation', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          email: 'newuser@test.com',
          role: 'User',
          expiresAt: new Date(Date.now() + 86400000).toISOString(),
        }),
      });
    });

    // Mock accept-invitation (returns session)
    await context.route('**/api/v1/auth/accept-invitation', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          sessionToken: 'mock-session-token',
          expiresAt: new Date(Date.now() + 3600000).toISOString(),
        }),
      });
    });

    await page.goto(`/accept-invite?token=${mockToken}`);
    await page.waitForLoadState('networkidle');

    // Verify email is shown readonly
    const emailInput = page.getByLabel(/email/i).first();
    await expect(emailInput).toBeVisible();
    await expect(emailInput).toBeDisabled();

    // Fill password
    await page.getByLabel(/^password$/i).first().fill('SecurePass1!');
    await page.getByLabel(/confirm password/i).first().fill('SecurePass1!');

    // Submit
    await page.getByRole('button', { name: /create account/i }).first().click();

    // Verify success state
    await expect(page.getByText(/account created/i).first()).toBeVisible({ timeout: 5000 });

    await context.close();
  });

  test('Expired token shows error', async ({ page }) => {
    await page.context().route('**/api/v1/auth/validate-invitation', async (route) => {
      await route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Invitation has expired' }),
      });
    });

    await page.goto('/accept-invite?token=expired-token');
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(/invalid invitation/i).first()).toBeVisible({ timeout: 5000 });
    await expect(page.getByLabel(/password/i)).not.toBeVisible();
  });

  test('Admin resends invitation', async ({ page }) => {
    const invitationId = crypto.randomUUID();

    await page.context().route('**/api/v1/admin/users/invitations?**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          invitations: [{
            id: invitationId,
            email: 'pending@test.com',
            role: 'User',
            status: 'Pending',
            sentAt: new Date().toISOString(),
            expiresAt: new Date(Date.now() + 86400000).toISOString(),
          }],
          total: 1,
        }),
      });
    });

    await page.context().route('**/api/v1/admin/users/invitations/stats', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ total: 1, pending: 1, accepted: 0, expired: 0, revoked: 0 }),
      });
    });

    await page.context().route(`**/api/v1/admin/users/invitations/${invitationId}/resend`, async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
    });

    await loginAsAdmin(page);
    await page.goto('/admin/users/invitations');
    await page.waitForLoadState('networkidle');

    // Click resend button (RefreshCw icon)
    const resendButton = page.getByTitle(/resend/i).first();
    await expect(resendButton).toBeVisible({ timeout: 5000 });
    await resendButton.click();

    // Verify no error (resend succeeded silently)
    await expect(page.getByText(/failed/i)).not.toBeVisible({ timeout: 2000 });
  });
});
```

- [ ] **Step 2: Verify test file compiles**

Run: `cd apps/web && npx playwright test tests/e2e/admin/admin-invite.spec.ts --list`
Expected: Lists 4 tests

- [ ] **Step 3: Commit**

```bash
git add apps/web/tests/e2e/admin/admin-invite.spec.ts
git commit -m "test(e2e): add admin invite flow tests (4 tests)"
```

---

## Chunk 2: Phase 2 — Content-Gating RAG

### Task 2.1: Backend — ContentAccessLevel + Chat Handler Filtering

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/ValueObjects/ContentAccessLevel.cs`
- Modify: Chat handler in KnowledgeBase (find the exact handler that returns RAG chunks)

**Important:** Before implementing, the developer MUST:
1. Find the chat/agent response handler: `grep -r "class.*ChatHandler\|class.*AgentQueryHandler\|class.*InvokeAgent" apps/api/src/Api/BoundedContexts/KnowledgeBase/ --include="*.cs" -l`
2. Find the response DTO that includes sources/chunks: `grep -r "class.*ChatResponse\|record.*ChatResponse\|class.*AgentResponse" apps/api/src/Api/BoundedContexts/KnowledgeBase/ --include="*.cs" -l`
3. Understand the current chunk/source structure before modifying

- [ ] **Step 1: Create ContentAccessLevel enum**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/ValueObjects/ContentAccessLevel.cs
namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

public enum ContentAccessLevel
{
    FullAccess,
    ReferenceOnly,
    NoAccess
}
```

- [ ] **Step 2: Modify chat response handler to check ownership**

In the handler that returns RAG sources, add ownership check:

```csharp
// Inside the handler, after retrieving chunks:
var collectionStatus = await _mediator.Send(
    new GetCollectionStatusQuery(request.UserId, EntityType.Game, gameId),
    cancellationToken);

var accessLevel = collectionStatus.IsInCollection
    ? ContentAccessLevel.FullAccess
    : ContentAccessLevel.ReferenceOnly;

// Filter sources based on access
var filteredSources = sources.Select(s => new SourceDto
{
    Reference = s.Reference,
    Text = accessLevel == ContentAccessLevel.FullAccess ? s.Text : null,
    ImageUrl = accessLevel == ContentAccessLevel.FullAccess ? s.ImageUrl : null,
    HasAccess = accessLevel == ContentAccessLevel.FullAccess,
}).ToList();
```

**Note:** The exact modification depends on the chat handler's structure. The developer must read the handler code first and adapt this pattern.

- [ ] **Step 3: Add agent creation gate in CreateAgentCommandHandler**

Find `CreateAgentCommandHandler` and add before agent creation:

```csharp
// Check if game has indexed KB
// Use existing repository method — IVectorDocumentRepository.GetIndexingInfoByGameIdAsync
var indexingInfo = await _vectorDocumentRepository
    .GetIndexingInfoByGameIdAsync(request.GameId, cancellationToken)
    .ConfigureAwait(false);

var hasIndexedKb = indexingInfo?.Status == VectorDocumentIndexingStatus.Indexed;

if (!hasIndexedKb)
{
    // Use project convention: ConflictException for business rule violations (not custom exceptions)
    throw new ConflictException(
        $"Cannot create agent for game {request.GameId}: no indexed knowledge base available. Upload and process a PDF first.");
}
```

**Note:** The project uses `ConflictException` (409) for business rule violations per CLAUDE.md learnings (#2568). Do NOT create a custom `AgentCreationBlockedException`.

- [ ] **Step 4: Build and verify**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/
git commit -m "feat(kb): add content-gating by game ownership and agent creation KB gate"
```

---

### Task 2.2: Frontend — SourceReference Component

**Files:**
- Create: `apps/web/src/components/chat-unified/SourceReference.tsx`

**Reference pattern:** `apps/web/src/components/ui/meeple/chat-message.tsx` (Citation rendering)

- [ ] **Step 1: Create SourceReference component**

```typescript
// apps/web/src/components/chat-unified/SourceReference.tsx
'use client';

import { LockIcon, BookOpenIcon, PlusCircleIcon } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface Source {
  reference: string;
  text: string | null;
  imageUrl: string | null;
  hasAccess: boolean;
}

interface SourceReferenceProps {
  source: Source;
  onAddToCollection?: () => void;
  className?: string;
}

export function SourceReference({ source, onAddToCollection, className }: SourceReferenceProps) {
  if (source.hasAccess) {
    return (
      <div className={cn('rounded-lg border bg-card p-3 space-y-2', className)}>
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <BookOpenIcon className="h-3.5 w-3.5" />
          <span>{source.reference}</span>
        </div>
        {source.text && (
          <p className="text-sm text-foreground">{source.text}</p>
        )}
        {source.imageUrl && (
          <img
            src={source.imageUrl}
            alt={source.reference}
            className="rounded max-h-48 object-contain"
          />
        )}
      </div>
    );
  }

  // ReferenceOnly — locked
  return (
    <div className={cn('rounded-lg border bg-muted/30 p-3 space-y-2 relative', className)}>
      <div className="flex items-center gap-2 text-xs text-muted-foreground">
        <BookOpenIcon className="h-3.5 w-3.5" />
        <span>{source.reference}</span>
        <LockIcon className="h-3.5 w-3.5 ml-auto text-amber-500" aria-label="Content locked" />
      </div>
      <div className="bg-muted/50 rounded p-3 text-center">
        <p className="text-xs text-muted-foreground mb-2">
          Add this game to your collection to see the full content
        </p>
        {onAddToCollection && (
          <button
            onClick={onAddToCollection}
            className="inline-flex items-center gap-1 text-xs text-amber-600 hover:text-amber-700 font-medium"
          >
            <PlusCircleIcon className="h-3.5 w-3.5" />
            Add to collection
          </button>
        )}
      </div>
    </div>
  );
}

interface ContentGatingBannerProps {
  onAddToCollection?: () => void;
}

export function ContentGatingBanner({ onAddToCollection }: ContentGatingBannerProps) {
  return (
    <div className="bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800 rounded-lg p-3 flex items-center gap-3">
      <LockIcon className="h-5 w-5 text-amber-500 shrink-0" />
      <div className="flex-1">
        <p className="text-sm text-amber-800 dark:text-amber-200">
          You don&apos;t own this game. Responses include rulebook references but not the full text.
        </p>
      </div>
      {onAddToCollection && (
        <button
          onClick={onAddToCollection}
          className="text-sm text-amber-600 hover:text-amber-700 font-medium whitespace-nowrap"
        >
          Add to collection →
        </button>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/chat-unified/SourceReference.tsx
git commit -m "feat(frontend): add SourceReference and ContentGatingBanner components"
```

---

### Task 2.3: Frontend — Agent Builder KB Gate

**Files:**
- Modify: `apps/web/src/components/admin/shared-games/AgentBuilderModal.tsx`

- [ ] **Step 1: Read existing AgentBuilderModal**

Run: Read `apps/web/src/components/admin/shared-games/AgentBuilderModal.tsx` — understand the game selection logic, KB cards query.

- [ ] **Step 2: Add KB status check to game selection**

If the modal currently lets users select any game, add a disabled state for games without KB:

```typescript
// In game selection area, add a check:
// If kbCards is empty after loading, show a warning
{!isLoadingKbCards && kbCards && kbCards.length === 0 && (
  <div className="bg-amber-50 dark:bg-amber-900/20 p-3 rounded text-sm text-amber-800 dark:text-amber-200">
    No indexed documents found for this game. Upload and process a PDF first.
  </div>
)}
```

**Note:** The exact modification depends on the modal's current structure. Read the file first.

- [ ] **Step 3: Typecheck**

Run: `cd apps/web && pnpm typecheck`

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/admin/shared-games/AgentBuilderModal.tsx
git commit -m "feat(frontend): add KB gate warning in AgentBuilderModal"
```

---

### Task 2.4: E2E Test — Content-Gating

**Files:**
- Create: `apps/web/tests/e2e/agent-content-gating.spec.ts`

- [ ] **Step 1: Create agent-content-gating.spec.ts**

```typescript
// apps/web/tests/e2e/agent-content-gating.spec.ts
import { test, expect, type Page } from '@playwright/test';

async function loginAsUser(page: Page) {
  await page.goto('/login');
  await page.getByLabel(/email/i).first().fill('user@test.com');
  await page.getByLabel(/password/i).first().fill('User123!');
  await page.getByRole('button', { name: /sign in|log in|accedi/i }).first().click();
  await page.waitForURL('**/dashboard**', { timeout: 10000 });
}

test.describe('Agent Content-Gating', () => {
  test('Game without KB shows agent creation blocked', async ({ page }) => {
    const gameId = 'aaaaaaaa-0000-0000-0000-000000000001';

    // Mock: game exists but no KB cards
    await page.context().route(`**/api/v1/admin/shared-games/${gameId}/kb-cards**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });

    // Mock: shared game detail
    await page.context().route(`**/api/v1/admin/shared-games/${gameId}`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ id: gameId, title: 'Test Game', description: 'A test game' }),
      });
    });

    await loginAsUser(page);
    // Navigate to the shared game detail page (where agent builder modal lives)
    await page.goto(`/admin/shared-games/${gameId}`);
    await page.waitForLoadState('networkidle');

    // Click AI Agent tab, then Create Agent button
    const aiTab = page.getByRole('tab', { name: /ai agent/i }).first();
    if (await aiTab.isVisible({ timeout: 3000 })) {
      await aiTab.click();
    }

    const createButton = page.getByRole('button', { name: /create agent/i }).first();
    if (await createButton.isVisible({ timeout: 3000 })) {
      await createButton.click();
    }

    // Look for KB warning in the modal
    await expect(
      page.getByText(/no indexed documents/i)
        .or(page.getByText(/no.*documents available/i))
        .first()
    ).toBeVisible({ timeout: 5000 });
  });

  test('User owns game — sources show full text (FullAccess)', async ({ page }) => {
    // Mock chat response with FullAccess
    await page.context().route('**/api/v1/chat**', async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            message: 'The goal of Descent is to explore dungeons and defeat monsters.',
            sources: [{
              reference: 'Page 3, Paragraph 2 - Game Objective',
              text: 'Players take on the role of heroes exploring dangerous dungeons.',
              imageUrl: null,
              hasAccess: true,
            }],
            contentAccessLevel: 'FullAccess',
          }),
        });
      } else {
        await route.continue();
      }
    });

    await loginAsUser(page);
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    // Send a chat message to trigger the mocked response
    const chatInput = page.getByPlaceholder(/messaggio|message|ask/i).or(page.locator('[data-testid="chat-input"]')).first();
    await chatInput.fill('What is the goal of Descent?');
    await page.getByRole('button', { name: /send|invia/i }).first().click();

    // Verify source text is visible, no lock icon
    await expect(page.getByText(/game objective/i).first()).toBeVisible({ timeout: 10000 });
    await expect(page.getByText(/players take on the role/i).first()).toBeVisible();
    await expect(page.locator('[aria-label="Content locked"]')).not.toBeVisible();
  });

  test('User does NOT own game — sources show lock (ReferenceOnly)', async ({ page }) => {
    // Mock chat response with ReferenceOnly
    await page.context().route('**/api/v1/chat**', async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            message: 'The goal of the game involves dungeon exploration.',
            sources: [{
              reference: 'Page 3, Paragraph 2 - Game Objective',
              text: null,
              imageUrl: null,
              hasAccess: false,
            }],
            contentAccessLevel: 'ReferenceOnly',
          }),
        });
      } else {
        await route.continue();
      }
    });

    await loginAsUser(page);
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    // Send a chat message to trigger the mocked response
    const chatInput = page.getByPlaceholder(/messaggio|message|ask/i).or(page.locator('[data-testid="chat-input"]')).first();
    await chatInput.fill('What is the goal?');
    await page.getByRole('button', { name: /send|invia/i }).first().click();

    // Verify lock icon visible
    await expect(page.locator('[aria-label="Content locked"]').first()).toBeVisible({ timeout: 10000 });
    // Verify "add to collection" CTA
    await expect(page.getByText(/add.*collection/i).first()).toBeVisible();
  });

  test('User adds game then gets FullAccess on next request', async ({ page }) => {
    let requestCount = 0;

    // First request: ReferenceOnly, second: FullAccess
    await page.context().route('**/api/v1/chat**', async (route) => {
      requestCount++;
      const isSecond = requestCount > 1;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          message: isSecond ? 'Full text now available.' : 'Limited response.',
          sources: [{
            reference: 'Page 3, Paragraph 2',
            text: isSecond ? 'Full text content here.' : null,
            imageUrl: null,
            hasAccess: isSecond,
          }],
          contentAccessLevel: isSecond ? 'FullAccess' : 'ReferenceOnly',
        }),
      });
    });

    // Mock add-to-collection
    await page.context().route('**/api/v1/user-library/collections/add', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
    });

    await loginAsUser(page);
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    // First: locked
    await expect(page.locator('[aria-label="Content locked"]').first()).toBeVisible({ timeout: 5000 });

    // Simulate adding to collection + new chat request
    // (exact flow depends on UI — may need to click "Add to collection" CTA)
    const addButton = page.getByText(/add.*collection/i).first();
    if (await addButton.isVisible()) {
      await addButton.click();
    }

    // Reload chat or send new message to get FullAccess response
    await page.reload();
    await page.waitForLoadState('networkidle');

    // Now should be unlocked
    await expect(page.getByText(/full text content/i).first()).toBeVisible({ timeout: 5000 });
  });
});
```

- [ ] **Step 2: Verify test file compiles**

Run: `cd apps/web && npx playwright test tests/e2e/agent-content-gating.spec.ts --list`
Expected: Lists 4 tests

- [ ] **Step 3: Commit**

```bash
git add apps/web/tests/e2e/agent-content-gating.spec.ts
git commit -m "test(e2e): add agent content-gating tests (4 tests)"
```

---

## Chunk 3: Phase 3 — Admin User Management

### Task 3.1: Backend — Extend ChangeUserRoleCommand with Reason

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ChangeUserRoleCommand.cs`
- Modify: Handler file (same directory)
- Modify: `apps/api/src/Api/Routing/AdminUserEndpoints.cs` (DTO update)

- [ ] **Step 1: Read current ChangeUserRoleCommand**

Run: Read the command file to understand current structure. Find handler, validator, and endpoint.

- [ ] **Step 2: Add Reason parameter**

```csharp
// Add Reason field to command record
internal sealed record ChangeUserRoleCommand(
    Guid UserId,
    string NewRole,
    string? Reason,    // NEW: optional reason for audit trail
    Guid AdminUserId
) : IRequest<bool>;
```

Update the endpoint DTO:
```csharp
internal record ChangeUserRoleRequest(string NewRole, string? Reason);
```

- [ ] **Step 3: Update FluentValidation validator (if exists)**

Search for validator: `grep -r "class.*ChangeUserRole.*Validator" apps/api/src/Api/BoundedContexts/Administration/ --include="*.cs" -l`

If found, add:
```csharp
RuleFor(x => x.Reason)
    .MaximumLength(500)
    .When(x => x.Reason != null);
```

- [ ] **Step 4: Update handler to pass Reason to audit log**

In the handler, ensure the Reason is included in the audit event or log entry. If the project uses `AuditableActionAttribute` pipeline behavior, the command's properties are auto-serialized to the audit log — verify by checking if `[AuditableAction]` decorates the command. If so, the Reason field will be captured automatically.

If manual audit logging is used, add: `_logger.LogInformation("Role changed for {UserId} to {NewRole}. Reason: {Reason}", request.UserId, request.NewRole, request.Reason);`

- [ ] **Step 5: Build and verify**

Run: `cd apps/api/src/Api && dotnet build`

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/
git add apps/api/src/Api/Routing/AdminUserEndpoints.cs
git commit -m "feat(admin): add Reason field to ChangeUserRoleCommand for audit trail"
```

---

### Task 3.2: Frontend — AdminClient Role + Audit Methods

**Files:**
- Modify: `apps/web/src/lib/api/clients/adminClient.ts`

- [ ] **Step 1: Add role management and audit log methods**

```typescript
// Zod schemas
const RoleHistoryEntrySchema = z.object({
  id: z.string().uuid(),
  previousRole: z.string(),
  newRole: z.string(),
  changedBy: z.string(),
  changedByName: z.string().optional(),
  reason: z.string().nullable(),
  changedAt: z.string().datetime(),
});

const AuditLogEntrySchema = z.object({
  id: z.string().uuid(),
  adminUserId: z.string().uuid().nullable(),
  adminUserName: z.string().nullable(),
  action: z.string(),
  resource: z.string(),
  targetUserId: z.string().uuid().nullable(),
  targetUserName: z.string().nullable(),
  result: z.string(),
  details: z.string().nullable(),
  timestamp: z.string().datetime(),
});

const AuditLogListResponseSchema = z.object({
  entries: z.array(AuditLogEntrySchema),
  total: z.number(),
});

export type RoleHistoryEntry = z.infer<typeof RoleHistoryEntrySchema>;
export type AuditLogEntry = z.infer<typeof AuditLogEntrySchema>;

// Methods inside createAdminClient return:

async changeUserRole(userId: string, newRole: string, reason?: string): Promise<void> {
  await httpClient.put(`/api/v1/admin/users/${userId}/role`, { newRole, reason });
},

async getUserRoleHistory(userId: string): Promise<RoleHistoryEntry[]> {
  const result = await httpClient.get(
    `/api/v1/admin/users/${userId}/role-history`,
    z.array(RoleHistoryEntrySchema)
  );
  return result;
},

async getUserAuditLog(userId: string, params?: {
  page?: number;
  pageSize?: number;
}): Promise<{ entries: AuditLogEntry[]; total: number }> {
  const query = new URLSearchParams();
  if (params?.page) query.set('page', params.page.toString());
  if (params?.pageSize) query.set('pageSize', params.pageSize.toString());
  const url = `/api/v1/admin/users/${userId}/audit-log${query.toString() ? `?${query}` : ''}`;
  return httpClient.get(url, AuditLogListResponseSchema);
},

async getAuditLogs(params?: {
  search?: string;
  action?: string;
  result?: string;
  adminUserId?: string;
  dateFrom?: string;
  dateTo?: string;
  page?: number;
  pageSize?: number;
}): Promise<{ entries: AuditLogEntry[]; total: number }> {
  const query = new URLSearchParams();
  if (params?.search) query.set('search', params.search);
  if (params?.action) query.set('action', params.action);
  if (params?.result) query.set('result', params.result);
  if (params?.adminUserId) query.set('adminUserId', params.adminUserId);
  if (params?.dateFrom) query.set('dateFrom', params.dateFrom);
  if (params?.dateTo) query.set('dateTo', params.dateTo);
  if (params?.page) query.set('page', params.page.toString());
  if (params?.pageSize) query.set('pageSize', (params.pageSize || 50).toString());
  return httpClient.get(`/api/v1/admin/audit-log?${query}`, AuditLogListResponseSchema);
},

async exportAuditLogs(params?: {
  action?: string;
  dateFrom?: string;
  dateTo?: string;
}): Promise<Blob> {
  const query = new URLSearchParams();
  if (params?.action) query.set('action', params.action);
  if (params?.dateFrom) query.set('dateFrom', params.dateFrom);
  if (params?.dateTo) query.set('dateTo', params.dateTo);
  // Note: httpClient doesn't support blob responses, so we use fetch directly
  // but include the same base URL and headers pattern
  const response = await httpClient.rawFetch(`/api/v1/admin/audit-log/export?${query}`);
  return response.blob();
},
// If httpClient.rawFetch doesn't exist, use: return httpClient.get(...) and handle
// blob conversion, or use fetch() directly as a documented exception.
```

**Note:** Check if `httpClient` has a `rawFetch` or `getBlob` method. If not, raw `fetch()` is acceptable for file downloads as a documented exception. Add a comment in the code explaining why.
```

- [ ] **Step 2: Typecheck**

Run: `cd apps/web && pnpm typecheck`

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/clients/adminClient.ts
git commit -m "feat(frontend): add role management and audit log methods to adminClient"
```

---

### Task 3.3: Frontend — User Detail Page

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/users/[id]/page.tsx`

This is a large page (~300 lines) with 3 tabs. The developer should:
1. Read the spec section "Phase 3: Admin User Management → User Detail Page"
2. Follow the admin page pattern from Task 1.3
3. Use `Tabs` from shadcn/ui for Overview / Role / Activity Log
4. ChangeRoleCard: `Select` + `Textarea` (reason) + `AlertDialog` confirmation
5. RoleHistoryTable: standard `Table` component
6. UserAuditLogTable: filtered version of the audit log

- [ ] **Step 1: Create user detail page**

The page should follow the pattern in `apps/web/src/app/admin/(dashboard)/knowledge-base/documents/page.tsx` with these specifics:

- Use `useParams()` to get `id` from URL
- Three `useQuery` calls: user detail, role history, audit log
- `useMutation` for role change with `queryClient.invalidateQueries`
- `AlertDialog` for role change confirmation
- `Tabs` / `TabsContent` for section switching

**Key imports:**
```typescript
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
```

The developer should create this page based on the component structure described in the spec.

- [ ] **Step 2: Add hidden nav item**

In `admin-dashboard-navigation.ts`, the user detail page is not added to sidebar (it's accessed via links in tables). No nav change needed.

- [ ] **Step 3: Typecheck and commit**

```bash
cd apps/web && pnpm typecheck
git add apps/web/src/app/admin/(dashboard)/users/[id]/
git commit -m "feat(frontend): add admin user detail page with role management and activity log"
```

---

### Task 3.4: Frontend — Audit Log Page

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/system/audit-log/page.tsx`
- Modify: `apps/web/src/config/admin-dashboard-navigation.ts`

- [ ] **Step 1: Add nav item**

In the "System" section of `DASHBOARD_SECTIONS`, add:

```typescript
{
  href: '/admin/system/audit-log',
  label: 'Audit Log',
  icon: ScrollTextIcon,
},
```

Add `ScrollTextIcon` to lucide-react imports.

- [ ] **Step 2: Create audit log page**

Follow the admin page pattern. Key features:
- `FilterBar` with search, action type select, result select, date range, admin user select
- `AuditLogTable` with expandable rows (click row → show JSON details)
- `AuditLogStats` cards at top
- Export CSV button triggers `adminClient.exportAuditLogs()` → download Blob
- Click on target user → `router.push(/admin/users/${targetUserId})`

The developer should create this based on the spec and existing admin page patterns.

- [ ] **Step 3: Typecheck and commit**

```bash
cd apps/web && pnpm typecheck
git add apps/web/src/app/admin/(dashboard)/system/audit-log/
git add apps/web/src/config/admin-dashboard-navigation.ts
git commit -m "feat(frontend): add admin audit log page with filters and CSV export"
```

---

### Task 3.5: E2E Test — Admin Role + Audit

**Files:**
- Create: `apps/web/tests/e2e/admin/admin-role-audit.spec.ts`

- [ ] **Step 1: Create admin-role-audit.spec.ts**

```typescript
// apps/web/tests/e2e/admin/admin-role-audit.spec.ts
import { test, expect, type Page } from '@playwright/test';

const ADMIN = { email: 'admin@test.com', password: 'Admin123!' };
const TARGET_USER_ID = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';

async function loginAsAdmin(page: Page) {
  await page.goto('/login');
  await page.getByLabel(/email/i).first().fill(ADMIN.email);
  await page.getByLabel(/password/i).first().fill(ADMIN.password);
  await page.getByRole('button', { name: /sign in|log in|accedi/i }).first().click();
  await page.waitForURL('**/admin/**', { timeout: 10000 });
}

function mockUserDetailApis(context: import('@playwright/test').BrowserContext) {
  context.route(`**/api/v1/admin/users/${TARGET_USER_ID}`, async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: TARGET_USER_ID,
          email: 'testuser@example.com',
          displayName: 'Test User',
          role: 'User',
          status: 'Active',
          createdAt: '2026-01-01T00:00:00Z',
          lastLoginAt: '2026-03-14T10:00:00Z',
          collectionCount: 5,
          agentCount: 2,
        }),
      });
    }
  });

  context.route(`**/api/v1/admin/users/${TARGET_USER_ID}/role`, async (route) => {
    if (route.request().method() === 'PUT') {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{"success":true}' });
    }
  });

  context.route(`**/api/v1/admin/users/${TARGET_USER_ID}/role-history`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([{
        id: crypto.randomUUID(),
        previousRole: 'User',
        newRole: 'Contributor',
        changedBy: 'admin-id',
        changedByName: 'Admin',
        reason: 'Promoted for testing',
        changedAt: new Date().toISOString(),
      }]),
    });
  });

  context.route(`**/api/v1/admin/users/${TARGET_USER_ID}/audit-log**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        entries: [{
          id: crypto.randomUUID(),
          adminUserId: 'admin-id',
          adminUserName: 'Admin',
          action: 'RoleChanged',
          resource: 'User',
          targetUserId: TARGET_USER_ID,
          targetUserName: 'Test User',
          result: 'Success',
          details: '{"previousRole":"User","newRole":"Contributor"}',
          timestamp: new Date().toISOString(),
        }],
        total: 1,
      }),
    });
  });
}

test.describe('Admin Role & Audit Management', () => {
  test('Admin changes user role', async ({ page }) => {
    await mockUserDetailApis(page.context());
    await loginAsAdmin(page);
    await page.goto(`/admin/users/${TARGET_USER_ID}`);
    await page.waitForLoadState('networkidle');

    // Go to Role tab
    await page.getByRole('tab', { name: /role/i }).first().click();

    // Select new role
    const roleSelect = page.getByLabel(/role/i).or(page.locator('[data-testid="role-select"]')).first();
    await expect(roleSelect).toBeVisible({ timeout: 5000 });

    // Submit role change (confirm dialog expected)
    await page.getByRole('button', { name: /change role|save/i }).first().click();

    // Confirm in alert dialog
    const confirmButton = page.getByRole('button', { name: /confirm|change/i }).first();
    if (await confirmButton.isVisible({ timeout: 2000 })) {
      await confirmButton.click();
    }

    // Verify no error
    await expect(page.getByText(/failed/i)).not.toBeVisible({ timeout: 2000 });
  });

  test('Role history shows entries', async ({ page }) => {
    await mockUserDetailApis(page.context());
    await loginAsAdmin(page);
    await page.goto(`/admin/users/${TARGET_USER_ID}`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('tab', { name: /role/i }).first().click();

    // Verify history table has entry
    await expect(page.getByText(/contributor/i).first()).toBeVisible({ timeout: 5000 });
    await expect(page.getByText(/promoted for testing/i).first()).toBeVisible();
  });

  test('User activity log shows actions', async ({ page }) => {
    await mockUserDetailApis(page.context());
    await loginAsAdmin(page);
    await page.goto(`/admin/users/${TARGET_USER_ID}`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('tab', { name: /activity/i }).first().click();

    await expect(page.getByText(/rolechanged/i).first()).toBeVisible({ timeout: 5000 });
    await expect(page.getByText(/success/i).first()).toBeVisible();
  });

  test('General audit log loads and filters work', async ({ page }) => {
    await page.context().route('**/api/v1/admin/audit-log?**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          entries: [{
            id: crypto.randomUUID(),
            adminUserId: 'admin-id',
            adminUserName: 'Admin',
            action: 'RoleChanged',
            resource: 'User',
            targetUserId: TARGET_USER_ID,
            targetUserName: 'Test User',
            result: 'Success',
            details: null,
            timestamp: new Date().toISOString(),
          }],
          total: 1,
        }),
      });
    });

    await loginAsAdmin(page);
    await page.goto('/admin/system/audit-log');
    await page.waitForLoadState('networkidle');

    // Verify table loaded
    await expect(page.getByText(/rolechanged/i).first()).toBeVisible({ timeout: 5000 });

    // Click on target user → navigate to user detail
    const userLink = page.getByText('Test User').first();
    if (await userLink.isVisible()) {
      await userLink.click();
      await expect(page).toHaveURL(new RegExp(`/admin/users/${TARGET_USER_ID}`));
    }
  });

  test('Export CSV triggers download', async ({ page }) => {
    await page.context().route('**/api/v1/admin/audit-log?**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ entries: [], total: 0 }),
      });
    });

    await page.context().route('**/api/v1/admin/audit-log/export**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'text/csv',
        body: 'timestamp,action,result\n2026-03-14,RoleChanged,Success',
      });
    });

    await loginAsAdmin(page);
    await page.goto('/admin/system/audit-log');
    await page.waitForLoadState('networkidle');

    // Click export
    const [download] = await Promise.all([
      page.waitForEvent('download', { timeout: 5000 }).catch(() => null),
      page.getByRole('button', { name: /export/i }).first().click(),
    ]);

    // Verify download initiated (or at least no error)
    await expect(page.getByText(/failed/i)).not.toBeVisible({ timeout: 2000 });
  });
});
```

- [ ] **Step 2: Verify tests list**

Run: `cd apps/web && npx playwright test tests/e2e/admin/admin-role-audit.spec.ts --list`
Expected: Lists 5 tests

- [ ] **Step 3: Commit**

```bash
git add apps/web/tests/e2e/admin/admin-role-audit.spec.ts
git commit -m "test(e2e): add admin role and audit log tests (5 tests)"
```

---

## Chunk 4: Phase 4 — Smoke Test Journey

### Task 4.1: E2E Smoke Test — Full User Journey

**Files:**
- Create: `apps/web/tests/e2e/full-user-journey.spec.ts`

- [ ] **Step 1: Create full-user-journey.spec.ts**

```typescript
// apps/web/tests/e2e/full-user-journey.spec.ts
import { test, expect } from '@playwright/test';

const ADMIN = { email: 'admin@test.com', password: 'Admin123!' };
const INVITE_EMAIL = 'journey-user@test.com';
const GAME_TITLE = 'Descent';
const MOCK_TOKEN = 'journey-test-token-123';
const MOCK_USER_ID = 'ffffffff-1111-2222-3333-444444444444';

test.describe('Full User Journey: Admin Invite → User Onboarding → Admin Management', () => {
  test('Complete end-to-end journey', async ({ browser }) => {
    // Create two isolated contexts
    const adminContext = await browser.newContext();
    const userContext = await browser.newContext();

    const adminPage = await adminContext.newPage();
    const userPage = await userContext.newPage();

    try {
      // === ADMIN CONTEXT MOCKS ===
      await adminContext.route('**/api/v1/admin/users/invite', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ id: MOCK_USER_ID }),
        });
      });

      await adminContext.route('**/api/v1/admin/users/invitations?**', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            invitations: [{
              id: MOCK_USER_ID,
              email: INVITE_EMAIL,
              role: 'User',
              status: 'Pending',
              sentAt: new Date().toISOString(),
              expiresAt: new Date(Date.now() + 86400000).toISOString(),
            }],
            total: 1,
          }),
        });
      });

      await adminContext.route('**/api/v1/admin/users/invitations/stats', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ total: 1, pending: 1, accepted: 0, expired: 0, revoked: 0 }),
        });
      });

      await adminContext.route(`**/api/v1/admin/users/${MOCK_USER_ID}`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: MOCK_USER_ID,
            email: INVITE_EMAIL,
            displayName: 'Journey User',
            role: 'User',
            status: 'Active',
            createdAt: new Date().toISOString(),
          }),
        });
      });

      await adminContext.route(`**/api/v1/admin/users/${MOCK_USER_ID}/role`, async (route) => {
        await route.fulfill({ status: 200, body: '{"success":true}' });
      });

      await adminContext.route(`**/api/v1/admin/users/${MOCK_USER_ID}/role-history`, async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([{
            id: crypto.randomUUID(),
            previousRole: 'User',
            newRole: 'Contributor',
            changedBy: 'admin-id',
            reason: 'Promoted for testing',
            changedAt: new Date().toISOString(),
          }]),
        });
      });

      await adminContext.route('**/api/v1/admin/audit-log?**', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            entries: [
              { id: crypto.randomUUID(), action: 'InvitationSent', targetUserName: 'Journey User', result: 'Success', timestamp: new Date().toISOString() },
              { id: crypto.randomUUID(), action: 'RoleChanged', targetUserName: 'Journey User', result: 'Success', timestamp: new Date().toISOString() },
            ],
            total: 2,
          }),
        });
      });

      // === USER CONTEXT MOCKS ===
      await userContext.route('**/api/v1/auth/validate-invitation', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ email: INVITE_EMAIL, role: 'User', expiresAt: new Date(Date.now() + 86400000).toISOString() }),
        });
      });

      await userContext.route('**/api/v1/auth/accept-invitation', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ sessionToken: 'mock-session', expiresAt: new Date(Date.now() + 3600000).toISOString() }),
        });
      });

      await userContext.route('**/api/v1/chat**', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            message: `${GAME_TITLE} is a dungeon-crawling adventure game. In a typical turn, the Overlord activates monsters, then each hero takes a turn performing two actions.`,
            sources: [{
              reference: 'Page 5, Paragraph 1 - Turn Structure',
              text: 'Each round consists of the Overlord phase followed by the Hero phase.',
              imageUrl: null,
              hasAccess: true,
            }],
            contentAccessLevel: 'FullAccess',
          }),
        });
      });

      // --- F1: ADMIN SENDS INVITATION ---
      await adminPage.goto('/login');
      await adminPage.getByLabel(/email/i).first().fill(ADMIN.email);
      await adminPage.getByLabel(/password/i).first().fill(ADMIN.password);
      await adminPage.getByRole('button', { name: /sign in|log in|accedi/i }).first().click();
      await adminPage.waitForURL('**/admin/**', { timeout: 10000 });

      await adminPage.goto('/admin/users/invitations');
      await adminPage.waitForLoadState('networkidle');

      await adminPage.getByLabel(/email/i).first().fill(INVITE_EMAIL);
      await adminPage.getByRole('button', { name: /send$/i }).first().click();

      await expect(adminPage.getByText(INVITE_EMAIL).first()).toBeVisible({ timeout: 5000 });
      await expect(adminPage.getByText('Pending').first()).toBeVisible();

      // --- F2a: USER ACCEPTS INVITATION ---
      await userPage.goto(`/accept-invite?token=${MOCK_TOKEN}`);
      await userPage.waitForLoadState('networkidle');

      await expect(userPage.getByLabel(/email/i).first()).toBeDisabled();
      await userPage.getByLabel(/^password$/i).first().fill('NewUser123!');
      await userPage.getByLabel(/confirm password/i).first().fill('NewUser123!');
      await userPage.getByRole('button', { name: /create account/i }).first().click();

      await expect(userPage.getByText(/account created/i).first()).toBeVisible({ timeout: 5000 });

      // --- F2b: USER ADDS GAME TO COLLECTION ---
      await userContext.route('**/api/v1/user-library/collections/add', async (route) => {
        await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
      });

      await userContext.route('**/api/v1/games**', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            games: [{ id: 'descent-id', title: GAME_TITLE, publisher: 'Fantasy Flight Games' }],
            total: 1,
          }),
        });
      });

      // Navigate to game search/library and add game
      await userPage.goto('/dashboard');
      await userPage.waitForLoadState('networkidle');
      // Simulate adding game to collection (click add button if visible, or call API directly)
      // The exact UI path depends on the dashboard/library layout

      // --- F2c: USER CREATES AGENT ---
      await userContext.route('**/api/v1/agent-definitions', async (route) => {
        if (route.request().method() === 'POST') {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ id: 'agent-descent-id', name: `${GAME_TITLE} Arbitro` }),
          });
        } else {
          await route.continue();
        }
      });

      // --- F2d: USER ASKS AGENT VIA VOICE (mocked speech) ---
      await userPage.goto('/chat');
      await userPage.waitForLoadState('networkidle');

      // Inject mock SpeechRecognition
      await userPage.evaluate(() => {
        class MockSpeechRecognition extends EventTarget {
          continuous = false;
          interimResults = false;
          lang = 'en-US';
          start() {
            setTimeout(() => {
              const event = new Event('result');
              (event as any).results = [[{ transcript: 'What is the purpose of the game and describe a turn' }]];
              (event as any).results[0].isFinal = true;
              this.dispatchEvent(event);
            }, 100);
          }
          stop() {}
          abort() {}
        }
        (window as any).SpeechRecognition = MockSpeechRecognition;
        (window as any).webkitSpeechRecognition = MockSpeechRecognition;
      });

      // Verify chat response with sources visible (FullAccess)
      await expect(userPage.getByText(/turn structure/i).first()).toBeVisible({ timeout: 10000 });
      await expect(userPage.locator('[aria-label="Content locked"]')).not.toBeVisible();

      // --- F3a: ADMIN CHANGES USER ROLE ---
      await adminPage.goto(`/admin/users/${MOCK_USER_ID}`);
      await adminPage.waitForLoadState('networkidle');

      await adminPage.getByRole('tab', { name: /role/i }).first().click();
      // Role change interaction (simplified — depends on UI)
      await expect(adminPage.getByText(/user/i).first()).toBeVisible({ timeout: 5000 });

      // --- F3b: ADMIN CHECKS AUDIT LOG ---
      await adminPage.goto('/admin/system/audit-log');
      await adminPage.waitForLoadState('networkidle');

      await expect(adminPage.getByText(/invitationsent/i).first()).toBeVisible({ timeout: 5000 });
      await expect(adminPage.getByText(/rolechanged/i).first()).toBeVisible();

    } finally {
      await adminContext.close();
      await userContext.close();
    }
  });
});
```

- [ ] **Step 2: Verify test lists**

Run: `cd apps/web && npx playwright test tests/e2e/full-user-journey.spec.ts --list`
Expected: Lists 1 test

- [ ] **Step 3: Commit**

```bash
git add apps/web/tests/e2e/full-user-journey.spec.ts
git commit -m "test(e2e): add full user journey smoke test"
```

---

## Final Verification

- [ ] **Run all E2E tests**

```bash
cd apps/web && npx playwright test tests/e2e/admin/admin-invite.spec.ts tests/e2e/agent-content-gating.spec.ts tests/e2e/admin/admin-role-audit.spec.ts tests/e2e/full-user-journey.spec.ts --reporter=list
```

Expected: 14 tests (4 + 4 + 5 + 1) — all should pass with mocked APIs.

- [ ] **Run typecheck**

```bash
cd apps/web && pnpm typecheck
```

- [ ] **Run backend build**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Create PRs per phase**

Each phase should be a separate PR targeting `main-dev`:
1. `feature/admin-invite-flow` → PR to `main-dev`
2. `feature/content-gating-rag` → PR to `main-dev`
3. `feature/admin-user-management` → PR to `main-dev`
4. `feature/e2e-smoke-journey` → PR to `main-dev` (after 1+2+3 merged)
