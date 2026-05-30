'use client';
import { type JSX } from 'react';

export interface DrawerEmptyLabels {
  title: string;
  body: string;
  cta: string;
}

export function DrawerEmpty({
  onCta,
  labels,
}: {
  onCta: () => void;
  labels: DrawerEmptyLabels;
}): JSX.Element {
  return (
    <div
      data-testid="drawer-state-empty"
      className="flex-1 p-6 flex flex-col items-center justify-center text-center"
    >
      <div className="text-4xl mb-3">📚</div>
      <div className="font-display font-bold text-base mb-2">{labels.title}</div>
      <div className="text-sm text-muted-foreground mb-4 leading-relaxed">{labels.body}</div>
      <button
        type="button"
        onClick={onCta}
        className="px-4 py-2 rounded-md bg-entity-kb text-white text-sm font-medium hover:bg-entity-kb/90"
      >
        {labels.cta}
      </button>
    </div>
  );
}
