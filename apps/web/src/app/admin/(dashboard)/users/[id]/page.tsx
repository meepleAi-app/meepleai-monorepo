'use client';

import { useState } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { format } from 'date-fns';
import {
  ArrowLeftIcon,
  ShieldIcon,
  UserIcon,
  CalendarIcon,
  MailIcon,
  ActivityIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  ChevronsUpDownIcon,
  ClockIcon,
  AlertCircleIcon,
} from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
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
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

const AVAILABLE_ROLES = ['User', 'Editor', 'Admin'];

function getRoleBadgeClass(role: string) {
  switch (role) {
    case 'Admin':
      return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300';
    case 'Contributor':
      return 'bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-300';
    default:
      return 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300';
  }
}

function getStatusBadge(isSuspended: boolean) {
  if (isSuspended) {
    return (
      <Badge variant="destructive" className="ml-2">
        Suspended
      </Badge>
    );
  }
  return (
    <Badge
      variant="default"
      className="ml-2 bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300"
    >
      Active
    </Badge>
  );
}

// ========== User Header ==========

function UserHeader({
  user,
}: {
  user: {
    displayName: string;
    email: string;
    role: string;
    isSuspended?: boolean;
    createdAt: string;
  };
}) {
  return (
    <div className="flex items-center gap-4">
      <div className="h-14 w-14 rounded-full bg-amber-100 dark:bg-amber-900/30 flex items-center justify-center">
        <UserIcon className="h-7 w-7 text-amber-700 dark:text-amber-400" />
      </div>
      <div className="flex-1">
        <div className="flex items-center gap-2 flex-wrap">
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            {user.displayName}
          </h1>
          <Badge variant="default" className={getRoleBadgeClass(user.role)}>
            {user.role}
          </Badge>
          {getStatusBadge(user.isSuspended ?? false)}
        </div>
        <p className="text-sm text-muted-foreground mt-0.5 flex items-center gap-1.5">
          <MailIcon className="h-3.5 w-3.5" />
          {user.email}
          <span className="mx-1">|</span>
          <CalendarIcon className="h-3.5 w-3.5" />
          Joined {format(new Date(user.createdAt), 'MMM d, yyyy')}
        </p>
      </div>
    </div>
  );
}

// ========== Overview Tab ==========

