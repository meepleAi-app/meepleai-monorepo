import type { JSX } from 'react';

import { Btn } from '@/components/ui/v2/btn';

import { Confetti } from './Confetti';

export interface CompleteStepProps {
  readonly isSubmitting: boolean;
  readonly error: string | null;
  readonly onHome: () => void;
}

export function CompleteStep({ isSubmitting, error, onHome }: CompleteStepProps): JSX.Element {
  return (
    <div className="relative flex min-h-[360px] flex-col items-center justify-center gap-6 px-6 py-10 text-center">
      <Confetti />
      <div
        className="flex h-24 w-24 items-center justify-center rounded-2xl bg-gradient-to-br from-amber-400 to-orange-500 text-4xl font-bold text-white"
        aria-hidden="true"
      >
        M
      </div>
      <h1 className="font-quicksand text-2xl font-bold sm:text-3xl">
        <span className="bg-gradient-to-r from-amber-500 to-orange-500 bg-clip-text text-transparent">
          Benvenuto!
        </span>
      </h1>
      <p className="font-nunito text-sm text-muted-foreground">
        MeepleAI è pronto.
        <br />
        Buon gioco!
      </p>
      {error && (
        <p role="alert" className="font-nunito text-sm text-destructive">
          {error}
        </p>
      )}
      <Btn
        variant="primary"
        size="lg"
        onClick={onHome}
        disabled={isSubmitting}
        loading={isSubmitting}
      >
        Vai alla home →
      </Btn>
    </div>
  );
}
