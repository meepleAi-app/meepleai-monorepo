'use client';

import { useState, useEffect } from 'react';

import { Cpu, Star, Settings } from 'lucide-react';

import { AdminHubEmptyState } from '@/components/admin/layout/AdminHubEmptyState';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { AiModelDto } from '@/lib/api/schemas';

export function ModelsTab() {
  const [models, setModels] = useState<AiModelDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.admin
      .getAiModels()
      .then(data => {
        const items = (data as Record<string, unknown>)?.items;
        setModels(Array.isArray(items) ? (items as AiModelDto[]) : []);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const handleSetPrimary = async (modelId: string) => {
    try {
      await api.admin.setPrimaryModel({ modelId });
      const data = await api.admin.getAiModels();
      const items = (data as Record<string, unknown>)?.items;
      setModels(Array.isArray(items) ? (items as AiModelDto[]) : []);
    } catch {
      // toast error
    }
  };

  return (
    <div className="space-y-5">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          AI Models
        </h2>
        <p className="text-sm text-muted-foreground mt-0.5">
          Configure LLM providers, routing rules, and cost tracking.
        </p>
      </div>

      {loading ? (
        <div className="grid gap-3 sm:grid-cols-2">
          {[1, 2, 3, 4].map(i => (
            <div
              key={i}
              className="h-28 rounded-xl bg-white/40 dark:bg-zinc-800/40 animate-pulse"
            />
          ))}
        </div>
      ) : models.length > 0 ? (
        <div className="grid gap-3 sm:grid-cols-2">
          {models.map(m => (
            <div
              key={m.id}
              className="relative rounded-xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md p-3 sm:p-4"
            >
              {m.isPrimary && (
                <span className="absolute top-3 right-3 inline-flex items-center gap-1 rounded-full bg-amber-100 dark:bg-amber-900/30 px-2 py-0.5 text-[10px] font-medium text-amber-700 dark:text-amber-400">
                  <Star className="h-3 w-3" /> Primary
                </span>
              )}
              <div className="flex items-start gap-3">
                <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary/10">
                  <Cpu className="h-4 w-4 text-primary" />
                </div>
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium text-foreground truncate pr-16 sm:pr-20">
                    {m.name}
                  </p>
                  <p className="text-xs text-muted-foreground mt-0.5 truncate">
                    {m.provider} · {m.modelIdentifier}
                  </p>
                  <div className="mt-2 flex items-center gap-2">
                    <span
                      className={`inline-block h-1.5 w-1.5 rounded-full ${
                        m.status === 'active' ? 'bg-emerald-500' : 'bg-zinc-300 dark:bg-zinc-600'
                      }`}
                    />
                    <span className="text-[10px] text-muted-foreground capitalize">{m.status}</span>
                  </div>
                </div>
              </div>
              <div className="mt-3 flex items-center gap-2">
                {!m.isPrimary && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => handleSetPrimary(m.id)}
                    className="text-xs"
                  >
                    <Star className="mr-1 h-3 w-3" /> Set Primary
                  </Button>
                )}
                <Button variant="ghost" size="sm" className="text-xs">
                  <Settings className="mr-1 h-3 w-3" /> Configure
                </Button>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <AdminHubEmptyState
          icon={<Cpu />}
          title="No AI models configured"
          description="Add and configure LLM providers to enable AI-powered features."
        />
      )}
    </div>
  );
}
