import type { JSX } from 'react';

import { Btn } from '@/components/ui/v2/btn';

export interface WelcomeStepProps {
  readonly userName: string | null;
  readonly onStart: () => void;
  readonly onSkip: () => void;
}

export function WelcomeStep({ userName, onStart, onSkip }: WelcomeStepProps): JSX.Element {
  return (
    <div className="flex flex-col items-center gap-8 px-6 py-10 text-center">
      <div className="flex flex-col items-center gap-4" aria-hidden="true">
        <div className="flex h-20 w-20 items-center justify-center rounded-2xl bg-gradient-to-br from-amber-400 to-orange-500 text-3xl font-bold text-white">
          M
        </div>
        <div className="flex gap-2 text-3xl">
          <span>♟️</span>
          <span>🎲</span>
          <span>🃏</span>
        </div>
      </div>
      <div className="flex flex-col items-center gap-3">
        <h1 className="font-quicksand text-2xl font-bold sm:text-3xl">
          {userName ? (
            <>
              Benvenuto in{' '}
              <span className="bg-gradient-to-r from-amber-500 to-orange-500 bg-clip-text text-transparent">
                MeepleAI
              </span>
              , {userName}!
            </>
          ) : (
            <>
              Benvenuto in{' '}
              <span className="bg-gradient-to-r from-amber-500 to-orange-500 bg-clip-text text-transparent">
                MeepleAI
              </span>
              !
            </>
          )}
        </h1>
        <p className="font-nunito text-sm text-muted-foreground">
          Configuriamo l&apos;app in 3 minuti — giochi, agenti e prima sessione.
        </p>
      </div>
      <div className="flex w-full max-w-xs flex-col gap-2">
        <Btn variant="primary" size="lg" fullWidth onClick={onStart}>
          Inizia il tour →
        </Btn>
        <Btn variant="ghost" size="md" fullWidth onClick={onSkip}>
          Salta, esploro da solo
        </Btn>
      </div>
    </div>
  );
}
