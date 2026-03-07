'use client';

import { BrainCircuit, ExternalLink } from 'lucide-react';
import Link from 'next/link';

export function RagTab() {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          RAG Pipeline
        </h2>
        <p className="text-sm text-muted-foreground mt-1">
          Retrieval-Augmented Generation pipeline configuration and monitoring.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <RagLink
          href="/admin/agents/pipeline"
          label="Pipeline Explorer"
          description="Visual pipeline diagram with step-by-step execution trace"
        />
        <RagLink
          href="/admin/agents/debug"
          label="Debug Console"
          description="Waterfall chart, confidence scoring, and retrieval analysis"
        />
        <RagLink
          href="/admin/agents/strategy"
          label="Strategy Config"
          description="Configure tier-based strategy mapping and model routing"
        />
        <RagLink
          href="/admin/knowledge-base"
          label="Knowledge Base"
          description="Documents, vectors, and embedding service management"
        />
        <RagLink
          href="/admin/knowledge-base/vectors"
          label="Vector Collections"
          description="Qdrant collections, point counts, and collection health"
        />
        <RagLink
          href="/admin/agents/debug-chat"
          label="Debug Chat"
          description="Interactive RAG-instrumented chat with full pipeline trace"
        />
      </div>
    </div>
  );
}

function RagLink({
  href,
  label,
  description,
}: {
  href: string;
  label: string;
  description: string;
}) {
  return (
    <Link
      href={href}
      className="group flex items-start gap-3 rounded-xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md p-4 hover:border-primary/40 transition-colors"
    >
      <BrainCircuit className="mt-0.5 h-5 w-5 text-primary shrink-0 group-hover:scale-110 transition-transform" />
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
