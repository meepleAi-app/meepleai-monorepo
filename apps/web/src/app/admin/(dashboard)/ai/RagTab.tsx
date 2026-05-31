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
          accent="bg-entity-agent/12 text-entity-agent"
        />
        <AdminHubQuickLink
          href="/admin/agents/debug"
          icon={<Bug />}
          label="Debug Console"
          description="Waterfall chart, confidence scoring, and retrieval analysis"
          accent="bg-entity-event/12 text-entity-event"
        />
        <AdminHubQuickLink
          href="/admin/agents/strategy"
          icon={<BrainCircuit />}
          label="Strategy Config"
          description="Configure tier-based strategy mapping and model routing"
          accent="bg-entity-player/12 text-entity-player"
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
          description="pgvector embeddings, vector counts, and collection health"
          accent="bg-entity-toolkit/12 text-entity-toolkit"
        />
        <AdminHubQuickLink
          href="/admin/agents/debug-chat"
          icon={<MessageSquare />}
          label="Debug Chat"
          description="Interactive RAG-instrumented chat with full pipeline trace"
          accent="bg-entity-session/12 text-entity-session"
        />
      </div>
    </div>
  );
}
