'use client';

import { useState } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  RefreshCw,
  Send,
  Clock,
  XCircle,
  Loader2,
  CheckCircle,
  Search,
  Mail,
  RotateCcw,
} from 'lucide-react';

import { EmptyFeatureState } from '@/components/admin/EmptyFeatureState';
import { toast } from '@/components/layout';
import { createAdminClient, type EmailQueueItem } from '@/lib/api/clients/adminClient';
import { isNotFoundError } from '@/lib/api/core/errors';
import { HttpClient } from '@/lib/api/core/httpClient';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

function StatCard({
  label,
  value,
  icon: Icon,
  color,
}: {
  label: string;
  value: string | number;
  icon: React.ComponentType<{ className?: string }>;
  color: string;
}) {
  return (
    <div className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm p-5">
      <div className="flex items-center gap-3">
        <div className={`flex h-10 w-10 items-center justify-center rounded-xl ${color}`}>
          <Icon className="h-5 w-5" />
        </div>
        <div>
          <p className="font-quicksand text-2xl font-bold text-foreground">{value}</p>
          <p className="text-xs text-muted-foreground">{label}</p>
        </div>
      </div>
    </div>
  );
}

function formatDate(dateStr: string | null | undefined): string {
  if (!dateStr) return '—';
  try {
    return new Date(dateStr).toLocaleString();
  } catch {
    return dateStr;
  }
}

function formatRelative(dateStr: string | null | undefined): string {
  if (!dateStr) return '—';
  try {
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    if (diffMins < 1) return 'just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours}h ago`;
    const diffDays = Math.floor(diffHours / 24);
    return `${diffDays}d ago`;
  } catch {
    return dateStr;
  }
}

