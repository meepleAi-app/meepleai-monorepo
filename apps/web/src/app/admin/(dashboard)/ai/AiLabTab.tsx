'use client';

import { ExternalLink, FlaskConical } from 'lucide-react';
import Link from 'next/link';

export function AiLabTab() {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            AI Lab
          </h2>
          <p className="text-sm text-muted-foreground mt-1">
            Test AI agents, run playground sessions, and experiment with prompts.
          </p>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <LabLink
          href="/admin/agents/definitions/playground"
          icon={<FlaskConical className="h-5 w-5" />}
          label="Agent Playground"
          description="Interactive chat testing with any agent definition"
        />
        <LabLink
          href="/admin/agents/debug-chat"
          icon={<FlaskConical className="h-5 w-5" />}
          label="Debug Chat"
          description="Send RAG-instrumented queries with full pipeline trace"
        />
        <LabLink
          href="/admin/agents/debug"
          icon={<FlaskConical className="h-5 w-5" />}
          label="Debug Console"
          description="RAG pipeline debug console with waterfall visualization"
        />
        <LabLink
          href="/admin/agents/pipeline"
          icon={<FlaskConical className="h-5 w-5" />}
          label="Pipeline Explorer"
          description="Visual RAG pipeline diagram with step timings"
        />
      </div>
    </div>
  );
}

function LabLink({
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
      className="group flex items-start gap-3 rounded-xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md p-4 hover:border-primary/40 transition-colors"
    >
      <div className="text-primary mt-0.5 group-hover:scale-110 transition-transform">{icon}</div>
      <div>
        <div className="flex items-center gap-1.5">
          <p className="font-medium text-sm text-foreground">{label}</p>
          <ExternalLink className="h-3 w-3 text-muted-foreground opacity-0 group-hover:opacity-100 transition-opacity" />
        </div>
        <p className="text-xs text-muted-foreground mt-0.5">{description}</p>
      </div>
    </Link>
  );
}
