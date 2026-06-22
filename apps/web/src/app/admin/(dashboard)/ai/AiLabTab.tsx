'use client';

import { Bug, FlaskConical, MessageSquare, Workflow } from 'lucide-react';

import { AdminHubQuickLink } from '@/components/admin/layout/AdminHubQuickLink';

export function AiLabTab() {
  return (
    <div className="space-y-5">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          AI Lab
        </h2>
        <p className="text-sm text-muted-foreground mt-0.5">
          Test AI agents, run playground sessions, and experiment with prompts.
        </p>
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <AdminHubQuickLink
          href="/admin/agents/definitions/playground"
          icon={<FlaskConical />}
          label="Agent Playground"
          description="Interactive chat testing with any agent definition"
          accent="bg-entity-player/12 text-entity-player"
        />
        <AdminHubQuickLink
          href="/admin/agents/debug-chat"
          icon={<MessageSquare />}
          label="Debug Chat"
          description="Send RAG-instrumented queries with full pipeline trace"
          accent="bg-entity-session/12 text-entity-session"
        />
        <AdminHubQuickLink
          href="/admin/agents/debug"
          icon={<Bug />}
          label="Debug Console"
          description="RAG pipeline debug console with waterfall visualization"
          accent="bg-entity-event/12 text-entity-event"
        />
        <AdminHubQuickLink
          href="/admin/agents/pipeline"
          icon={<Workflow />}
          label="Pipeline Explorer"
          description="Visual RAG pipeline diagram with step timings"
          accent="bg-entity-agent/12 text-entity-agent"
        />
      </div>
    </div>
  );
}
