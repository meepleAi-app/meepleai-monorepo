import { type ReactNode } from 'react';

import { GameNightContextBar } from '@/components/game-night/GameNightContextBar';
import { ContextBarRegistrar } from '@/components/layout/ContextBar';

export default function GameNightDetailLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <ContextBarRegistrar>
        <GameNightContextBar />
      </ContextBarRegistrar>
      {children}
    </>
  );
}
