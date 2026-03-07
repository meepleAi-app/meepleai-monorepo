'use client';

import { useState, useEffect, useCallback } from 'react';

import { Tag, Check, Trash2 } from 'lucide-react';

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
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Agent Typologies
        </h2>
        <p className="text-sm text-muted-foreground mt-1">
          Manage agent classification types and approval workflow.
        </p>
      </div>

      {loading ? (
        <div className="grid gap-3">
          {[1, 2, 3].map(i => (
            <div
              key={i}
              className="h-20 rounded-xl bg-white/40 dark:bg-zinc-800/40 animate-pulse"
            />
          ))}
        </div>
      ) : typologies.length > 0 ? (
        <div className="grid gap-3">
          {typologies.map(t => (
            <div
              key={t.id}
              className="flex items-start gap-4 rounded-xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md p-4"
            >
              <Tag className="mt-0.5 h-5 w-5 text-primary shrink-0" />
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  <p className="font-medium text-foreground">{t.name}</p>
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
              <div className="flex items-center gap-1 shrink-0">
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
        <div className="rounded-xl border border-dashed border-border/60 p-12 text-center">
          <Tag className="mx-auto h-8 w-8 text-muted-foreground/50" />
          <p className="mt-3 text-sm text-muted-foreground">No typologies found.</p>
        </div>
      )}
    </div>
  );
}