export function EmailManagementTab() {
  const queryClient = useQueryClient();
  const [retryingId, setRetryingId] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [testEmailTo, setTestEmailTo] = useState('');

  // Queries
  const statsQuery = useQuery({
    queryKey: ['admin', 'email-stats'],
    queryFn: () => adminClient.getEmailQueueStats(),
    refetchInterval: 30000, // Auto-refresh every 30s
    retry: (failureCount, err) => {
      if (isNotFoundError(err)) return false;
      return failureCount < 3;
    },
  });

  const deadLetterQuery = useQuery({
    queryKey: ['admin', 'dead-letter-emails'],
    queryFn: () => adminClient.getDeadLetterEmails({ take: 50 }),
  });

  const historyQuery = useQuery({
    queryKey: ['admin', 'email-history', searchTerm],
    queryFn: () => adminClient.getEmailHistory({ take: 20, search: searchTerm || undefined }),
  });

  // Mutations
  const retryMutation = useMutation({
    mutationFn: (id: string) => adminClient.retryEmail(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'email-stats'] });
      queryClient.invalidateQueries({ queryKey: ['admin', 'dead-letter-emails'] });
      toast.success('Email queued for retry');
    },
    onError: () => toast.error('Failed to retry email'),
  });

  const retryAllMutation = useMutation({
    mutationFn: () => adminClient.retryAllDeadLetters(),
    onSuccess: count => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'email-stats'] });
      queryClient.invalidateQueries({ queryKey: ['admin', 'dead-letter-emails'] });
      toast.success(`${count} emails queued for retry`);
    },
    onError: () => toast.error('Failed to retry dead letters'),
  });

  const sendTestMutation = useMutation({
    mutationFn: (to: string) => adminClient.sendTestEmail(to),
    onSuccess: () => {
      toast.success('Test email sent');
      setTestEmailTo('');
    },
    onError: () => toast.error('Failed to send test email'),
  });

  async function handleRetry(email: EmailQueueItem) {
    setRetryingId(email.id);
    try {
      await retryMutation.mutateAsync(email.id);
    } finally {
      setRetryingId(null);
    }
  }

  const stats = statsQuery.data;
  const deadLetters = deadLetterQuery.data?.items ?? [];
  const history = historyQuery.data?.items ?? [];

  // Show fallback if any primary query returns 404 (endpoint not implemented)
  if (isNotFoundError(statsQuery.error)) {
    return (
      <EmptyFeatureState
        title="Funzionalità non disponibile"
        description="Endpoint email management non ancora implementato nel backend."
      />
    );
  }

  return (
    <div className="space-y-6">
      {/* Stats cards */}
      <div className="grid gap-4 sm:grid-cols-4">
        <StatCard
          label="Sent (24h)"
          value={stats?.sentLast24Hours ?? '—'}
          icon={Send}
          color="bg-emerald-100/80 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400"
        />
        <StatCard
          label="Sent (1h)"
          value={stats?.sentLastHour ?? '—'}
          icon={CheckCircle}
          color="bg-blue-100/80 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400"
        />
        <StatCard
          label="Pending"
          value={stats?.pendingCount ?? '—'}
          icon={Clock}
          color="bg-amber-100/80 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400"
        />
        <StatCard
          label="Dead Letter"
          value={stats?.deadLetterCount ?? '—'}
          icon={XCircle}
          color="bg-red-100/80 dark:bg-red-900/30 text-red-700 dark:text-red-400"
        />
      </div>

      {/* Send Test Email */}
      <div className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm p-5">
        <h2 className="font-quicksand text-sm font-semibold text-foreground mb-3">
          Send Test Email
        </h2>
        <div className="flex gap-2">
          <input
            type="email"
            value={testEmailTo}
            onChange={e => setTestEmailTo(e.target.value)}
            placeholder="recipient@example.com"
            className="flex-1 rounded-lg border border-slate-200/60 dark:border-zinc-700/40 bg-white/50 dark:bg-zinc-900/50 px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-amber-400/40"
          />
          <button
            onClick={() => testEmailTo && sendTestMutation.mutate(testEmailTo)}
            disabled={!testEmailTo || sendTestMutation.isPending}
            className="inline-flex items-center gap-1.5 rounded-lg bg-amber-100/80 dark:bg-amber-900/30 px-4 py-2 text-sm font-semibold text-amber-900 dark:text-amber-300 transition-colors hover:bg-amber-200/80 dark:hover:bg-amber-900/50 disabled:opacity-60"
          >
            {sendTestMutation.isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Mail className="h-4 w-4" />
            )}
            Send Test
          </button>
        </div>
      </div>

      {/* Dead letter queue */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <h2 className="font-quicksand text-sm font-semibold text-foreground">
            Dead Letter Queue ({deadLetters.length})
          </h2>
          {deadLetters.length > 0 && (
            <button
              onClick={() => retryAllMutation.mutate()}
              disabled={retryAllMutation.isPending}
              className="inline-flex items-center gap-1.5 rounded-lg bg-amber-100/80 dark:bg-amber-900/30 px-3 py-1.5 text-xs font-semibold text-amber-900 dark:text-amber-300 transition-colors hover:bg-amber-200/80 dark:hover:bg-amber-900/50 disabled:opacity-60"
            >
              {retryAllMutation.isPending ? (
                <Loader2 className="h-3 w-3 animate-spin" />
              ) : (
                <RotateCcw className="h-3 w-3" />
              )}
              Retry All
            </button>
          )}
        </div>
        <div className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200/60 dark:border-zinc-700/40">
                  <th className="text-left px-4 py-3 text-xs font-medium text-muted-foreground">
                    Recipient
                  </th>
                  <th className="text-left px-4 py-3 text-xs font-medium text-muted-foreground">
                    Subject
                  </th>
                  <th className="text-left px-4 py-3 text-xs font-medium text-muted-foreground">
                    Error
                  </th>
                  <th className="text-left px-4 py-3 text-xs font-medium text-muted-foreground">
                    Failed
                  </th>
                  <th className="text-left px-4 py-3 text-xs font-medium text-muted-foreground">
                    Retries
                  </th>
                  <th className="text-right px-4 py-3 text-xs font-medium text-muted-foreground">
                    Action
                  </th>
                </tr>
              </thead>
              <tbody>
                {deadLetters.length === 0 && (
                  <tr>
                    <td colSpan={6} className="px-4 py-8 text-center text-muted-foreground text-sm">
                      No dead letter emails
                    </td>
                  </tr>
                )}
                {deadLetters.map((email: EmailQueueItem) => {
                  const isRetrying = retryingId === email.id;
                  return (
                    <tr
                      key={email.id}
                      className="border-b border-slate-100/60 dark:border-zinc-800/40 last:border-0"
                    >
                      <td className="px-4 py-3 font-medium text-foreground">{email.to}</td>
                      <td className="px-4 py-3 text-muted-foreground max-w-[200px] truncate">
                        {email.subject}
                      </td>
                      <td className="px-4 py-3">
                        <span className="inline-flex items-center gap-1 text-red-600 dark:text-red-400 text-xs max-w-[200px] truncate">
                          <XCircle className="h-3 w-3 shrink-0" />
                          {email.errorMessage ?? 'Unknown error'}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-muted-foreground text-xs">
                        {formatRelative(email.failedAt)}
                      </td>
                      <td className="px-4 py-3 text-muted-foreground text-xs">
                        {email.retryCount}/{email.maxRetries}
                      </td>
                      <td className="px-4 py-3 text-right">
                        <button
                          onClick={() => handleRetry(email)}
                          disabled={isRetrying}
                          className="inline-flex items-center gap-1 rounded-lg bg-amber-100/80 dark:bg-amber-900/30 px-2.5 py-1 text-xs font-semibold text-amber-900 dark:text-amber-300 transition-colors hover:bg-amber-200/80 dark:hover:bg-amber-900/50 disabled:opacity-60"
                        >
                          {isRetrying ? (
                            <Loader2 className="h-3 w-3 animate-spin" />
                          ) : (
                            <RefreshCw className="h-3 w-3" />
                          )}
                          Retry
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      {/* Email History */}
      <div>
        <h2 className="font-quicksand text-sm font-semibold text-foreground mb-3">Email History</h2>
        <div className="mb-3">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <input
              type="text"
              value={searchTerm}
              onChange={e => setSearchTerm(e.target.value)}
              placeholder="Search by email or subject..."
              className="w-full rounded-lg border border-slate-200/60 dark:border-zinc-700/40 bg-white/50 dark:bg-zinc-900/50 pl-9 pr-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-amber-400/40"
            />
          </div>
        </div>
        <div className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200/60 dark:border-zinc-700/40">
                  <th className="text-left px-4 py-3 text-xs font-medium text-muted-foreground">
                    Recipient
                  </th>
                  <th className="text-left px-4 py-3 text-xs font-medium text-muted-foreground">
                    Subject
                  </th>
                  <th className="text-left px-4 py-3 text-xs font-medium text-muted-foreground">
                    Status
                  </th>
                  <th className="text-left px-4 py-3 text-xs font-medium text-muted-foreground">
                    Created
                  </th>
                  <th className="text-left px-4 py-3 text-xs font-medium text-muted-foreground">
                    Processed
                  </th>
                </tr>
              </thead>
              <tbody>
                {history.length === 0 && (
                  <tr>
                    <td colSpan={5} className="px-4 py-8 text-center text-muted-foreground text-sm">
                      {historyQuery.isLoading ? 'Loading...' : 'No emails found'}
                    </td>
                  </tr>
                )}
                {history.map((email: EmailQueueItem) => (
                  <tr
                    key={email.id}
                    className="border-b border-slate-100/60 dark:border-zinc-800/40 last:border-0"
                  >
                    <td className="px-4 py-3 font-medium text-foreground">{email.to}</td>
                    <td className="px-4 py-3 text-muted-foreground max-w-[250px] truncate">
                      {email.subject}
                    </td>
                    <td className="px-4 py-3">
                      <StatusBadge status={email.status} />
                    </td>
                    <td className="px-4 py-3 text-muted-foreground text-xs">
                      {formatDate(email.createdAt)}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground text-xs">
                      {formatDate(email.processedAt)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  const config: Record<string, { bg: string; text: string; label: string }> = {
    sent: {
      bg: 'bg-emerald-100/80 dark:bg-emerald-900/30',
      text: 'text-emerald-700 dark:text-emerald-400',
      label: 'Sent',
    },
    pending: {
      bg: 'bg-amber-100/80 dark:bg-amber-900/30',
      text: 'text-amber-700 dark:text-amber-400',
      label: 'Pending',
    },
    processing: {
      bg: 'bg-blue-100/80 dark:bg-blue-900/30',
      text: 'text-blue-700 dark:text-blue-400',
      label: 'Processing',
    },
    failed: {
      bg: 'bg-orange-100/80 dark:bg-orange-900/30',
      text: 'text-orange-700 dark:text-orange-400',
      label: 'Failed',
    },
    dead_letter: {
      bg: 'bg-red-100/80 dark:bg-red-900/30',
      text: 'text-red-700 dark:text-red-400',
      label: 'Dead Letter',
    },
  };

  const c = config[status] ?? {
    bg: 'bg-slate-100/80 dark:bg-slate-900/30',
    text: 'text-slate-700 dark:text-slate-400',
    label: status,
  };

  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${c.bg} ${c.text}`}
    >
      {c.label}
    </span>
  );
}
