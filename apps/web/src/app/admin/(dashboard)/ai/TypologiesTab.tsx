'use client';

import { useState, useEffect, useCallback } from 'react';

import { Tag, Check, Trash2 } from 'lucide-react';

import { AdminHubEmptyState } from '@/components/admin/layout/AdminHubEmptyState';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';

interface AgentTypology {
  id: string;
  name: string;
  description: string;
  isApproved: boolean;
  createdAt: string;
}

export function TypologiesTab() {
  const [typologies, setTypologies] = useState<AgentTypology[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchTypologies = useCallback(() => {
    setLoading(true);
    api.admin
      .getAgentTypologies()
      .then(data => {
        setTypologies(
          Array.isArray(data)
            ? (data as AgentTypology[])
            : (((data as Record<string, unknown>)?.items as AgentTypology[]) ?? [])
        );
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    fetchTypologies();
  }, [fetchTypologies]);

  const handleApprove = async (id: string) => {
    try {
      await api.admin.approveAgentTypology(id);
      fetchTypologies();
    } catch {
      // toast error
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await api.admin.deleteAgentTypology(id);
      fetchTypologies();
    } catch {
      // toast error
    }
  };

  return (
    <div className="space-y-5">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Agent Typologies
        </h2>
        <p className="text-sm text-muted-foreground mt-0.5">
          Manage agent classification types and approval workflow.
        </p>
      </div>

      {loading ? (
        <div className="grid gap-2">
          {[1, 2, 3].map(i => (
            <div
              key={i}
              className="h-20 rounded-xl bg-white/40 dark:bg-zinc-800/40 animate-pulse"
            />
          ))}
        </div>
      ) : typologies.length > 0 ? (
        <div className="grid gap-2">
          {typologies.map(t => (
            <div
              key={t.id}
              className="flex flex-col gap-3 sm:flex-row sm:items-start rounded-xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md p-3 sm:p-4"
            >
              <div className="flex items-start gap-3 flex-1 min-w-0">
                <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary/10">
                  <Tag className="h-4 w-4 text-primary" />
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <p className="text-sm font-medium text-foreground">{t.name}</p>
                    {t.isApproved ? (
                      <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 dark:bg-emerald-900/30 px-2 py-0.5 text-[10px] font-medium text-emerald-700 dark:text-emerald-400">
                        <Check className="h-3 w-3" /> Approved
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 dark:bg-amber-900/30 px-2 py-0.5 text-[10px] font-medium text-amber-700 dark:text-amber-400">
                        Pending
                      </span>
                    )}
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground line-clamp-2">{t.description}</p>
                </div>
              </div>
              <div className="flex items-center gap-1 shrink-0 self-end sm:self-start">
                {!t.isApproved && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => handleApprove(t.id)}
                    aria-label="Approve"
                  >
                    <Check className="h-4 w-4 text-emerald-600" />
                  </Button>
                )}
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => handleDelete(t.id)}
                  aria-label="Delete"
                >
                  <Trash2 className="h-4 w-4 text-destructive" />
                </Button>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <AdminHubEmptyState
          icon={<Tag />}
          title="No typologies found"
          description="Agent typologies will appear here once they are created."
        />
      )}
    </div>
  );
}
