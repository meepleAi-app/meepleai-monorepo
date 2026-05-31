'use client';

import { useSearchParams } from 'next/navigation';

const TAB_LABELS: Record<string, string> = {
  agents: 'Agents',
  typologies: 'Typologies',
  definitions: 'Definitions',
  lab: 'AI Lab',
  prompts: 'Prompts',
  models: 'Models',
  requests: 'Requests',
  rag: 'RAG',
  config: 'Config',
};

function labelFor(tab: string | null): string {
  if (!tab) return TAB_LABELS.agents;
  return TAB_LABELS[tab] ?? TAB_LABELS.agents;
}

export function AiCrumbs() {
  const params = useSearchParams();
  return (
    <div className="font-mono text-[11px] text-muted-foreground mt-0.5">
      Admin · AI · {labelFor(params?.get('tab') ?? null)}
    </div>
  );
}
