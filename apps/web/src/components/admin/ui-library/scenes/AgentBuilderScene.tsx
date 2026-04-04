'use client';

import { AgentConfigPanel } from '@/components/admin/agents/AgentConfigPanel';

// AgentBuilderModal requires useQuery + admin API context.
// LlmProviderSelector and DocumentSelector require API hooks.
// AgentConfigPanel uses react-hook-form with local storage — it renders standalone.

function PlaceholderCard({
  title,
  description,
  note,
}: {
  title: string;
  description: string;
  note?: string;
}) {
  return (
    <div className="flex flex-col gap-2 rounded-xl border border-dashed border-border/50 bg-muted/20 p-5">
      <p className="font-quicksand text-sm font-semibold text-foreground">{title}</p>
      <p className="font-nunito text-xs text-muted-foreground">{description}</p>
      {note && (
        <span className="inline-flex w-fit items-center rounded-full border border-border/50 px-2.5 py-0.5 font-nunito text-[10px] text-muted-foreground">
          {note}
        </span>
      )}
    </div>
  );
}

export default function AgentBuilderScene() {
  return (
    <div className="space-y-8">
      {/* AgentConfigPanel — renders standalone with react-hook-form + local defaults */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          Agent Config Panel
        </h3>
        <AgentConfigPanel
          mode="Chat"
          config={{
            mode: 'Chat',
            confidenceThreshold: 0.7,
            temperature: 0.7,
            maxTokens: 2048,
            useContextWindow: true,
            enableRuleLookup: true,
            maxHistoryMessages: 10,
          }}
          onConfigChange={() => undefined}
        />
      </div>

      {/* Components requiring API context */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          Additional Components (require context)
        </h3>
        <div className="grid gap-3 sm:grid-cols-2">
          <PlaceholderCard
            title="AgentBuilderModal"
            description="Full-screen modal wizard for creating AI agents with model selection, KB card attachment, and system prompt configuration."
            note="requires useQuery + admin API"
          />
          <PlaceholderCard
            title="LlmProviderSelector"
            description="Dropdown with live model availability, cost per token, and context window info."
            note="requires admin API client"
          />
          <PlaceholderCard
            title="DocumentSelector"
            description="Searchable checklist for attaching KB cards to an agent definition."
            note="requires useQuery"
          />
        </div>
      </div>
    </div>
  );
}
