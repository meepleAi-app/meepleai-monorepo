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
      {/* #4525: white on bg-entity-kb is 1.88:1 in the dark theme, so there the ink is the page
          background (9.26:1). The hover is a shadow: `hover:bg-entity-kb/90` over the white card
          took the white text to 4.27:1 in the light theme. */}
      <button
        type="button"
        onClick={onCta}
        className="px-4 py-2 rounded-md bg-entity-kb text-white dark:text-background text-sm font-medium shadow-sm transition-shadow hover:shadow-md"
      >
        {labels.cta}
      </button>
    </div>
  );
}
