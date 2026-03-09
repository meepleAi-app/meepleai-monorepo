'use client';

import { useState, useEffect } from 'react';

import { ScrollText, Plus, Trash2, Pencil } from 'lucide-react';
import Link from 'next/link';

import { AdminHubEmptyState } from '@/components/admin/layout/AdminHubEmptyState';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { PromptTemplate } from '@/lib/api/schemas';

export function PromptsTab() {
  const [prompts, setPrompts] = useState<PromptTemplate[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.admin
      .getPrompts()
      .then(data => {
        setPrompts(Array.isArray(data) ? data : []);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            Prompt Templates
          </h2>
          <p className="text-sm text-muted-foreground mt-0.5">
            System prompts for AI agents. Version-controlled with audit history.
          </p>
        </div>
        <Link
          href="/admin/agents/models"
          className="inline-flex items-center justify-center gap-1.5 rounded-lg bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:bg-primary/90 transition-colors self-start sm:self-auto"
        >
          <Plus className="h-3.5 w-3.5" /> New Prompt
        </Link>
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
      ) : prompts.length > 0 ? (
        <div className="grid gap-2">
          {prompts.map(p => (
            <div
              key={p.id}
              className="flex flex-col gap-3 sm:flex-row sm:items-start rounded-xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md p-3 sm:p-4"
            >
              <div className="flex items-start gap-3 flex-1 min-w-0">
                <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary/10">
                  <ScrollText className="h-4 w-4 text-primary" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-foreground truncate">{p.name}</p>
                  <p className="mt-0.5 text-xs text-muted-foreground line-clamp-1">
                    {p.description ?? 'No description'}
                  </p>
                  <div className="mt-1.5 flex items-center gap-2">
                    <span
                      className={`inline-block h-1.5 w-1.5 rounded-full ${
                        p.isActive ? 'bg-emerald-500' : 'bg-zinc-300 dark:bg-zinc-600'
                      }`}
                    />
                    <span className="text-[10px] text-muted-foreground">
                      {p.isActive ? 'Active' : 'Inactive'} · {p.category}
                    </span>
                  </div>
                </div>
              </div>
              <div className="flex items-center gap-1 shrink-0 self-end sm:self-start">
                <Button variant="ghost" size="sm" aria-label="Edit prompt">
                  <Pencil className="h-4 w-4" />
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  aria-label="Delete prompt"
                  onClick={() => api.admin.deletePrompt(p.id).catch(() => {})}
                >
                  <Trash2 className="h-4 w-4 text-destructive" />
                </Button>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <AdminHubEmptyState
          icon={<ScrollText />}
          title="No prompt templates found"
          description="Create prompt templates for your AI agents to use."
          action={
            <Link
              href="/admin/agents/models"
              className="inline-flex items-center gap-1.5 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90 transition-colors"
            >
              <Plus className="h-3.5 w-3.5" /> New Prompt
            </Link>
          }
        />
      )}
    </div>
  );
}