function OverviewTab({
  user,
}: {
  user: {
    createdAt: string;
    lastSeenAt?: string | null;
    isTwoFactorEnabled?: boolean;
    tier?: string;
    tokenUsage?: number;
    tokenLimit?: number;
  };
}) {
  return (
    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
      <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
        <CardHeader className="pb-2">
          <CardTitle className="font-quicksand text-base">Account Info</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3 text-sm">
          <div className="flex justify-between">
            <span className="text-muted-foreground">Created</span>
            <span className="font-medium">{format(new Date(user.createdAt), 'PPP')}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Last Seen</span>
            <span className="font-medium">
              {user.lastSeenAt ? format(new Date(user.lastSeenAt), 'PPP p') : 'Never'}
            </span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">2FA Enabled</span>
            <span className="font-medium">{user.isTwoFactorEnabled ? 'Yes' : 'No'}</span>
          </div>
        </CardContent>
      </Card>

      <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
        <CardHeader className="pb-2">
          <CardTitle className="font-quicksand text-base">Usage</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3 text-sm">
          <div className="flex justify-between">
            <span className="text-muted-foreground">Tier</span>
            <Badge variant="outline">{user.tier ?? 'Free'}</Badge>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Token Usage</span>
            <span className="font-medium">
              {(user.tokenUsage ?? 0).toLocaleString()} /{' '}
              {(user.tokenLimit ?? 10_000).toLocaleString()}
            </span>
          </div>
          <div className="mt-2">
            <div className="h-2 rounded-full bg-slate-200 dark:bg-zinc-700 overflow-hidden">
              <div
                className="h-full rounded-full bg-amber-500 dark:bg-amber-400 transition-all"
                style={{
                  width: `${Math.min(100, ((user.tokenUsage ?? 0) / (user.tokenLimit ?? 10_000)) * 100)}%`,
                }}
              />
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

// ========== Change Role Card ==========

function ChangeRoleCard({ userId, currentRole }: { userId: string; currentRole: string }) {
  const queryClient = useQueryClient();
  const [newRole, setNewRole] = useState('');
  const [reason, setReason] = useState('');

  const changeRoleMutation = useMutation({
    mutationFn: ({ role, reasonText }: { role: string; reasonText?: string }) =>
      adminClient.changeUserRole(userId, role, reasonText || undefined),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'users', userId] });
      queryClient.invalidateQueries({ queryKey: ['admin', 'users', userId, 'role-history'] });
      setNewRole('');
      setReason('');
    },
  });

  const canSubmit = newRole && newRole !== currentRole;

  return (
    <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
      <CardHeader className="pb-2">
        <CardTitle className="font-quicksand text-base flex items-center gap-2">
          <ChevronsUpDownIcon className="h-4 w-4" />
          Change Role
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="space-y-2">
          <Label htmlFor="new-role">New Role</Label>
          <Select value={newRole} onValueChange={setNewRole}>
            <SelectTrigger id="new-role" className="bg-white/70 dark:bg-zinc-800/70">
              <SelectValue placeholder="Select a role..." />
            </SelectTrigger>
            <SelectContent>
              {AVAILABLE_ROLES.map(role => (
                <SelectItem key={role} value={role} disabled={role === currentRole}>
                  {role} {role === currentRole ? '(current)' : ''}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-2">
          <Label htmlFor="reason">Reason (optional)</Label>
          <Textarea
            id="reason"
            value={reason}
            onChange={e => setReason(e.target.value)}
            placeholder="Why is this role change needed?"
            rows={3}
            className="bg-white/70 dark:bg-zinc-800/70"
          />
        </div>

        <AlertDialog>
          <AlertDialogTrigger asChild>
            <Button disabled={!canSubmit || changeRoleMutation.isPending} className="w-full">
              {changeRoleMutation.isPending ? 'Changing...' : 'Change Role'}
            </Button>
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Confirm Role Change</AlertDialogTitle>
              <AlertDialogDescription>
                Change user role from <strong>{currentRole}</strong> to <strong>{newRole}</strong>?
                {reason && (
                  <>
                    <br />
                    <br />
                    Reason: {reason}
                  </>
                )}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction
                onClick={() => changeRoleMutation.mutate({ role: newRole, reasonText: reason })}
              >
                Confirm
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        {changeRoleMutation.isError && (
          <div className="flex items-center gap-2 text-sm text-red-600 dark:text-red-400">
            <AlertCircleIcon className="h-4 w-4 shrink-0" />
            {changeRoleMutation.error instanceof Error
              ? changeRoleMutation.error.message
              : 'Failed to change role'}
          </div>
        )}

        {changeRoleMutation.isSuccess && (
          <p className="text-sm text-green-600 dark:text-green-400">Role changed successfully.</p>
        )}
      </CardContent>
    </Card>
  );
}

// ========== Role History Table ==========

function RoleHistoryTable({ userId }: { userId: string }) {
  const { data: roleHistory, isLoading } = useQuery({
    queryKey: ['admin', 'users', userId, 'role-history'],
    queryFn: () => adminClient.getUserRoleHistory(userId),
  });

  if (isLoading) {
    return (
      <div className="space-y-3">
        {Array.from({ length: 3 }).map((_, i) => (
          <Skeleton key={i} className="h-10 w-full" />
        ))}
      </div>
    );
  }

  if (!roleHistory || roleHistory.length === 0) {
    return (
      <p className="text-sm text-muted-foreground py-4 text-center">No role changes recorded.</p>
    );
  }

  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/40 overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-200/60 dark:border-zinc-700/40">
              <th className="text-left p-3 font-medium text-muted-foreground">Previous Role</th>
              <th className="text-left p-3 font-medium text-muted-foreground">New Role</th>
              <th className="text-left p-3 font-medium text-muted-foreground">Changed By</th>
              <th className="text-left p-3 font-medium text-muted-foreground">Date</th>
            </tr>
          </thead>
          <tbody>
            {roleHistory.map((entry, i) => (
              <tr
                key={i}
                className="border-b border-slate-100 dark:border-zinc-800/60 hover:bg-slate-50/50 dark:hover:bg-zinc-700/30"
              >
                <td className="p-3">
                  <Badge variant="outline" className={getRoleBadgeClass(entry.oldRole)}>
                    {entry.oldRole}
                  </Badge>
                </td>
                <td className="p-3">
                  <Badge variant="outline" className={getRoleBadgeClass(entry.newRole)}>
                    {entry.newRole}
                  </Badge>
                </td>
                <td className="p-3 text-muted-foreground">{entry.changedByDisplayName}</td>
                <td className="p-3 text-muted-foreground">
                  {format(new Date(entry.changedAt), 'MMM d, yyyy HH:mm')}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ========== Audit Log Table ==========

function UserAuditLogTable({ userId }: { userId: string }) {
  const [page, setPage] = useState(0);
  const pageSize = 20;

  const { data: auditLog, isLoading } = useQuery({
    queryKey: ['admin', 'users', userId, 'audit-log', { page }],
    queryFn: () =>
      adminClient.getUserAuditLog(userId, { limit: pageSize, offset: page * pageSize }),
  });

  const [expandedId, setExpandedId] = useState<string | null>(null);

  const entries = auditLog?.entries ?? [];
  const totalCount = auditLog?.totalCount ?? 0;
  const totalPages = Math.ceil(totalCount / pageSize);

  if (isLoading) {
    return (
      <div className="space-y-3">
        {Array.from({ length: 5 }).map((_, i) => (
          <Skeleton key={i} className="h-10 w-full" />
        ))}
      </div>
    );
  }

  if (entries.length === 0) {
    return (
      <p className="text-sm text-muted-foreground py-4 text-center">No audit log entries found.</p>
    );
  }

  function getResultBadge(result: string) {
    if (result === 'Success') {
      return (
        <Badge
          variant="default"
          className="bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300"
        >
          Success
        </Badge>
      );
    }
    if (result === 'Failure' || result === 'Error') {
      return <Badge variant="destructive">{result}</Badge>;
    }
    return <Badge variant="outline">{result}</Badge>;
  }

  return (
    <div className="space-y-3">
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/40 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-200/60 dark:border-zinc-700/40">
                <th className="text-left p-3 font-medium text-muted-foreground">Timestamp</th>
                <th className="text-left p-3 font-medium text-muted-foreground">Action</th>
                <th className="text-left p-3 font-medium text-muted-foreground">Resource</th>
                <th className="text-left p-3 font-medium text-muted-foreground">Result</th>
                <th className="text-left p-3 font-medium text-muted-foreground">Details</th>
              </tr>
            </thead>
            <tbody>
              {entries.map(entry => (
                <tr
                  key={entry.id}
                  className="border-b border-slate-100 dark:border-zinc-800/60 hover:bg-slate-50/50 dark:hover:bg-zinc-700/30 cursor-pointer"
                  onClick={() => setExpandedId(expandedId === entry.id ? null : entry.id)}
                >
                  <td className="p-3 text-muted-foreground whitespace-nowrap">
                    <span className="flex items-center gap-1.5">
                      <ClockIcon className="h-3.5 w-3.5" />
                      {format(new Date(entry.createdAt), 'MMM d, HH:mm:ss')}
                    </span>
                  </td>
                  <td className="p-3 font-medium">{entry.action}</td>
                  <td className="p-3 text-muted-foreground">{entry.resource}</td>
                  <td className="p-3">{getResultBadge(entry.result)}</td>
                  <td className="p-3">
                    {entry.details ? (
                      <Button variant="ghost" size="sm" className="h-6 px-2 text-xs">
                        {expandedId === entry.id ? 'Hide' : 'Show'}
                      </Button>
                    ) : (
                      <span className="text-muted-foreground">--</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Expanded detail rows */}
        {entries
          .filter(e => e.id === expandedId && e.details)
          .map(entry => (
            <div
              key={`detail-${entry.id}`}
              className="px-4 py-3 border-t border-slate-200/60 dark:border-zinc-700/40 bg-slate-50/50 dark:bg-zinc-900/30"
            >
              <p className="text-xs font-medium text-muted-foreground mb-1">Details</p>
              <pre className="text-xs whitespace-pre-wrap text-foreground/80 font-mono">
                {entry.details}
              </pre>
            </div>
          ))}
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            Showing {page * pageSize + 1}--{Math.min((page + 1) * pageSize, totalCount)} of{' '}
            {totalCount}
          </p>
          <div className="flex items-center gap-1">
            <Button
              variant="outline"
              size="icon"
              className="h-8 w-8"
              disabled={page <= 0}
              onClick={() => setPage(p => p - 1)}
            >
              <ChevronLeftIcon className="h-4 w-4" />
            </Button>
            <span className="px-3 text-sm">
              Page {page + 1} of {totalPages}
            </span>
            <Button
              variant="outline"
              size="icon"
              className="h-8 w-8"
              disabled={page + 1 >= totalPages}
              onClick={() => setPage(p => p + 1)}
            >
              <ChevronRightIcon className="h-4 w-4" />
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

// ========== Main Page ==========

export default function UserDetailPage() {
  const params = useParams();
  const userId = params.id as string;

  const {
    data: user,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['admin', 'users', userId],
    queryFn: () => adminClient.getUserDetail(userId),
    enabled: !!userId,
  });

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-48" />
        <div className="flex items-center gap-4">
          <Skeleton className="h-14 w-14 rounded-full" />
          <div className="space-y-2">
            <Skeleton className="h-6 w-40" />
            <Skeleton className="h-4 w-60" />
          </div>
        </div>
        <Skeleton className="h-[300px] w-full" />
      </div>
    );
  }

  if (error || !user) {
    return (
      <div className="space-y-6">
        <Link
          href="/admin/users"
          className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeftIcon className="h-4 w-4" />
          Back to Users
        </Link>
        <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/40 rounded-lg p-6 text-center">
          <AlertCircleIcon className="h-8 w-8 text-red-500 mx-auto mb-2" />
          <p className="font-medium text-red-800 dark:text-red-200">Failed to load user</p>
          <p className="text-sm text-red-600 dark:text-red-400 mt-1">
            {error instanceof Error ? error.message : 'User not found or access denied.'}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Back link */}
      <Link
        href="/admin/users"
        className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeftIcon className="h-4 w-4" />
        Back to Users
      </Link>

      {/* Header */}
      <UserHeader user={user} />

      {/* Tabs */}
      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview" className="gap-1.5">
            <UserIcon className="h-3.5 w-3.5" />
            Overview
          </TabsTrigger>
          <TabsTrigger value="role" className="gap-1.5">
            <ShieldIcon className="h-3.5 w-3.5" />
            Role
          </TabsTrigger>
          <TabsTrigger value="activity" className="gap-1.5">
            <ActivityIcon className="h-3.5 w-3.5" />
            Activity Log
          </TabsTrigger>
        </TabsList>

        <TabsContent value="overview">
          <OverviewTab user={user} />
        </TabsContent>

        <TabsContent value="role" className="space-y-6">
          <ChangeRoleCard userId={userId} currentRole={user.role} />
          <div>
            <h3 className="font-quicksand text-lg font-semibold mb-3">Role History</h3>
            <RoleHistoryTable userId={userId} />
          </div>
        </TabsContent>

        <TabsContent value="activity">
          <UserAuditLogTable userId={userId} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
