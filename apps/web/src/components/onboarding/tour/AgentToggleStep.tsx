import type { JSX } from 'react';

import { ToggleSwitch } from '@/components/ui/v2/toggle-switch';

import { AGENTS } from './data';

export type AgentStates = Record<string, boolean>;

export interface AgentToggleStepProps {
  readonly agentStates: AgentStates;
  readonly onToggle: (id: string) => void;
}

export function AgentToggleStep({ agentStates, onToggle }: AgentToggleStepProps): JSX.Element {
  const activeCount = AGENTS.filter(a => agentStates[a.id]).length;
  return (
    <div className="flex flex-col gap-5 px-4 py-6">
      <div className="text-center">
        <h2 className="font-quicksand text-xl font-bold">
          Attiva i tuoi <span className="text-[hsl(var(--e-agent))]">assistenti</span>
        </h2>
        <p className="mt-1 font-nunito text-sm text-muted-foreground">
          Ogni agente è specializzato in un tipo di domanda. Puoi modificarli in qualsiasi momento.
        </p>
      </div>
      <ul className="flex flex-col gap-2">
        {AGENTS.map(a => (
          <li
            key={a.id}
            className="flex items-center gap-3 rounded-xl border border-border bg-card px-3 py-3"
          >
            <span className="text-2xl" aria-hidden="true">
              {a.emoji}
            </span>
            <div className="flex flex-1 flex-col">
              <span className="font-quicksand text-sm font-semibold">{a.name}</span>
              <span className="font-nunito text-xs text-muted-foreground">{a.desc}</span>
            </div>
            <ToggleSwitch
              entity="agent"
              checked={agentStates[a.id] ?? false}
              onCheckedChange={() => onToggle(a.id)}
              ariaLabel={a.name}
            />
          </li>
        ))}
      </ul>
      <p aria-live="polite" className="text-center font-nunito text-sm text-muted-foreground">
        {activeCount === 1 ? '1 agente attivo' : `${activeCount} agenti attivi`}
      </p>
    </div>
  );
}
