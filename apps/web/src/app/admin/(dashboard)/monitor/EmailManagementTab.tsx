'use client';

import { useState } from 'react';

import { AlertCircle, RefreshCw, Send, Clock, XCircle, Loader2 } from 'lucide-react';

import { toast } from '@/components/layout';

interface DeadLetterEmail {
  id: string;
  recipient: string;
  subject: string;
  error: string;
  failedAt: string;
  retries: number;
}

const PLACEHOLDER_DEAD_LETTERS: DeadLetterEmail[] = [
  {
    id: '1',
    recipient: 'user@example.com',
    subject: 'Welcome to MeepleAI',
    error: 'SMTP connection timeout',
    failedAt: '2 hours ago',
    retries: 2,
  },
  {
    id: '2',
    recipient: 'admin@boardgame.io',
    subject: 'Game Approval Notification',
    error: 'Recipient mailbox full',
    failedAt: '5 hours ago',
    retries: 1,
  },
  {
    id: '3',
    recipient: 'player@test.dev',
    subject: 'Password Reset',
    error: 'Invalid recipient address',
    failedAt: '1 day ago',
    retries: 3,
  },
];

function StatCard({
  label,
  value,
  icon: Icon,
  color,
}: {
  label: string;
  value: string;
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

export function EmailManagementTab() {
  const [retryingId, setRetryingId] = useState<string | null>(null);

  async function handleRetry(email: DeadLetterEmail) {
    setRetryingId(email.id);
    try {
      await new Promise(resolve => setTimeout(resolve, 600));
      toast.info(`Retry for "${email.subject}": API endpoint not yet connected`);
    } finally {
      setRetryingId(null);
    }
  }

  return (
    <div className="space-y-6">
      {/* Info banner */}
      <div className="flex items-center gap-2 rounded-xl border border-amber-200/60 dark:border-amber-800/40 bg-amber-50/50 dark:bg-amber-900/10 px-4 py-3">
        <AlertCircle className="h-4 w-4 text-amber-600 dark:text-amber-400 shrink-0" />
        <p className="text-xs text-amber-700 dark:text-amber-300">
          Showing placeholder data. Email management will display real stats once the email service
          API is connected.
        </p>
      </div>

      {/* Stats cards */}
      <div className="grid gap-4 sm:grid-cols-3">
        <StatCard
          label="Emails Sent (24h)"
          value="—"
          icon={Send}
          color="bg-emerald-100/80 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400"
        />
        <StatCard
          label="Pending"
          value="—"
          icon={Clock}
          color="bg-amber-100/80 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400"
        />
        <StatCard
          label="Failed (Dead Letter)"
          value={String(PLACEHOLDER_DEAD_LETTERS.length)}
          icon={XCircle}
          color="bg-red-100/80 dark:bg-red-900/30 text-red-700 dark:text-red-400"
        />
      </div>

      {/* Dead letter queue */}
      <div>
        <h2 className="font-quicksand text-sm font-semibold text-foreground mb-3">
          Dead Letter Queue
        </h2>
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
                {PLACEHOLDER_DEAD_LETTERS.map(email => {
                  const isRetrying = retryingId === email.id;
                  return (
                    <tr
                      key={email.id}
                      className="border-b border-slate-100/60 dark:border-zinc-800/40 last:border-0"
                    >
                      <td className="px-4 py-3 font-medium text-foreground">{email.recipient}</td>
                      <td className="px-4 py-3 text-muted-foreground">{email.subject}</td>
                      <td className="px-4 py-3">
                        <span className="inline-flex items-center gap-1 text-red-600 dark:text-red-400 text-xs">
                          <XCircle className="h-3 w-3" />
                          {email.error}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-muted-foreground text-xs">{email.failedAt}</td>
                      <td className="px-4 py-3 text-muted-foreground text-xs">{email.retries}</td>
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
    </div>
  );
}
