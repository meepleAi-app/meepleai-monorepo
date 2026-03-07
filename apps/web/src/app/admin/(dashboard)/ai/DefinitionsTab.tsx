'use client';

import { Hammer, ListOrdered, PlusCircle } from 'lucide-react';
import Link from 'next/link';

import { AdminHubQuickLink } from '@/components/admin/layout/AdminHubQuickLink';

export function DefinitionsTab() {
  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            Agent Definitions
          </h2>
          <p className="text-sm text-muted-foreground mt-0.5">
            Manage agent system prompts, configurations, and KB card bindings.
          </p>
        </div>
        <Link
          href="/admin/agents/definitions"
          className="inline-flex items-center justify-center gap-1.5 rounded-lg border border-border/60 px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground hover:border-primary/50 transition-colors self-start sm:self-auto"
        >
          Full Manager
        </Link>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <AdminHubQuickLink
          href="/admin/agents/definitions"
          icon={<ListOrdered />}
          label="All Definitions"
          description="Browse and search all agent definitions"
        />
        <AdminHubQuickLink
          href="/admin/agents/definitions/create"
          icon={<PlusCircle />}
          label="Create New"
          description="Create a new agent definition from scratch"
          accent="bg-emerald-500/10 text-emerald-600 dark:text-emerald-400"
        />
        <AdminHubQuickLink
          href="/admin/agents/builder"
          icon={<Hammer />}
          label="Agent Builder"
          description="Visual step-by-step agent creation wizard"
          accent="bg-amber-500/10 text-amber-600 dark:text-amber-400"
        />
      </div>
    </div>
  );
}
