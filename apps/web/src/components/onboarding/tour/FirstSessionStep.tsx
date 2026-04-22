import type { JSX } from 'react';

import { ACTIONS, type TourAction } from './data';

export interface FirstSessionChoice {
  readonly id: string;
  readonly href: string;
}

export interface FirstSessionStepProps {
  readonly onChoose: (choice: FirstSessionChoice) => void;
}

function ActionCard({
  action,
  onChoose,
}: {
  readonly action: TourAction;
  readonly onChoose: (choice: FirstSessionChoice) => void;
}): JSX.Element {
  return (
    <button
      type="button"
      data-entity={action.entity}
      aria-label={action.title}
      onClick={() => onChoose({ id: action.id, href: action.href })}
      className="group flex w-full items-center gap-3 rounded-xl border border-border bg-card px-4 py-4 text-left transition-colors hover:border-[hsl(var(--e-session))] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2"
    >
      <span className="text-2xl" aria-hidden="true">
        {action.emoji}
      </span>
      <div className="flex flex-1 flex-col">
        <span className="font-quicksand text-sm font-semibold">{action.title}</span>
        <span className="font-nunito text-xs text-muted-foreground">{action.desc}</span>
      </div>
      <span
        aria-hidden="true"
        className="text-lg text-muted-foreground transition-transform group-hover:translate-x-1"
      >
        →
      </span>
    </button>
  );
}

export function FirstSessionStep({ onChoose }: FirstSessionStepProps): JSX.Element {
  return (
    <div className="flex flex-col gap-5 px-4 py-6">
      <div className="text-center">
        <h2 className="font-quicksand text-xl font-bold">
          Tutto pronto! Cosa vuoi fare <span className="text-[hsl(var(--e-session))]">ora</span>?
        </h2>
        <p className="mt-1 font-nunito text-sm text-muted-foreground">
          Scegli da dove partire — puoi sempre tornare.
        </p>
      </div>
      <div className="flex flex-col gap-2">
        {ACTIONS.map(a => (
          <ActionCard key={a.id} action={a} onChoose={onChoose} />
        ))}
      </div>
    </div>
  );
}
