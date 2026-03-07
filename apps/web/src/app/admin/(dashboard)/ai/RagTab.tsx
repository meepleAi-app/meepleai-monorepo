'use client';

import { BrainCircuit, Bug, Database, Layers, MessageSquare, Workflow } from 'lucide-react';

import { AdminHubQuickLink } from '@/components/admin/layout/AdminHubQuickLink';

export function RagTab() {
  return (
    <div className="space-y-5">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          RAG Pipeline
        </h2>
        <p className="text-sm text-muted-foreground mt-0.5">
          Retrieval-Augmented Generation pipeline configuration and monitoring.
        </p>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <AdminHubQuickLink
          href="/admin/agents/pipeline"
          icon={<Workflow />}
          label="Pipeline Explorer"
          description="Visual pipeline diagram with step-by-step execution trace"
          accent="bg-amber-500/10 text-amber-600 dark:text-amber-400"
        />
        <AdminHubQuickLink
          href="/admin/agents/debug"
          icon={<Bug />}
          label="Debug Console"
          description="Waterfall chart, confidence scoring, and retrieval analysis"
          accent="bg-rose-500/10 text-rose-600 dark:text-rose-400"
        />
        <AdminHubQuickLink
          href="/admin/agents/strategy"
          icon={<BrainCircuit />}
          label="Strategy Config"
          description="Configure tier-based strategy mapping and model routing"
          accent="bg-violet-500/10 text-violet-600 dark:text-violet-400"
        />
        <AdminHubQuickLink
          href="/admin/knowledge-base"
          icon={<Database />}
          label="Knowledge Base"
          description="Documents, vectors, and embedding service management"
        />
        <AdminHubQuickLink
          href="/admin/knowledge-base/vectors"
          icon={<Layers />}
          label="Vector Collections"
          description="Qdrant collections, point counts, and collection health"
          accent="bg-emerald-500/10 text-emerald-600 dark:text-emerald-400"
        />
        <AdminHubQuickLink
          href="/admin/agents/debug-chat"
          icon={<MessageSquare />}
          label="Debug Chat"
          description="Interactive RAG-instrumented chat with full pipeline trace"
          accent="bg-blue-500/10 text-blue-600 dark:text-blue-400"
        />
      </div>
    </div>
  );
}
