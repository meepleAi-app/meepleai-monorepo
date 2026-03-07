'use client';

import { useState, useEffect } from 'react';

import { ScrollText, Plus, Trash2, Pencil } from 'lucide-react';
import Link from 'next/link';

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
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            Prompt Templates
          </h2>
          <p className="text-sm text-muted-foreground mt-1">
            System prompts for AI agents. Version-controlled with audit history.
          </p>
        </div>
        <Link
          href="/admin/agents/models"
          className="inline-flex items-center gap-1.5 rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:bg-primary/90 transition-colors"
        >
          <Plus className="h-3.5 w-3.5" /> New Prompt
        </Link>
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
      ) : prompts.length > 0 ? (
        <div className="grid gap-3">
          {prompts.map(p => (
            <div
              key={p.id}
              className="flex items-start gap-4 rounded-xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md p-4"
            >
              <ScrollText className="mt-0.5 h-5 w-5 text-primary shrink-0" />
              <div className="flex-1 min-w-0">
                <p className="font-medium text-foreground truncate">{p.name}</p>
                <p className="mt-0.5 text-xs text-muted-foreground line-clamp-1">
                  {p.description ?? 'No description'}
                </p>
                <p className="mt-1 text-[10px] text-muted-foreground/60">
                  {p.isActive ? 'Active' : 'Inactive'} &middot; {p.category}
                </p>
              </div>
              <div className="flex items-center gap-1 shrink-0">
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
        <div className="rounded-xl border border-dashed border-border/60 p-12 text-center">
          <ScrollText className="mx-auto h-8 w-8 text-muted-foreground/50" />
          <p className="mt-3 text-sm text-muted-foreground">No prompt templates found.</p>
        </div>
      )}
    </div>
  );
}
