'use client';

import { ExternalLink, ListOrdered } from 'lucide-react';
import Link from 'next/link';

export function DefinitionsTab() {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            Agent Definitions
          </h2>
          <p className="text-sm text-muted-foreground mt-1">
            Manage agent system prompts, configurations, and KB card bindings.
          </p>
        </div>
        <Link
          href="/admin/agents/definitions"
          className="inline-flex items-center gap-1.5 rounded-md border border-border/60 px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground hover:border-primary/50 transition-colors"
        >
          Full Manager <ExternalLink className="h-3.5 w-3.5" />
        </Link>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <QuickLink
          href="/admin/agents/definitions"
          icon={<ListOrdered className="h-5 w-5" />}
          label="All Definitions"
          description="Browse all agent definitions"
        />
        <QuickLink
          href="/admin/agents/definitions/create"
          icon={<ListOrdered className="h-5 w-5" />}
          label="Create New"
          description="Create a new agent definition"
        />
        <QuickLink
          href="/admin/agents/builder"
          icon={<ListOrdered className="h-5 w-5" />}
          label="Agent Builder"
          description="Visual step-by-step agent creation"
        />
      </div>
    </div>
  );
}

function QuickLink({
  href,
  icon,
  label,
  description,
}: {
  href: string;
  icon: React.ReactNode;
  label: string;
  description: string;
}) {
  return (
    <Link
      href={href}
      className="flex items-start gap-3 rounded-xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md p-4 hover:border-primary/40 transition-colors"
    >
      <div className="text-primary mt-0.5">{icon}</div>
      <div>
        <p className="font-medium text-sm text-foreground">{label}</p>
        <p className="text-xs text-muted-foreground mt-0.5">{description}</p>
      </div>
    </Link>
  );
}
